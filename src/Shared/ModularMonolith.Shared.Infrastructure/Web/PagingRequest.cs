using FastEndpoints;
using ModularMonolith.Shared.Kernel.Paging;

namespace ModularMonolith.Shared.Infrastructure.Web;

/// <summary>
/// Query-string paging parameters shared by list endpoints: <c>?page=1&amp;pageSize=20</c>.
/// Values are validated by <see cref="PageRequest.Create"/> (out-of-range values return 400).
/// </summary>
public sealed class PagingRequest
{
    /// <summary>Gets the 1-based page number.</summary>
    [QueryParam]
    public int Page { get; init; } = 1;

    /// <summary>Gets the page size (1 to <see cref="PageRequest.MaxPageSize"/>).</summary>
    [QueryParam]
    public int PageSize { get; init; } = PageRequest.DefaultPageSize;
}
