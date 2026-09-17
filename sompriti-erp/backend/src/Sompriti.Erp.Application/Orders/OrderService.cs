using Microsoft.EntityFrameworkCore;
using Sompriti.Erp.Application.Common;
using Sompriti.Erp.Application.MasterData;
using Sompriti.Erp.Application.Stock;
using Sompriti.Erp.Domain.Common;
using Sompriti.Erp.Domain.Entities;
using Sompriti.Erp.Domain.Enums;
using Sompriti.Erp.Domain.Rules;

namespace Sompriti.Erp.Application.Orders;

/// <summary>
/// Shared purchase/sales order logic: draft editing, finalize, void, payments, detail and list.
/// Stock direction and party type are supplied by the concrete service.
/// </summary>
public abstract class OrderService<TOrder, TLine, TPayment, TParty>(
    IAppDbContext db, ICurrentUser user, StockService stock, ISmsSender sms, IOrderPdfRenderer pdf, TimeProvider clock)
    where TOrder : OrderHeader, new()
    where TLine : OrderLine, new()
    where TPayment : OrderPayment, new()
    where TParty : PartyEntity
{
    protected readonly IAppDbContext Db = db;
    protected readonly ICurrentUser User = user;

    protected abstract TransactionType Type { get; }
    protected abstract string OrderTable { get; }
    protected abstract string Label { get; }      // "Purchase order"
    protected abstract string PartyLabel { get; } // "Supplier"
    /// <summary>For USER role: the linked party uuid for this order type, or null if not linked.</summary>
    protected abstract Guid? LinkedPartyUuid { get; }
    protected abstract decimal DefaultPrice(Product product);
    /// <summary>Extra validation when a draft is saved (sales checks available stock).</summary>
    protected virtual Task ValidateDraftLinesAsync(IReadOnlyList<TLine> lines, CancellationToken ct) => Task.CompletedTask;

    private DbSet<TOrder> Orders => Db.Set<TOrder>();
    private DbSet<TLine> Lines => Db.Set<TLine>();
    private DbSet<TPayment> Payments => Db.Set<TPayment>();
    private DbSet<TParty> Parties => Db.Set<TParty>();

    private DateOnly Today => BusinessClock.Today(clock.GetUtcNow());

    // ================================================================== reads

    /// <summary>Base query limited to what the current user may see.</summary>
    private IQueryable<TOrder> Visible()
    {
        var q = Orders.AsNoTracking().Where(o => o.Status == RecordStatus.Active);
        if (User.Role == Role.User)
        {
            var linked = LinkedPartyUuid;
            if (linked is null) return q.Where(o => false);
            q = q.Where(o => o.PartyUuid == linked.Value);
        }
        return q;
    }

    public async Task<PagedResult<OrderListItemDto>> ListAsync(OrderListQuery q, CancellationToken ct)
    {
        var orders = Visible();
        if (q.PostingStatus is { } ps) orders = orders.Where(o => o.PostingStatus == ps);
        if (q.FromDate is { } from) orders = orders.Where(o => o.OrderDate >= from);
        if (q.ToDate is { } to) orders = orders.Where(o => o.OrderDate <= to);
        if (q.PartyUuid is { } pid) orders = orders.Where(o => o.PartyUuid == pid);
        if (q.CompanyUuid is { } cid) orders = orders.Where(o => o.CompanyUuid == cid);
        if (q.DueOnly) orders = orders.Where(o => o.PostingStatus == PostingStatus.Final && o.TotalAmount > o.TotalPaidAmount);

        var query = from o in orders
                    join c in Db.Companies.AsNoTracking() on o.CompanyUuid equals c.Uuid
                    join p in Parties.AsNoTracking() on o.PartyUuid equals p.Uuid
                    select new { o, c, p };

        if (q.Term is { } t)
        {
            var term = t.ToLower();
            query = query.Where(x => x.o.OrderNumber!.Contains(term) || x.p.Name.ToLower().Contains(term) || x.p.Code!.Contains(term));
        }

        var desc = q.Sort?.StartsWith('-') ?? true;
        query = (q.Sort?.TrimStart('-')) switch
        {
            "orderNumber" => desc ? query.OrderByDescending(x => x.o.OrderNumber) : query.OrderBy(x => x.o.OrderNumber),
            "totalAmount" => desc ? query.OrderByDescending(x => x.o.TotalAmount) : query.OrderBy(x => x.o.TotalAmount),
            "partyName" => desc ? query.OrderByDescending(x => x.p.Name) : query.OrderBy(x => x.p.Name),
            "orderDate" => desc ? query.OrderByDescending(x => x.o.OrderDate).ThenByDescending(x => x.o.CreatedDate)
                                : query.OrderBy(x => x.o.OrderDate).ThenBy(x => x.o.CreatedDate),
            _ => query.OrderByDescending(x => x.o.OrderDate).ThenByDescending(x => x.o.CreatedDate)
        };

        return await query.ToPagedAsync(q, x => new OrderListItemDto(x.o.Uuid, x.o.TransactionType, x.o.OrderNumber!, x.o.OrderDate,
            x.c.Uuid, x.c.CompanyName, x.p.Uuid, x.p.Name, x.p.Code!, x.o.PaymentType, x.o.PostingStatus,
            x.o.TotalAmount, x.o.TotalPaidAmount, x.o.TotalAmount - x.o.TotalPaidAmount,
            x.o.CreatedDate, x.o.CreatedByUserName, x.o.Revision), ct);
    }

    public async Task<OrderDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        var o = (await Visible().FirstOrDefaultAsync(x => x.Uuid == id, ct)).OrNotFound(Label);
        var company = await Db.Companies.AsNoTracking().FirstAsync(c => c.Uuid == o.CompanyUuid, ct);
        var party = await Parties.AsNoTracking().FirstAsync(p => p.Uuid == o.PartyUuid, ct);

        var lines = await (from l in Lines.AsNoTracking()
                           join p in Db.Products.AsNoTracking() on l.ProductUuid equals p.Uuid
                           where l.OrderUuid == id && l.Status == RecordStatus.Active
                           orderby l.LineNumber
                           select new OrderLineDto(l.Uuid, l.LineNumber, l.ProductUuid, p.ProductCode, p.ProductName, l.QuantityType,
                               l.BoxQuantity, l.PcsQuantity, l.PcsPerBoxSnapshot, l.TotalQuantityPcs, l.PerPcsPrice, l.PerBoxPrice,
                               l.TotalPrice)).ToListAsync(ct);

        var payments = await Payments.AsNoTracking()
            .Where(p => p.OrderUuid == id && p.Status == RecordStatus.Active)
            .OrderBy(p => p.PaymentDate).ThenBy(p => p.CreatedDate)
            .Select(p => new OrderPaymentDto(p.Uuid, p.PaymentDate, p.PaymentAmount, p.PaymentMethod, p.PaymentNote, p.CreatedDate, p.CreatedByUserName))
            .ToListAsync(ct);

        return new OrderDetailDto(o.Uuid, o.TransactionType, o.OrderNumber!,
            new OrderCompanyDto(company.Uuid, company.CompanyName, company.CompanyCode),
            new OrderPartyDto(party.Uuid, party.Name, party.Code ?? "", BdMobile.ToDisplay(party.MobileNumber), party.Address, party.City),
            o.PaymentType, o.PostingStatus, o.OrderDate, o.Notes, o.TotalAmount, o.TotalPaidAmount, o.DueAmount,
            o.FinalizedDate, o.FinalizedByUserName, o.VoidedDate, o.VoidedByUserName, o.VoidReason,
            o.Revision, o.CreatedDate, o.UpdatedDate, o.CreatedByUserName, o.UpdatedByUserName, lines, payments);
    }

    public async Task<(byte[] Content, string FileName)> PdfAsync(Guid id, CancellationToken ct)
    {
        var detail = await GetAsync(id, ct);
        var company = await Db.Companies.AsNoTracking().FirstAsync(c => c.Uuid == detail.Company.Uuid, ct);
        var bytes = pdf.Render(detail, CompanyService.ToDto(company));
        var prefix = Type == TransactionType.Sales ? "sales-invoice" : "purchase-order";
        return (bytes, $"{prefix}-{detail.OrderNumber}.pdf");
    }

    // ================================================================== draft editing

    public async Task<OrderDetailDto> CreateAsync(OrderSaveRequest r, CancellationToken ct)
    {
        var order = new TOrder { Uuid = Guid.NewGuid(), PostingStatus = PostingStatus.Draft };
        await ApplyHeaderAsync(order, r, ct);
        var lines = await BuildLinesAsync(order, r.Lines ?? [], [], ct);
        await ValidateDraftLinesAsync(lines.Where(l => l.IsActive).ToList(), ct);

        await using var tx = await Db.Database.BeginTransactionAsync(ct);
        Orders.Add(order);
        await Db.SaveChangesAsync(ct); // header first: generates order number
        foreach (var l in lines) Lines.Add(l);
        await Db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(order.Uuid, ct);
    }

    public async Task<OrderDetailDto> UpdateAsync(Guid id, OrderSaveRequest r, CancellationToken ct)
    {
        await using var tx = await Db.Database.BeginTransactionAsync(ct);
        var order = await LockAsync(id, ct);
        RevisionGuard.Check(Db, order, r.Revision ?? Guid.Empty);
        PostingRules.EnsureDraft(order);

        await ApplyHeaderAsync(order, r, ct);
        var existing = await Lines.Where(l => l.OrderUuid == id && l.Status == RecordStatus.Active).ToListAsync(ct);
        var lines = await BuildLinesAsync(order, r.Lines ?? [], existing, ct);
        await ValidateDraftLinesAsync(lines.Where(l => l.IsActive).ToList(), ct);

        foreach (var l in lines.Where(l => existing.All(e => e.Uuid != l.Uuid))) Lines.Add(l);
        Touch(order);
        await Db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<OrderDetailDto> DeleteLineAsync(Guid id, Guid lineId, Guid revision, CancellationToken ct)
    {
        await using var tx = await Db.Database.BeginTransactionAsync(ct);
        var order = await LockAsync(id, ct);
        RevisionGuard.Check(Db, order, revision);
        PostingRules.EnsureDraft(order);

        var line = (await Lines.FirstOrDefaultAsync(l => l.Uuid == lineId && l.OrderUuid == id && l.Status == RecordStatus.Active, ct))
            .OrNotFound("Line item");
        line.Status = RecordStatus.Deleted;
        order.TotalAmount = Money.Round(await Lines.Where(l => l.OrderUuid == id && l.Status == RecordStatus.Active && l.Uuid != lineId)
            .SumAsync(l => l.TotalPrice, ct));
        Touch(order);
        await Db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Soft-deletes a whole DRAFT order.</summary>
    public async Task DeleteAsync(Guid id, Guid revision, CancellationToken ct)
    {
        await using var tx = await Db.Database.BeginTransactionAsync(ct);
        var order = await LockAsync(id, ct);
        RevisionGuard.Check(Db, order, revision);
        if (order.PostingStatus != PostingStatus.Draft)
            throw DomainException.Conflict(ErrorCodes.InvalidStatusTransition, "Only DRAFT orders can be deleted. Use VOID for finalized orders.");
        order.Status = RecordStatus.Deleted;
        await Db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private async Task ApplyHeaderAsync(TOrder order, OrderSaveRequest r, CancellationToken ct)
    {
        var v = new Validator()
            .Required("companyUuid", r.CompanyUuid, "Company")
            .Required("partyUuid", r.PartyUuid, PartyLabel)
            .MaxLength("notes", r.Notes, "Notes", 1000);
        v.When(r.PaymentType is null, "paymentType", "Payment type is required.");
        v.When(r.OrderDate > Today, "orderDate", "Order date cannot be in the future.");
        v.ThrowIfInvalid();

        if (!await Db.Companies.AnyAsync(c => c.Uuid == r.CompanyUuid && c.Status == RecordStatus.Active, ct))
            throw DomainException.Validation("companyUuid", "Selected company is not available.");
        if (!await Parties.AnyAsync(p => p.Uuid == r.PartyUuid && p.Status == RecordStatus.Active, ct))
            throw DomainException.Validation("partyUuid", $"Selected {PartyLabel.ToLower()} is not available.");

        order.CompanyUuid = r.CompanyUuid!.Value;
        order.PartyUuid = r.PartyUuid!.Value;
        order.PaymentType = r.PaymentType!.Value;
        order.OrderDate = r.OrderDate ?? Today;
        order.Notes = Validator.Clean(r.Notes);
    }

    /// <summary>
    /// Builds the new set of lines. Existing lines found in the request are updated, new ones are created,
    /// and existing lines missing from the request are soft-deleted. Also recalculates the header total.
    /// Returns all touched lines (including deleted ones).
    /// </summary>
    private async Task<List<TLine>> BuildLinesAsync(TOrder order, IReadOnlyList<OrderLineRequest> requests, List<TLine> existing, CancellationToken ct)
    {
        var v = new Validator();
        var productIds = requests.Where(x => x.ProductUuid is not null).Select(x => x.ProductUuid!.Value).Distinct().ToList();
        var products = await Db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Uuid) && p.Status == RecordStatus.Active)
            .ToDictionaryAsync(p => p.Uuid, ct);

        var result = new List<TLine>();
        var lineNo = 0;
        for (var i = 0; i < requests.Count; i++)
        {
            var req = requests[i];
            var field = $"lines[{i}]";
            if (req.ProductUuid is null) { v.Add($"{field}.productUuid", "Product is required."); continue; }
            if (req.QuantityType is null) { v.Add($"{field}.quantityType", "Quantity type is required."); continue; }
            if (!products.TryGetValue(req.ProductUuid.Value, out var product))
            {
                v.Add($"{field}.productUuid", "Selected product is not available.");
                continue;
            }

            var values = LineCalculator.Normalize(
                new LineInput(req.Uuid, req.ProductUuid.Value, req.QuantityType.Value, req.BoxQuantity, req.PcsQuantity,
                    req.TotalQuantityPcs, req.PerPcsPrice, req.PerBoxPrice, req.TotalPrice),
                product, DefaultPrice(product), field, v);

            TLine line;
            if (req.Uuid is { } lineId && existing.FirstOrDefault(e => e.Uuid == lineId) is { } found)
            {
                line = found;
            }
            else
            {
                line = new TLine { Uuid = Guid.NewGuid(), OrderUuid = order.Uuid };
            }

            line.ProductUuid = product.Uuid;
            line.LineNumber = ++lineNo;
            LineCalculator.Apply(line, values);
            result.Add(line);
        }
        v.ThrowIfInvalid();

        foreach (var removed in existing.Where(e => result.All(r => r.Uuid != e.Uuid)))
        {
            removed.Status = RecordStatus.Deleted;
            result.Add(removed);
        }

        order.TotalAmount = Money.Round(result.Where(l => l.IsActive).Sum(l => l.TotalPrice));
        return result;
    }

    // ================================================================== posting

    public async Task<OrderDetailDto> FinalizeAsync(Guid id, Guid revision, CancellationToken ct)
    {
        await using var tx = await Db.Database.BeginTransactionAsync(ct);
        var order = await LockAsync(id, ct);
        RevisionGuard.Check(Db, order, revision);
        PostingRules.EnsureTransition(order.PostingStatus, PostingStatus.Final);

        var lines = await Lines.Where(l => l.OrderUuid == id && l.Status == RecordStatus.Active).ToListAsync(ct);
        if (lines.Count == 0)
            throw DomainException.Rule(ErrorCodes.BusinessRule, "Add at least one line item before finalizing.");

        var productIds = lines.Select(l => l.ProductUuid).Distinct().ToList();
        var inactive = await Db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Uuid) && p.Status != RecordStatus.Active)
            .Select(p => p.ProductCode).ToListAsync(ct);
        if (inactive.Count > 0)
            throw DomainException.Rule(ErrorCodes.BusinessRule, $"These products are deleted: {string.Join(", ", inactive)}. Remove them from the order.");

        var sign = PostingRules.FinalizeSign(Type);
        var changes = LineCalculator.QuantityByProduct(lines).ToDictionary(kv => kv.Key, kv => kv.Value * sign);
        await stock.ApplyMovementsAsync(changes, PostingRules.FinalizeMovement(Type),
            Type == TransactionType.Sales ? ReferenceType.SalesOrder : ReferenceType.PurchaseOrder,
            order.Uuid, order.OrderNumber!, ErrorCodes.InsufficientStock, "Not enough stock to finalize.", ct);

        order.TotalAmount = Money.Round(lines.Sum(l => l.TotalPrice));
        order.PostingStatus = PostingStatus.Final;
        order.FinalizedDate = clock.GetUtcNow();
        order.FinalizedByUserUuid = User.UserUuid;
        order.FinalizedByUserName = User.UserName;

        await Db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<OrderDetailDto> VoidAsync(Guid id, VoidOrderRequest r, CancellationToken ct)
    {
        new Validator().Required("voidReason", r.VoidReason, "Void reason", 500).ThrowIfInvalid();

        await using var tx = await Db.Database.BeginTransactionAsync(ct);
        var order = await LockAsync(id, ct);
        RevisionGuard.Check(Db, order, r.Revision);
        PostingRules.EnsureTransition(order.PostingStatus, PostingStatus.Void);

        if (order.PostingStatus == PostingStatus.Final)
        {
            var hasPayments = await Payments.AnyAsync(p => p.OrderUuid == id && p.Status == RecordStatus.Active, ct);
            if (hasPayments)
                throw DomainException.Rule(ErrorCodes.OrderHasPayments, "Remove all payments from this order before voiding it.");

            var lines = await Lines.AsNoTracking().Where(l => l.OrderUuid == id && l.Status == RecordStatus.Active).ToListAsync(ct);
            var reverseSign = -PostingRules.FinalizeSign(Type);
            var changes = LineCalculator.QuantityByProduct(lines).ToDictionary(kv => kv.Key, kv => kv.Value * reverseSign);
            await stock.ApplyMovementsAsync(changes, PostingRules.VoidMovement(Type),
                Type == TransactionType.Sales ? ReferenceType.SalesOrder : ReferenceType.PurchaseOrder,
                order.Uuid, order.OrderNumber!, ErrorCodes.NegativeStockOnVoid,
                "Cannot void: the purchased stock has already been used.", ct);
        }

        order.PostingStatus = PostingStatus.Void;
        order.VoidedDate = clock.GetUtcNow();
        order.VoidedByUserUuid = User.UserUuid;
        order.VoidedByUserName = User.UserName;
        order.VoidReason = r.VoidReason!.Trim();

        await Db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    // ================================================================== payments

    public async Task<OrderDetailDto> AddPaymentAsync(Guid id, AddPaymentRequest r, CancellationToken ct)
    {
        var today = Today;
        var v = new Validator().MaxLength("paymentNote", r.PaymentNote, "Payment note", 500);
        v.When(r.PaymentAmount is not > 0, "paymentAmount", "Payment amount must be greater than 0.");
        v.When(r.PaymentDate > today, "paymentDate", "Payment date cannot be in the future.");
        v.ThrowIfInvalid();

        await using var tx = await Db.Database.BeginTransactionAsync(ct);
        var order = await LockAsync(id, ct);
        RevisionGuard.Check(Db, order, r.Revision);
        if (order.PostingStatus != PostingStatus.Final)
            throw DomainException.Rule(ErrorCodes.BusinessRule, "Payments can be added only to FINAL orders.");

        var date = r.PaymentDate ?? today;
        if (date < order.OrderDate)
            throw DomainException.Validation("paymentDate", "Payment date cannot be before the order date.");

        var amount = Money.Round(r.PaymentAmount!.Value);
        if (order.TotalPaidAmount + amount > order.TotalAmount)
            throw DomainException.Rule(ErrorCodes.Overpayment,
                $"Payment exceeds the due amount. Due is {Money.Format(order.DueAmount)}.");

        var payment = new TPayment
        {
            Uuid = Guid.NewGuid(),
            OrderUuid = id,
            PaymentDate = date,
            PaymentAmount = amount,
            PaymentMethod = r.PaymentMethod ?? PaymentMethod.Cash,
            PaymentNote = Validator.Clean(r.PaymentNote),
        };
        Payments.Add(payment);
        order.TotalPaidAmount = Money.Round(order.TotalPaidAmount + amount);

        // Outbox pattern (SRS 11.3): the SMS row is committed together with the payment and sent later by a worker.
        var company = await Db.Companies.AsNoTracking().FirstAsync(c => c.Uuid == order.CompanyUuid, ct);
        var party = await Parties.AsNoTracking().FirstAsync(p => p.Uuid == order.PartyUuid, ct);
        var now = clock.GetUtcNow();
        Db.SmsOutbox.Add(new SmsOutbox
        {
            Uuid = Guid.NewGuid(),
            RecipientNumber = party.MobileNumber,
            Message = SmsTemplates.Payment(Type, company.CompanyName, order.OrderNumber!, amount, order.DueAmount),
            ReferenceType = Type == TransactionType.Sales ? "SALES_ORDER_PAYMENT" : "PURCHASE_ORDER_PAYMENT",
            ReferenceUuid = payment.Uuid,
            Status = sms.Enabled ? SmsStatus.Pending : SmsStatus.Skipped,
            NextAttemptDate = sms.Enabled ? now : null,
            CreatedDate = now,
        });

        await Db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<OrderDetailDto> DeletePaymentAsync(Guid id, Guid paymentId, Guid revision, CancellationToken ct)
    {
        await using var tx = await Db.Database.BeginTransactionAsync(ct);
        var order = await LockAsync(id, ct);
        RevisionGuard.Check(Db, order, revision);
        if (order.PostingStatus != PostingStatus.Final)
            throw DomainException.Rule(ErrorCodes.BusinessRule, "Payments can be removed only from FINAL orders.");

        var payment = (await Payments.FirstOrDefaultAsync(p => p.Uuid == paymentId && p.OrderUuid == id && p.Status == RecordStatus.Active, ct))
            .OrNotFound("Payment");
        payment.Status = RecordStatus.Deleted;
        order.TotalPaidAmount = Money.Round(Math.Max(0, order.TotalPaidAmount - payment.PaymentAmount));
        await Db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    // ================================================================== helpers

    /// <summary>Loads the order header with a row lock (SELECT ... FOR UPDATE). Must be called inside a transaction.</summary>
    private async Task<TOrder> LockAsync(Guid id, CancellationToken ct)
    {
        // Not composed further, so the SQL runs exactly as written.
        var rows = await Orders.FromSqlRaw("SELECT * FROM " + OrderTable + " WHERE uuid = {0} FOR UPDATE", id)
            .ToListAsync(ct);
        var order = rows.FirstOrDefault();
        if (order is null || order.Status != RecordStatus.Active) throw DomainException.NotFound(Label);
        return order;
    }

    /// <summary>Forces a new revision on the header when only child rows changed.</summary>
    private static void Touch(TOrder order) => order.Revision = Guid.NewGuid();
}

public sealed class PurchaseOrderService(IAppDbContext db, ICurrentUser user, StockService stock, ISmsSender sms, IOrderPdfRenderer pdf, TimeProvider clock)
    : OrderService<PurchaseOrder, PurchaseOrderLineItem, PurchaseOrderPayment, Supplier>(db, user, stock, sms, pdf, clock)
{
    protected override TransactionType Type => TransactionType.Purchase;
    protected override string OrderTable => "purchase_order";
    protected override string Label => "Purchase order";
    protected override string PartyLabel => "Supplier";
    protected override Guid? LinkedPartyUuid => User.SupplierUuid;
    protected override decimal DefaultPrice(Product product) => product.ProductPurchasePrice;
}

public sealed class SalesOrderService(IAppDbContext db, ICurrentUser user, StockService stock, ISmsSender sms, IOrderPdfRenderer pdf, TimeProvider clock)
    : OrderService<SalesOrder, SalesOrderLineItem, SalesOrderPayment, Customer>(db, user, stock, sms, pdf, clock)
{
    private readonly StockService _stock = stock;

    protected override TransactionType Type => TransactionType.Sales;
    protected override string OrderTable => "sales_order";
    protected override string Label => "Sales order";
    protected override string PartyLabel => "Customer";
    protected override Guid? LinkedPartyUuid => User.CustomerUuid;
    protected override decimal DefaultPrice(Product product) => product.ProductSalesPrice;

    /// <summary>SRS 8.2: total quantity per product on this order must not exceed current stock (warning check; finalize re-checks with locks).</summary>
    protected override async Task ValidateDraftLinesAsync(IReadOnlyList<SalesOrderLineItem> lines, CancellationToken ct)
    {
        var needed = LineCalculator.QuantityByProduct(lines);
        if (needed.Count == 0) return;
        var balances = await _stock.CurrentBalancesAsync(needed.Keys, ct);
        var shortIds = needed.Where(n => n.Value > balances.GetValueOrDefault(n.Key)).Select(n => n.Key).ToList();
        if (shortIds.Count == 0) return;

        var products = await Db.Products.AsNoTracking().Where(p => shortIds.Contains(p.Uuid)).ToDictionaryAsync(p => p.Uuid, ct);
        var details = shortIds.Select(id => new StockShortage(id, products[id].ProductCode, products[id].ProductName,
            balances.GetValueOrDefault(id), needed[id])).ToList();
        var text = string.Join("; ", details.Select(d => $"{d.ProductName} ({d.ProductCode}): available {d.Available}, requested {d.Required}"));
        throw DomainException.Rule(ErrorCodes.InsufficientStock, $"Not enough stock. {text}", details);
    }
}
