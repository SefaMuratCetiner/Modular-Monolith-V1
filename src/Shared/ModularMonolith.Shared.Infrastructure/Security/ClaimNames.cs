namespace ModularMonolith.Shared.Infrastructure.Security;

/// <summary>
/// Token claim names used across the application. Inbound claim mapping is disabled, so these
/// are the raw OIDC/JWT names issued by Keycloak (see <c>deploy/keycloak/realm-export.json</c>).
/// </summary>
public static class ClaimNames
{
    /// <summary>The subject (stable user id).</summary>
    public const string Subject = "sub";

    /// <summary>The tenant the user belongs to (Keycloak user-attribute mapper).</summary>
    public const string TenantId = "tenant_id";

    /// <summary>The flat list of realm roles (Keycloak realm-role mapper).</summary>
    public const string Roles = "roles";

    /// <summary>The display user name.</summary>
    public const string PreferredUserName = "preferred_username";
}
