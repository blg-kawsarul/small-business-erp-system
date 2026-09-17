using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Sompriti.Erp.Domain.Common;
using Sompriti.Erp.Domain.Entities;

namespace Sompriti.Erp.Application.Common;

public record PageQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    /// <summary>Property name, prefix with '-' for descending. Example: "-createdDate".</summary>
    public string? Sort { get; init; }

    public int SafePage => Page < 1 ? 1 : Page;
    public int SafePageSize => PageSize is < 1 ? 20 : Math.Min(PageSize, 200);
    public string? Term => string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record DropdownItem(Guid Uuid, string Code, string Name, string Label);

public sealed record RevisionRequest(Guid Revision);

public static class QueryExtensions
{
    public static async Task<PagedResult<TOut>> ToPagedAsync<T, TOut>(
        this IQueryable<T> query, PageQuery page, Expression<Func<T, TOut>> selector, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page.SafePage - 1) * page.SafePageSize)
                               .Take(page.SafePageSize)
                               .Select(selector)
                               .ToListAsync(ct);
        return new PagedResult<TOut>(items, page.SafePage, page.SafePageSize, total);
    }

    /// <summary>Applies a whitelisted sort. Unknown sort keys fall back to the default.</summary>
    public static IQueryable<T> ApplySort<T>(this IQueryable<T> query, string? sort,
        IReadOnlyDictionary<string, Expression<Func<T, object?>>> map,
        Func<IQueryable<T>, IOrderedQueryable<T>> defaultSort)
    {
        if (string.IsNullOrWhiteSpace(sort)) return defaultSort(query);
        var desc = sort.StartsWith('-');
        var key = sort.TrimStart('-', '+');
        var match = map.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));
        if (match.Value is null) return defaultSort(query);
        return desc ? query.OrderByDescending(match.Value) : query.OrderBy(match.Value);
    }

    public static T OrNotFound<T>(this T? entity, string what) where T : class =>
        entity ?? throw DomainException.NotFound(what);
}

public static class RevisionGuard
{
    /// <summary>Throws 409 if the submitted revision differs, then arms the database-level concurrency check.</summary>
    public static void Check(IAppDbContext db, AuditedEntity entity, Guid submitted)
    {
        if (submitted == Guid.Empty || entity.Revision != submitted) throw DomainException.RevisionConflict();
        db.SetOriginalRevision(entity, submitted);
    }
}
