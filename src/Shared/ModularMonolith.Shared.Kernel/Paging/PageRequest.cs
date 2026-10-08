using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Shared.Kernel.Paging;

/// <summary>
/// A validated, 1-based page request. Every list query takes one, so no list is ever unbounded.
/// </summary>
public sealed record PageRequest
{
    /// <summary>The page size used when the caller does not specify one.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>The largest page size a caller may request.</summary>
    public const int MaxPageSize = 100;

    private PageRequest(int page, int pageSize)
    {
        Page = page;
        PageSize = pageSize;
    }

    /// <summary>Gets the 1-based page number.</summary>
    public int Page { get; }

    /// <summary>Gets the number of items per page.</summary>
    public int PageSize { get; }

    /// <summary>Gets the number of items to skip.</summary>
    public long Offset => (Page - 1L) * PageSize;

    /// <summary>Validates and creates a page request.</summary>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="pageSize">The page size (1 to <see cref="MaxPageSize"/>).</param>
    /// <returns>The page request, or a validation error.</returns>
    public static Result<PageRequest> Create(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (page < 1)
        {
            errors["page"] = ["Page must be 1 or greater."];
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            errors["pageSize"] = [$"Page size must be between 1 and {MaxPageSize}."];
        }

        return errors.Count == 0
            ? Result.Success(new PageRequest(page, pageSize))
            : Result.Failure<PageRequest>(new ValidationError(errors));
    }
}
