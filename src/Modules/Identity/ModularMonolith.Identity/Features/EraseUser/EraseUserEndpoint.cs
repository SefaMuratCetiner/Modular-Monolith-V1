using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using ModularMonolith.Shared.Infrastructure.Web;
using EraseUserHandler = ModularMonolith.Shared.Kernel.Cqrs.ICommandHandler<
    ModularMonolith.Identity.Features.EraseUser.EraseUserCommand, ModularMonolith.Identity.Domain.UserProfileId>;

namespace ModularMonolith.Identity.Features.EraseUser;

/// <summary>
/// <c>DELETE /api/v1/identity/users/{subjectId}</c>: a tenant administrator erases a user's personal data
/// (e.g. an erasure request received by e-mail). Row-level security limits it to the admin's tenant.
/// </summary>
internal sealed class EraseUserEndpoint(EraseUserHandler handler) : Endpoint<EraseUserRequest>
{
    public override void Configure()
    {
        Delete("identity/users/{subjectId}");
        Version(1);
        Policies(IdentityPermissions.UsersErase.Name);
        Options(builder => builder.WithTags("Identity").RequireRateLimiting(RateLimitPolicies.PerUser));
        Description(
            builder => builder
                .Produces(StatusCodes.Status204NoContent)
                .ProducesProblems(StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict),
            clearDefaults: true);
        Summary(summary => summary.Summary = "Erases a user's personal data in the caller's tenant (admin only).");
    }

    public override async Task HandleAsync(EraseUserRequest req, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new EraseUserCommand(req.SubjectId), ct);
        if (result.IsFailure)
        {
            await Send.ResultAsync(result.Error.ToProblem());
            return;
        }

        await Send.NoContentAsync(ct);
    }
}

/// <summary>Route binding (slice-local DTO).</summary>
internal sealed class EraseUserRequest
{
    /// <summary>Gets the identity provider subject (<c>sub</c>) of the user to erase.</summary>
    public string SubjectId { get; init; } = string.Empty;
}
