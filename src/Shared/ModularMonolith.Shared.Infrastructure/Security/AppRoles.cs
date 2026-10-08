namespace ModularMonolith.Shared.Infrastructure.Security;

/// <summary>
/// Realm roles the application authorizes against (and the policy names that require them).
/// </summary>
public static class AppRoles
{
    /// <summary>Tenant administrator: manages the users of their own tenant.</summary>
    public const string Admin = "admin";

    /// <summary>
    /// Platform operator: cross-tenant operations such as the Hangfire dashboard and outbox
    /// dead-letter replay. Never grant this to tenant users.
    /// </summary>
    public const string Operator = "operator";
}
