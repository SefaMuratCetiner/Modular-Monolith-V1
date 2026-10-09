using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace ModularMonolith.Shared.Infrastructure.Security;

/// <summary>
/// Grants a <see cref="PermissionRequirement"/> when the caller has one of the permission's roles and the
/// access token carries its scope. A failed check yields 403 (problem details) through the JWT handler.
/// </summary>
internal sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var permission = requirement.Permission;
        if (permission.Roles.Any(context.User.IsInRole) && HasScope(context.User, permission.Scope))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static bool HasScope(ClaimsPrincipal user, string scope)
        => user.FindAll(claim => claim.Type is ClaimNames.Scope or ClaimNames.ScopeAlternative)
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(scope, StringComparer.Ordinal);
}
