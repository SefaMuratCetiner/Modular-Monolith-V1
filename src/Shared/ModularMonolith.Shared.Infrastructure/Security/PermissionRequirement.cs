using Microsoft.AspNetCore.Authorization;

namespace ModularMonolith.Shared.Infrastructure.Security;

/// <summary>The authorization requirement behind every permission policy.</summary>
/// <param name="Permission">The required permission.</param>
internal sealed record PermissionRequirement(Permission Permission) : IAuthorizationRequirement;
