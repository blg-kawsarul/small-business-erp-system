using Microsoft.EntityFrameworkCore;
using Sompriti.Erp.Application.Common;
using Sompriti.Erp.Domain.Common;
using Sompriti.Erp.Domain.Entities;
using Sompriti.Erp.Domain.Enums;

namespace Sompriti.Erp.Application.MasterData;

public sealed record ProductDto(
    Guid Uuid, string ProductName, string ProductCode, decimal ProductSalesPrice, decimal ProductPurchasePrice,
    Uom Uom, int? PcsPerBox, int? LowStockThreshold, int CurrentStock, Guid Revision,
    DateTimeOffset CreatedDate, DateTimeOffset UpdatedDate, string CreatedByUserName, string UpdatedByUserName);

public sealed record ProductSaveRequest(
    string? ProductName, string? ProductCode, decimal? ProductSalesPrice, decimal? ProductPurchasePrice,
    Uom? Uom, int? PcsPerBox, int? LowStockThreshold, Guid? Revision);

public sealed record ProductDropdownItem(
    Guid Uuid, string Code, string Name, string Label, Uom Uom, int? PcsPerBox,
    decimal SalesPrice, decimal PurchasePrice, int CurrentStock);

public sealed class ProductService(IAppDbContext db)
{
    private static readonly Dictionary<string, System.Linq.Expressions.Expression<Func<ProductRow, object?>>> SortMap = new()
    {
        ["productName"] = x => x.P.ProductName,
        ["productCode"] = x => x.P.ProductCode,
        ["productSalesPrice"] = x => x.P.ProductSalesPrice,
        ["productPurchasePrice"] = x => x.P.ProductPurchasePrice,
        ["currentStock"] = x => x.Stock,
        ["createdDate"] = x => x.P.CreatedDate,
    };

    public sealed class ProductRow
    {
        public Product P { get; set; } = null!;
        public int Stock { get; set; }
    }

    private IQueryable<ProductRow> Rows() =>
        from p in db.Products.AsNoTracking()
        join s in db.StockBalances.AsNoTracking() on p.Uuid equals s.ProductUuid into sj
        from s in sj.DefaultIfEmpty()
        where p.Status == RecordStatus.Active
        select new ProductRow { P = p, Stock = s == null ? 0 : s.CurrentStockBalance };

    private static ProductDto ToDto(Product p, int stock) => new(p.Uuid, p.ProductName, p.ProductCode, p.ProductSalesPrice,
        p.ProductPurchasePrice, p.Uom, p.PcsPerBox, p.LowStockThreshold, stock, p.Revision, p.CreatedDate, p.UpdatedDate,
        p.CreatedByUserName, p.UpdatedByUserName);

    public async Task<PagedResult<ProductDto>> ListAsync(PageQuery q, bool lowStockOnly, CancellationToken ct)
    {
        var query = Rows();
        if (q.Term is { } t)
        {
            var term = t.ToLower();
            query = query.Where(x => x.P.ProductName.ToLower().Contains(term) || x.P.ProductCode.ToLower().Contains(term));
        }
        if (lowStockOnly) query = query.Where(x => x.P.LowStockThreshold != null && x.Stock <= x.P.LowStockThreshold);
        var page = await query.ApplySort(q.Sort, SortMap, s => s.OrderBy(x => x.P.ProductName)).ToPagedAsync(q, x => x, ct);
        return new PagedResult<ProductDto>(page.Items.Select(x => ToDto(x.P, x.Stock)).ToList(), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<IReadOnlyList<ProductDropdownItem>> DropdownAsync(string? search, CancellationToken ct)
    {
        var query = Rows();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.P.ProductName.ToLower().Contains(term) || x.P.ProductCode.ToLower().Contains(term));
        }
        return await query.OrderBy(x => x.P.ProductName).Take(50)
            .Select(x => new ProductDropdownItem(x.P.Uuid, x.P.ProductCode, x.P.ProductName,
                x.P.ProductName + " (" + x.P.ProductCode + ")", x.P.Uom, x.P.PcsPerBox,
                x.P.ProductSalesPrice, x.P.ProductPurchasePrice, x.Stock))
            .ToListAsync(ct);
    }

    public async Task<ProductDto> GetAsync(Guid id, CancellationToken ct)
    {
        var row = await Rows().FirstOrDefaultAsync(x => x.P.Uuid == id, ct);
        if (row is null) throw DomainException.NotFound("Product");
        return ToDto(row.P, row.Stock);
    }

