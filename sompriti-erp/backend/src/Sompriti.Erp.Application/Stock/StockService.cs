using Microsoft.EntityFrameworkCore;
using Sompriti.Erp.Application.Common;
using Sompriti.Erp.Domain.Common;
using Sompriti.Erp.Domain.Entities;
using Sompriti.Erp.Domain.Enums;

namespace Sompriti.Erp.Application.Stock;

public sealed record StockBalanceDto(Guid ProductUuid, string ProductCode, string ProductName, Uom Uom, int? PcsPerBox,
    int CurrentStockBalance, int? LowStockThreshold, bool IsLowStock, DateTimeOffset UpdatedDate);

public sealed record StockLedgerDto(Guid Uuid, Guid ProductUuid, string ProductCode, string ProductName,
    MovementType MovementType, int QuantityChange, int BalanceAfter, ReferenceType ReferenceType, Guid ReferenceUuid,
    string ReferenceNumber, DateTimeOffset CreatedDate, string CreatedByUserName);

public sealed record StockAdjustmentDto(Guid Uuid, string AdjustmentNumber, Guid ProductUuid, string ProductCode, string ProductName,
    AdjustmentType AdjustmentType, int QuantityPcs, AdjustmentReason Reason, string? Note, DateOnly AdjustmentDate,
    DateTimeOffset CreatedDate, string CreatedByUserName);

public sealed record StockAdjustmentRequest(Guid? ProductUuid, AdjustmentType? AdjustmentType, int? QuantityPcs,
    AdjustmentReason? Reason, string? Note, DateOnly? AdjustmentDate);

public sealed record StockShortage(Guid ProductUuid, string ProductCode, string ProductName, int Available, int Required);

public sealed record StockLedgerQuery : PageQuery
{
    public Guid? ProductUuid { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
}

public sealed class StockService(IAppDbContext db, ICurrentUser user, TimeProvider clock)
{
    // ------------------------------------------------------------------ queries

    public async Task<PagedResult<StockBalanceDto>> BalancesAsync(PageQuery q, Guid? productUuid, bool lowStockOnly, CancellationToken ct)
    {
        var query = from s in db.StockBalances.AsNoTracking()
                    join p in db.Products.AsNoTracking() on s.ProductUuid equals p.Uuid
                    where p.Status == RecordStatus.Active
                    select new { s, p };
        if (productUuid is { } pid) query = query.Where(x => x.p.Uuid == pid);
        if (q.Term is { } t)
        {
            var term = t.ToLower();
            query = query.Where(x => x.p.ProductName.ToLower().Contains(term) || x.p.ProductCode.ToLower().Contains(term));
        }
        if (lowStockOnly) query = query.Where(x => x.p.LowStockThreshold != null && x.s.CurrentStockBalance <= x.p.LowStockThreshold);

        query = q.Sort?.TrimStart('-') switch
        {
            "currentStockBalance" => q.Sort.StartsWith('-') ? query.OrderByDescending(x => x.s.CurrentStockBalance) : query.OrderBy(x => x.s.CurrentStockBalance),
            "productCode" => q.Sort.StartsWith('-') ? query.OrderByDescending(x => x.p.ProductCode) : query.OrderBy(x => x.p.ProductCode),
            _ => q.Sort?.StartsWith('-') == true ? query.OrderByDescending(x => x.p.ProductName) : query.OrderBy(x => x.p.ProductName)
        };

        return await query.ToPagedAsync(q, x => new StockBalanceDto(x.p.Uuid, x.p.ProductCode, x.p.ProductName, x.p.Uom, x.p.PcsPerBox,
            x.s.CurrentStockBalance, x.p.LowStockThreshold,
            x.p.LowStockThreshold != null && x.s.CurrentStockBalance <= x.p.LowStockThreshold, x.s.UpdatedDate), ct);
    }

