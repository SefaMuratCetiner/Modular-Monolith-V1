using ModularMonolith.Shared.Infrastructure.Security;

namespace ModularMonolith.Identity;

/// <summary>
/// Permissions owned by the Identity module. Each one needs a realm role <b>and</b> an OAuth scope
/// (see <see cref="Permission"/>).
/// </summary>
internal static class IdentityPermissions
{
    /// <summary>Read one's own profile.</summary>
    public static Permission ProfileRead { get; } = new("identity.profile.read", "identity.read", [AppRoles.User]);

    /// <summary>Register or erase one's own profile.</summary>
    public static Permission ProfileWrite { get; } = new("identity.profile.write", "identity.write", [AppRoles.User]);

    /// <summary>List the profiles of the caller's tenant.</summary>
    public static Permission UsersRead { get; } = new("identity.users.read", "identity.read", [AppRoles.Admin]);

    /// <summary>Erase another user's personal data in the caller's tenant (right to erasure on request).</summary>
    public static Permission UsersErase { get; } = new("identity.users.erase", "identity.write", [AppRoles.Admin]);

    /// <summary>Gets every Identity permission (registered by <see cref="IdentityModule"/>).</summary>
    public static Permission[] All { get; } = [ProfileRead, ProfileWrite, UsersRead, UsersErase];
}
