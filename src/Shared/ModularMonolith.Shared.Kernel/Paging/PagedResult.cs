namespace ModularMonolith.Shared.Kernel.Paging;

/// <summary>
/// One page of a list plus the metadata clients need to navigate it.
/// Serialized as <c>{ items, page, pageSize, totalCount, totalPages, hasNextPage, hasPreviousPage }</c>.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="Items">The items on this page.</param>
/// <param name="Page">The 1-based page number.</param>
/// <param name="PageSize">The requested page size.</param>
/// <param name="TotalCount">The total number of items across all pages.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount)
{
    /// <summary>Gets the total number of pages.</summary>
    public long TotalPages => PageSize <= 0 ? 0 : (TotalCount + PageSize - 1) / PageSize;

    /// <summary>Gets a value indicating whether a later page exists.</summary>
    public bool HasNextPage => (long)Page * PageSize < TotalCount;

    /// <summary>Gets a value indicating whether an earlier page exists.</summary>
    public bool HasPreviousPage => Page > 1;
}

/// <summary>Factory helpers for <see cref="PagedResult{T}"/>.</summary>
public static class PagedResult
{
    /// <summary>Creates a page from a request, its items and the total count.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="request">The page request.</param>
    /// <param name="items">The items on this page.</param>
    /// <param name="totalCount">The total number of items.</param>
    /// <returns>The page.</returns>
    public static PagedResult<T> Create<T>(PageRequest request, IReadOnlyList<T> items, long totalCount)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new PagedResult<T>(items, request.Page, request.PageSize, totalCount);
    }
}