    public async Task<PagedResult<StockLedgerDto>> LedgerAsync(StockLedgerQuery q, CancellationToken ct)
    {
        var query = from l in db.StockLedgers.AsNoTracking()
                    join p in db.Products.AsNoTracking() on l.ProductUuid equals p.Uuid
                    select new { l, p };
        // MANAGER sees sales movements only (SRS 5.1)
        if (user.Role == Role.Manager)
            query = query.Where(x => x.l.MovementType == MovementType.SalesFinal || x.l.MovementType == MovementType.SalesVoid);
        if (q.ProductUuid is { } pid) query = query.Where(x => x.l.ProductUuid == pid);
        if (q.FromDate is { } from)
        {
            var f = BusinessClock.StartOfDayUtc(from);
            query = query.Where(x => x.l.CreatedDate >= f);
        }
        if (q.ToDate is { } to)
        {
            var e = BusinessClock.StartOfDayUtc(to.AddDays(1));
            query = query.Where(x => x.l.CreatedDate < e);
        }
        if (q.Term is { } t)
        {
            var term = t.ToLower();
            query = query.Where(x => x.p.ProductName.ToLower().Contains(term) || x.p.ProductCode.ToLower().Contains(term) || x.l.ReferenceNumber.Contains(term));
        }
        return await query.OrderByDescending(x => x.l.CreatedDate)
            .ToPagedAsync(q, x => new StockLedgerDto(x.l.Uuid, x.l.ProductUuid, x.p.ProductCode, x.p.ProductName, x.l.MovementType,
                x.l.QuantityChange, x.l.BalanceAfter, x.l.ReferenceType, x.l.ReferenceUuid, x.l.ReferenceNumber,
                x.l.CreatedDate, x.l.CreatedByUserName), ct);
    }

    public async Task<PagedResult<StockAdjustmentDto>> AdjustmentsAsync(PageQuery q, Guid? productUuid, CancellationToken ct)
    {
        var query = from a in db.StockAdjustments.AsNoTracking()
                    join p in db.Products.AsNoTracking() on a.ProductUuid equals p.Uuid
                    where a.Status == RecordStatus.Active
                    select new { a, p };
        if (productUuid is { } pid) query = query.Where(x => x.a.ProductUuid == pid);
        if (q.Term is { } t)
        {
            var term = t.ToLower();
            query = query.Where(x => x.p.ProductName.ToLower().Contains(term) || x.p.ProductCode.ToLower().Contains(term) || x.a.AdjustmentNumber!.Contains(term));
        }
        return await query.OrderByDescending(x => x.a.CreatedDate)
            .ToPagedAsync(q, x => new StockAdjustmentDto(x.a.Uuid, x.a.AdjustmentNumber!, x.a.ProductUuid, x.p.ProductCode, x.p.ProductName,
                x.a.AdjustmentType, x.a.QuantityPcs, x.a.Reason, x.a.Note, x.a.AdjustmentDate, x.a.CreatedDate, x.a.CreatedByUserName), ct);
    }

    // ------------------------------------------------------------------ commands

    public async Task<StockAdjustmentDto> CreateAdjustmentAsync(StockAdjustmentRequest r, CancellationToken ct)
    {
        var today = BusinessClock.Today(clock.GetUtcNow());
        var v = new Validator().Required("productUuid", r.ProductUuid, "Product");
        v.When(r.AdjustmentType is null, "adjustmentType", "Adjustment type is required.");
        v.When(r.QuantityPcs is not > 0, "quantityPcs", "Quantity must be greater than 0.");
        v.When(r.Reason is null, "reason", "Reason is required.");
        v.When(r.Reason == AdjustmentReason.Other && string.IsNullOrWhiteSpace(r.Note), "note", "Note is required when reason is OTHER.");
        v.MaxLength("note", r.Note, "Note", 500);
        v.When(r.AdjustmentDate > today, "adjustmentDate", "Adjustment date cannot be in the future.");
        v.ThrowIfInvalid();

        var product = await db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Uuid == r.ProductUuid && p.Status == RecordStatus.Active, ct);
        product.OrNotFound("Product");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var adj = new StockAdjustment
        {
            Uuid = Guid.NewGuid(),
            ProductUuid = r.ProductUuid!.Value,
            AdjustmentType = r.AdjustmentType!.Value,
            QuantityPcs = r.QuantityPcs!.Value,
            Reason = r.Reason!.Value,
            Note = Validator.Clean(r.Note),
            AdjustmentDate = r.AdjustmentDate ?? today,
        };
        db.StockAdjustments.Add(adj);
        await db.SaveChangesAsync(ct); // generates adjustment_number