    public async Task<ProductDto> CreateAsync(ProductSaveRequest r, CancellationToken ct)
    {
        Validate(r);
        var code = r.ProductCode!.Trim();
        if (await db.Products.AnyAsync(x => x.ProductCode == code, ct))
            throw DomainException.Conflict(ErrorCodes.DuplicateCode, $"Product code '{code}' is already used.");
        var p = new Product { Uuid = Guid.NewGuid() };
        Apply(p, r);
        db.Products.Add(p);
        // SRS 6.4: stock balance row is created with the product, in the same transaction (one SaveChanges).
        db.StockBalances.Add(new StockBalance { Uuid = Guid.NewGuid(), ProductUuid = p.Uuid, CurrentStockBalance = 0 });
        await db.SaveChangesAsync(ct);
        return ToDto(p, 0);
    }

    public async Task<ProductDto> UpdateAsync(Guid id, ProductSaveRequest r, CancellationToken ct)
    {
        Validate(r);
        var p = (await db.Products.FirstOrDefaultAsync(x => x.Uuid == id && x.Status == RecordStatus.Active, ct)).OrNotFound("Product");
        RevisionGuard.Check(db, p, r.Revision ?? Guid.Empty);
        var code = r.ProductCode!.Trim();
        if (await db.Products.AnyAsync(x => x.ProductCode == code && x.Uuid != id, ct))
            throw DomainException.Conflict(ErrorCodes.DuplicateCode, $"Product code '{code}' is already used.");
        Apply(p, r);
        await db.SaveChangesAsync(ct);
        var stock = await db.StockBalances.Where(s => s.ProductUuid == id).Select(s => s.CurrentStockBalance).FirstOrDefaultAsync(ct);
        return ToDto(p, stock);
    }

    public async Task DeleteAsync(Guid id, Guid revision, CancellationToken ct)
    {
        var p = (await db.Products.FirstOrDefaultAsync(x => x.Uuid == id && x.Status == RecordStatus.Active, ct)).OrNotFound("Product");
        RevisionGuard.Check(db, p, revision);

        var inPurchaseDraft = await (from l in db.PurchaseOrderLines
                                     join o in db.PurchaseOrders on l.OrderUuid equals o.Uuid
                                     where l.ProductUuid == id && l.Status == RecordStatus.Active
                                           && o.Status == RecordStatus.Active && o.PostingStatus == PostingStatus.Draft
                                     select l.Uuid).AnyAsync(ct);
        var inSalesDraft = await (from l in db.SalesOrderLines
                                  join o in db.SalesOrders on l.OrderUuid equals o.Uuid
                                  where l.ProductUuid == id && l.Status == RecordStatus.Active
                                        && o.Status == RecordStatus.Active && o.PostingStatus == PostingStatus.Draft
                                  select l.Uuid).AnyAsync(ct);
        if (inPurchaseDraft || inSalesDraft)
            throw DomainException.Rule(ErrorCodes.InUseByDraft, "This product is used by draft orders. Finalize, void or delete those drafts first.");

        var stock = await db.StockBalances.Where(s => s.ProductUuid == id).Select(s => s.CurrentStockBalance).FirstOrDefaultAsync(ct);
        if (stock != 0)
            throw DomainException.Rule(ErrorCodes.BusinessRule, $"This product still has {stock} pcs in stock. Adjust stock to 0 before deleting.");

        p.Status = RecordStatus.Deleted;
        await db.SaveChangesAsync(ct);
    }

    private static void Validate(ProductSaveRequest r)
    {
        var v = new Validator()
            .Required("productName", r.ProductName, "Product name", 200)
            .Required("productCode", r.ProductCode, "Product code", 50);
        v.When(r.ProductSalesPrice is null, "productSalesPrice", "Sales price is required.");
        v.When(r.ProductSalesPrice < 0, "productSalesPrice", "Sales price cannot be negative.");
        v.When(r.ProductPurchasePrice is null, "productPurchasePrice", "Purchase price is required.");
        v.When(r.ProductPurchasePrice < 0, "productPurchasePrice", "Purchase price cannot be negative.");
        v.When(r.Uom is null, "uom", "UOM is required.");
        v.When(r.Uom == Uom.Box && r.PcsPerBox is not > 0, "pcsPerBox", "Pcs per box is required and must be greater than 0 when UOM is BOX.");
        v.When(r.PcsPerBox is <= 0, "pcsPerBox", "Pcs per box must be greater than 0.");
        v.When(r.LowStockThreshold is < 0, "lowStockThreshold", "Low stock threshold cannot be negative.");
        v.ThrowIfInvalid();
    }

    private static void Apply(Product p, ProductSaveRequest r)
    {
        p.ProductName = r.ProductName!.Trim();
        p.ProductCode = r.ProductCode!.Trim();
        p.ProductSalesPrice = Domain.Rules.Money.Round(r.ProductSalesPrice!.Value);
        p.ProductPurchasePrice = Domain.Rules.Money.Round(r.ProductPurchasePrice!.Value);
        p.Uom = r.Uom!.Value;
        p.PcsPerBox = r.PcsPerBox;
        p.LowStockThreshold = r.LowStockThreshold;
    }
}
