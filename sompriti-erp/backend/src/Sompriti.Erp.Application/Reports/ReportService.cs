using Microsoft.EntityFrameworkCore;
using Sompriti.Erp.Application.Common;
using Sompriti.Erp.Application.Orders;
using Sompriti.Erp.Domain.Common;
using Sompriti.Erp.Domain.Entities;
using Sompriti.Erp.Domain.Enums;

namespace Sompriti.Erp.Application.Reports;

public sealed record ReportQuery : PageQuery
{
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public Guid? CompanyUuid { get; init; }
    public bool DueOnly { get; init; }
}

public sealed record PartyReportRow(Guid PartyUuid, string PartyName, string PartyCode, string MobileNumber,
    int OrderCount, decimal TotalAmount, decimal TotalPaid, decimal Due);

public sealed record ReportTotals(int OrderCount, decimal TotalAmount, decimal TotalPaid, decimal Due);

public sealed record PartyReport(PagedResult<PartyReportRow> Rows, ReportTotals Totals);

public sealed record CompanyReportRow(Guid CompanyUuid, string CompanyName, string CompanyCode,
    int SalesCount, decimal SalesTotal, decimal SalesPaid, decimal SalesDue,
    int? PurchaseCount, decimal? PurchaseTotal, decimal? PurchasePaid, decimal? PurchaseDue);

public sealed record DashboardDto(
    Role Role,
    decimal? TodaySales, decimal? MonthSales, decimal? MonthPurchases,
    decimal? CustomerDue, decimal? SupplierDue,
    int? DraftSalesOrders, int? DraftPurchaseOrders,
    IReadOnlyList<LowStockItem> LowStock,
    IReadOnlyList<OrderListItemDto> LatestSales,
    IReadOnlyList<OrderListItemDto> LatestPurchases,
    LinkedSummary? AsBuyer, LinkedSummary? AsSupplier,
    IReadOnlyList<DailyTotal> SalesLast30Days);

public sealed record LowStockItem(Guid ProductUuid, string ProductCode, string ProductName, int CurrentStock, int Threshold);
public sealed record LinkedSummary(Guid PartyUuid, string PartyName, int OrderCount, decimal TotalAmount, decimal TotalPaid, decimal Due);
public sealed record DailyTotal(DateOnly Date, decimal Total);

