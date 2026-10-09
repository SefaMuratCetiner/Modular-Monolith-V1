using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using ModularMonolith.Identity.Features.GetCurrentUser;
using ModularMonolith.Shared.Infrastructure.Web;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Paging;

namespace ModularMonolith.Identity.Features.ListUsers;

/// <summary><c>GET /api/v1/identity/users?page=1&amp;pageSize=20</c> (role <c>admin</c>).</summary>
internal sealed class ListUsersEndpoint(IQueryHandler<ListUsersQuery, PagedResult<UserProfileView>> handler)
    : Endpoint<PagingRequest, PagedResult<UserProfileView>>
{
    public override void Configure()
    {
        Get("identity/users");
        Version(1);
        Policies(IdentityPermissions.UsersRead.Name);
        Options(builder => builder.WithTags("Identity").RequireRateLimiting(RateLimitPolicies.PerUser));
        Description(
            builder => builder
                .Produces<PagedResult<UserProfileView>>(StatusCodes.Status200OK)
                .ProducesProblems(StatusCodes.Status400BadRequest, StatusCodes.Status403Forbidden),
            clearDefaults: true);
        Summary(summary => summary.Summary = "Lists one page of user profiles of the caller's tenant (admin only).");
    }

    public override async Task HandleAsync(PagingRequest req, CancellationToken ct)
    {
        var paging = PageRequest.Create(req.Page, req.PageSize);
        if (paging.IsFailure)
        {
            await Send.ResultAsync(paging.Error.ToProblem());
            return;
        }

        var result = await handler.HandleAsync(new ListUsersQuery(paging.Value), ct);
        if (result.IsFailure)
        {
            await Send.ResultAsync(result.Error.ToProblem());
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