        var change = adj.AdjustmentType == AdjustmentType.Increase ? adj.QuantityPcs : -adj.QuantityPcs;
        await ApplyMovementsAsync(new Dictionary<Guid, int> { [adj.ProductUuid] = change },
            adj.AdjustmentType == AdjustmentType.Increase ? MovementType.AdjustmentIn : MovementType.AdjustmentOut,
            ReferenceType.StockAdjustment, adj.Uuid, adj.AdjustmentNumber!, ErrorCodes.NegativeStock,
            "This adjustment would make stock negative.", ct);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new StockAdjustmentDto(adj.Uuid, adj.AdjustmentNumber!, adj.ProductUuid, product!.ProductCode, product.ProductName,
            adj.AdjustmentType, adj.QuantityPcs, adj.Reason, adj.Note, adj.AdjustmentDate, adj.CreatedDate, adj.CreatedByUserName);
    }

    /// <summary>
    /// Applies stock changes inside the caller's transaction (SRS 6.5.5):
    /// locks the balance rows in product_uuid order, validates no negative balance, updates balances
    /// and adds ledger rows. The caller must call SaveChangesAsync and commit.
    /// </summary>
    public async Task ApplyMovementsAsync(IReadOnlyDictionary<Guid, int> changes, MovementType movement,
        ReferenceType referenceType, Guid referenceUuid, string referenceNumber,
        string shortageCode, string shortageMessage, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Stock movements must run inside a database transaction.");

        var ids = changes.Where(c => c.Value != 0).Select(c => c.Key).OrderBy(id => id).ToArray();
        if (ids.Length == 0) return;

        // Lock rows in a consistent order to avoid deadlocks.
        var balances = await db.StockBalances
            .FromSqlRaw("SELECT * FROM stock_balance WHERE product_uuid = ANY({0}) ORDER BY product_uuid FOR UPDATE", ids)
            .ToListAsync(ct);

        // Create missing balance rows defensively (should already exist from product creation).
        foreach (var id in ids.Where(id => balances.All(b => b.ProductUuid != id)))
        {
            var b = new StockBalance { Uuid = Guid.NewGuid(), ProductUuid = id, CurrentStockBalance = 0 };
            db.StockBalances.Add(b);
            balances.Add(b);
        }

        var shortages = balances
            .Where(b => b.CurrentStockBalance + changes[b.ProductUuid] < 0)
            .Select(b => (b.ProductUuid, Available: b.CurrentStockBalance, Required: -changes[b.ProductUuid]))
            .ToList();
        if (shortages.Count > 0)
        {
            var shortIds = shortages.Select(s => s.ProductUuid).ToList();
            var products = await db.Products.AsNoTracking().Where(p => shortIds.Contains(p.Uuid))
                .ToDictionaryAsync(p => p.Uuid, ct);
            var details = shortages.Select(s => new StockShortage(s.ProductUuid,
                products.GetValueOrDefault(s.ProductUuid)?.ProductCode ?? "", products.GetValueOrDefault(s.ProductUuid)?.ProductName ?? "",
                s.Available, s.Required)).ToList();
            var text = string.Join("; ", details.Select(d => $"{d.ProductName} ({d.ProductCode}): available {d.Available}, needed {d.Required}"));
            throw DomainException.Rule(shortageCode, $"{shortageMessage} {text}", details);
        }

        var now = clock.GetUtcNow();
        foreach (var b in balances.OrderBy(b => b.ProductUuid))
        {
            var change = changes[b.ProductUuid];
            b.CurrentStockBalance += change;
            db.StockLedgers.Add(new StockLedger
            {
                Uuid = Guid.NewGuid(),
                ProductUuid = b.ProductUuid,
                MovementType = movement,
                QuantityChange = change,
                BalanceAfter = b.CurrentStockBalance,
                ReferenceType = referenceType,
                ReferenceUuid = referenceUuid,
                ReferenceNumber = referenceNumber,
                CreatedDate = now,
                CreatedByUserUuid = user.IsAuthenticated ? user.UserUuid : AppUser.SystemUserUuid,
                CreatedByUserName = user.IsAuthenticated ? user.UserName : AppUser.SystemUserName,
            });
        }
    }

    /// <summary>Current balances for the given products (no locking), used for line-level stock warnings.</summary>
    public async Task<Dictionary<Guid, int>> CurrentBalancesAsync(IEnumerable<Guid> productIds, CancellationToken ct)
    {
        var ids = productIds.Distinct().ToList();
        return await db.StockBalances.AsNoTracking().Where(s => ids.Contains(s.ProductUuid))
            .ToDictionaryAsync(s => s.ProductUuid, s => s.CurrentStockBalance, ct);
    }
}