public sealed class ReportService(IAppDbContext db, ICurrentUser user, TimeProvider clock,
    SalesOrderService salesOrders, PurchaseOrderService purchaseOrders)
{
    // ------------------------------------------------------------------ party reports

    public Task<PartyReport> CustomerReportAsync(ReportQuery q, CancellationToken ct)
    {
        Guid? scope = null;
        if (user.Role == Role.User)
        {
            if (user.CustomerUuid is null) return Task.FromResult(Empty(q));
            scope = user.CustomerUuid;
        }
        return PartyReportAsync<SalesOrder, Customer>(q, scope, ct);
    }

    public Task<PartyReport> SupplierReportAsync(ReportQuery q, CancellationToken ct)
    {
        if (user.Role == Role.Manager) throw DomainException.Forbidden();
        Guid? scope = null;
        if (user.Role == Role.User)
        {
            if (user.SupplierUuid is null) return Task.FromResult(Empty(q));
            scope = user.SupplierUuid;
        }
        return PartyReportAsync<PurchaseOrder, Supplier>(q, scope, ct);
    }

    private static PartyReport Empty(PageQuery q) =>
        new(new PagedResult<PartyReportRow>([], q.SafePage, q.SafePageSize, 0), new ReportTotals(0, 0, 0, 0));

    private IQueryable<TOrder> FinalOrders<TOrder>(ReportQuery q) where TOrder : OrderHeader
    {
        var orders = db.Set<TOrder>().AsNoTracking()
            .Where(o => o.Status == RecordStatus.Active && o.PostingStatus == PostingStatus.Final);
        if (q.FromDate is { } f) orders = orders.Where(o => o.OrderDate >= f);
        if (q.ToDate is { } t) orders = orders.Where(o => o.OrderDate <= t);
        if (q.CompanyUuid is { } c) orders = orders.Where(o => o.CompanyUuid == c);
        return orders;
    }

    private async Task<PartyReport> PartyReportAsync<TOrder, TParty>(ReportQuery q, Guid? scope, CancellationToken ct)
        where TOrder : OrderHeader where TParty : PartyEntity
    {
        var orders = FinalOrders<TOrder>(q);
        var parties = db.Set<TParty>().AsNoTracking().AsQueryable();
        if (scope is { } s) parties = parties.Where(p => p.Uuid == s);
        if (q.Term is { } term)
        {
            var t = term.ToLower();
            parties = parties.Where(p => p.Name.ToLower().Contains(t) || p.Code!.Contains(t));
        }

        // Project to an anonymous type first: EF can filter/sort anonymous members but not record constructor arguments.
        var rows = from p in parties
                   where p.Status == RecordStatus.Active || orders.Any(o => o.PartyUuid == p.Uuid)
                   select new
                   {
                       p.Uuid,
                       p.Name,
                       Code = p.Code!,
                       p.MobileNumber,
                       Count = orders.Count(o => o.PartyUuid == p.Uuid),
                       Total = orders.Where(o => o.PartyUuid == p.Uuid).Sum(o => (decimal?)o.TotalAmount) ?? 0,
                       Paid = orders.Where(o => o.PartyUuid == p.Uuid).Sum(o => (decimal?)o.TotalPaidAmount) ?? 0,
                   };

        if (q.DueOnly) rows = rows.Where(r => r.Total - r.Paid > 0);

        var desc = q.Sort?.StartsWith('-') ?? true;
        rows = q.Sort?.TrimStart('-') switch
        {
            "partyName" => desc ? rows.OrderByDescending(r => r.Name) : rows.OrderBy(r => r.Name),
            "totalAmount" => desc ? rows.OrderByDescending(r => r.Total) : rows.OrderBy(r => r.Total),
            "totalPaid" => desc ? rows.OrderByDescending(r => r.Paid) : rows.OrderBy(r => r.Paid),
            _ => desc ? rows.OrderByDescending(r => r.Total - r.Paid).ThenBy(r => r.Name) : rows.OrderBy(r => r.Total - r.Paid).ThenBy(r => r.Name)
        };

        var page = await rows.ToPagedAsync(q, r => new PartyReportRow(r.Uuid, r.Name, r.Code, r.MobileNumber, r.Count, r.Total, r.Paid, r.Total - r.Paid), ct);
        page = page with { Items = page.Items.Select(r => r with { MobileNumber = Domain.Rules.BdMobile.ToDisplay(r.MobileNumber) }).ToList() };

        var scoped = scope is { } sc ? orders.Where(o => o.PartyUuid == sc) : orders;
        var totalsRow = await scoped.GroupBy(_ => 1)
            .Select(g => new ReportTotals(g.Count(), g.Sum(o => o.TotalAmount), g.Sum(o => o.TotalPaidAmount), g.Sum(o => o.TotalAmount - o.TotalPaidAmount)))
            .FirstOrDefaultAsync(ct);
        return new PartyReport(page, totalsRow ?? new ReportTotals(0, 0, 0, 0));
    }

    // ------------------------------------------------------------------ company report

    public async Task<IReadOnlyList<CompanyReportRow>> CompanyReportAsync(ReportQuery q, CancellationToken ct)
    {
        if (user.Role == Role.User) throw DomainException.Forbidden();
        var includePurchases = user.Role == Role.Admin;
        var sales = FinalOrders<SalesOrder>(q);
        var purchases = FinalOrders<PurchaseOrder>(q);

        var companies = await db.Companies.AsNoTracking()
            .Where(c => c.Status == RecordStatus.Active || sales.Any(o => o.CompanyUuid == c.Uuid) || purchases.Any(o => o.CompanyUuid == c.Uuid))
            .OrderBy(c => c.CompanyName)
            .Select(c => new
            {
                c.Uuid, c.CompanyName, c.CompanyCode,
                SalesCount = sales.Count(o => o.CompanyUuid == c.Uuid),
                SalesTotal = sales.Where(o => o.CompanyUuid == c.Uuid).Sum(o => (decimal?)o.TotalAmount) ?? 0,
                SalesPaid = sales.Where(o => o.CompanyUuid == c.Uuid).Sum(o => (decimal?)o.TotalPaidAmount) ?? 0,
                PurchaseCount = purchases.Count(o => o.CompanyUuid == c.Uuid),
                PurchaseTotal = purchases.Where(o => o.CompanyUuid == c.Uuid).Sum(o => (decimal?)o.TotalAmount) ?? 0,
                PurchasePaid = purchases.Where(o => o.CompanyUuid == c.Uuid).Sum(o => (decimal?)o.TotalPaidAmount) ?? 0,
            }).ToListAsync(ct);

        return companies.Select(c => new CompanyReportRow(c.Uuid, c.CompanyName, c.CompanyCode,
            c.SalesCount, c.SalesTotal, c.SalesPaid, c.SalesTotal - c.SalesPaid,
            includePurchases ? c.PurchaseCount : null, includePurchases ? c.PurchaseTotal : null,
            includePurchases ? c.PurchasePaid : null, includePurchases ? c.PurchaseTotal - c.PurchasePaid : null)).ToList();
    }

    // ------------------------------------------------------------------ dashboard

    public async Task<DashboardDto> DashboardAsync(Guid? companyUuid, CancellationToken ct)
    {
        var today = BusinessClock.Today(clock.GetUtcNow());
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var empty = new ReportQuery { CompanyUuid = companyUuid };

        if (user.Role == Role.User)
        {
            LinkedSummary? buyer = null, supplier = null;
            var latestSales = new List<OrderListItemDto>();
            var latestPurchases = new List<OrderListItemDto>();
            if (user.CustomerUuid is { } cid)
            {
                buyer = await LinkedAsync<SalesOrder, Customer>(cid, empty, ct);
                latestSales = (await salesOrders.ListAsync(new OrderListQuery { PageSize = 10 }, ct)).Items.ToList();
            }
            if (user.SupplierUuid is { } sid)
            {
                supplier = await LinkedAsync<PurchaseOrder, Supplier>(sid, empty, ct);
                latestPurchases = (await purchaseOrders.ListAsync(new OrderListQuery { PageSize = 10 }, ct)).Items.ToList();
            }
            return new DashboardDto(user.Role, null, null, null, null, null, null, null, [], latestSales, latestPurchases, buyer, supplier, []);
        }

        var isAdmin = user.Role == Role.Admin;
        var finalSales = FinalOrders<SalesOrder>(empty);
        var finalPurchases = FinalOrders<PurchaseOrder>(empty);

        var todaySales = await finalSales.Where(o => o.OrderDate == today).SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0;
        var monthSales = await finalSales.Where(o => o.OrderDate >= monthStart).SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0;
        var customerDue = await finalSales.SumAsync(o => (decimal?)(o.TotalAmount - o.TotalPaidAmount), ct) ?? 0;

        var draftSales = await db.SalesOrders.CountAsync(o => o.Status == RecordStatus.Active && o.PostingStatus == PostingStatus.Draft
                                                             && (companyUuid == null || o.CompanyUuid == companyUuid), ct);

        decimal? monthPurchases = null, supplierDue = null;
        int? draftPurchases = null;
        var latestPurch = new List<OrderListItemDto>();
        if (isAdmin)
        {
            monthPurchases = await finalPurchases.Where(o => o.OrderDate >= monthStart).SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0;
            supplierDue = await finalPurchases.SumAsync(o => (decimal?)(o.TotalAmount - o.TotalPaidAmount), ct) ?? 0;
            draftPurchases = await db.PurchaseOrders.CountAsync(o => o.Status == RecordStatus.Active && o.PostingStatus == PostingStatus.Draft
                                                                   && (companyUuid == null || o.CompanyUuid == companyUuid), ct);
            latestPurch = (await purchaseOrders.ListAsync(new OrderListQuery { PageSize = 10, CompanyUuid = companyUuid }, ct)).Items.ToList();
        }

        var latestSalesList = (await salesOrders.ListAsync(new OrderListQuery { PageSize = 10, CompanyUuid = companyUuid }, ct)).Items.ToList();

        var lowStock = await (from p in db.Products.AsNoTracking()
                              join s in db.StockBalances.AsNoTracking() on p.Uuid equals s.ProductUuid
                              where p.Status == RecordStatus.Active && p.LowStockThreshold != null && s.CurrentStockBalance <= p.LowStockThreshold
                              orderby s.CurrentStockBalance
                              select new LowStockItem(p.Uuid, p.ProductCode, p.ProductName, s.CurrentStockBalance, p.LowStockThreshold!.Value))
                             .Take(20).ToListAsync(ct);

        var from30 = today.AddDays(-29);
        var daily = await finalSales.Where(o => o.OrderDate >= from30)
            .GroupBy(o => o.OrderDate)
            .Select(g => new { Date = g.Key, Total = g.Sum(o => o.TotalAmount) })
            .ToListAsync(ct);
        var series = Enumerable.Range(0, 30).Select(i => from30.AddDays(i))
            .Select(d => new DailyTotal(d, daily.FirstOrDefault(x => x.Date == d)?.Total ?? 0)).ToList();

        return new DashboardDto(user.Role, todaySales, monthSales, monthPurchases, customerDue, supplierDue,
            draftSales, draftPurchases, lowStock, latestSalesList, latestPurch, null, null, series);
    }

    private async Task<LinkedSummary?> LinkedAsync<TOrder, TParty>(Guid partyUuid, ReportQuery q, CancellationToken ct)
        where TOrder : OrderHeader where TParty : PartyEntity
    {
        var name = await db.Set<TParty>().AsNoTracking().Where(p => p.Uuid == partyUuid).Select(p => p.Name).FirstOrDefaultAsync(ct);
        if (name is null) return null;
        var orders = FinalOrders<TOrder>(q).Where(o => o.PartyUuid == partyUuid);
        var count = await orders.CountAsync(ct);
        var total = await orders.SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0;
        var paid = await orders.SumAsync(o => (decimal?)o.TotalPaidAmount, ct) ?? 0;
        return new LinkedSummary(partyUuid, name, count, total, paid, total - paid);
    }
}
