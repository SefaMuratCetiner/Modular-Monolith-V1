using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using ModularMonolith.Identity.Domain;
using ModularMonolith.Shared.Infrastructure.Web;
using ModularMonolith.Shared.Kernel.Context;
using ModularMonolith.Shared.Kernel.Cqrs;

namespace ModularMonolith.Identity.Features.GetCurrentUser;

/// <summary><c>GET /api/v1/identity/users/me</c>.</summary>
internal sealed class GetCurrentUserEndpoint(
    IQueryHandler<GetCurrentUserQuery, UserProfileView> handler,
    ICurrentUser currentUser) : EndpointWithoutRequest<UserProfileView>
{
    public override void Configure()
    {
        Get("identity/users/me");
        Version(1);
        Options(builder => builder.WithTags("Identity").RequireRateLimiting(RateLimitPolicies.PerUser));
        Summary(summary => summary.Summary = "Gets the authenticated user's profile.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (currentUser.UserId is not { } subjectId)
        {
            await Send.ResultAsync(IdentityErrors.NotAuthenticated.ToProblem());
            return;
        }

        var result = await handler.HandleAsync(new GetCurrentUserQuery(subjectId), ct);
        if (result.IsFailure)
        {
            await Send.ResultAsync(result.Error.ToProblem());
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
