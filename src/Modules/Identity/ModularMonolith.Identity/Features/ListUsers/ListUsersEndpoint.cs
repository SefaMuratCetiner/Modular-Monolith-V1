using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using ModularMonolith.Identity.Features.GetCurrentUser;
using ModularMonolith.Shared.Infrastructure.Web;
using ModularMonolith.Shared.Kernel.Cqrs;

namespace ModularMonolith.Identity.Features.ListUsers;

/// <summary><c>GET /api/v1/identity/users?page=1&amp;pageSize=50</c> (role <c>admin</c>).</summary>
internal sealed class ListUsersEndpoint(IQueryHandler<ListUsersQuery, IReadOnlyList<UserProfileView>> handler)
    : Endpoint<ListUsersRequest, IReadOnlyList<UserProfileView>>
{
    public override void Configure()
    {
        Get("identity/users");
        Version(1);
        Roles("admin");
        Options(builder => builder.WithTags("Identity").RequireRateLimiting(RateLimitPolicies.PerUser));
        Summary(summary => summary.Summary = "Lists user profiles of the caller's tenant (admin only).");
    }

    public override async Task HandleAsync(ListUsersRequest req, CancellationToken ct)
    {
        var page = Math.Max(req.Page, 1);
        var pageSize = Math.Clamp(req.PageSize, 1, 200);
        var result = await handler.HandleAsync(new ListUsersQuery(page, pageSize), ct);
        if (result.IsFailure)
        {
            await Send.ResultAsync(result.Error.ToProblem());
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}

/// <summary>Paging parameters (query string).</summary>
internal sealed class ListUsersRequest
{
    /// <summary>Gets the 1-based page number.</summary>
    [QueryParam]
    public int Page { get; init; } = 1;

    /// <summary>Gets the page size (1-200).</summary>
    [QueryParam]
    public int PageSize { get; init; } = 50;
}
