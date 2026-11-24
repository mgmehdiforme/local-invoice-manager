namespace LocalInvoiceManager.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public record PageQuery
{
    // Nullable so minimal-API [AsParameters] treats them as optional query parameters.
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public string? Search { get; init; }

    public int SafePage => Math.Max(1, Page ?? 1);
    public int SafePageSize => Math.Clamp(PageSize ?? 50, 1, 500);
}

public static class PagingExtensions
{
    public static PagedResult<T> ToPaged<T>(this IEnumerable<T> source, PageQuery q)
    {
        var list = source as IList<T> ?? source.ToList();
        var items = list.Skip((q.SafePage - 1) * q.SafePageSize).Take(q.SafePageSize).ToList();
        return new PagedResult<T>(items, q.SafePage, q.SafePageSize, list.Count);
    }
}
