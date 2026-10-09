namespace ModularMonolith.Identity;

/// <summary>
/// Erasure of the identity-provider account (section <c>Modules:Identity:AccountErasure</c>).
/// When a user is erased, Identity calls the Keycloak admin REST API as a service account
/// (client credentials) that holds only <c>realm-management/manage-users</c>.
/// </summary>
internal sealed class AccountErasureOptions
{
    /// <summary>Gets or sets a value indicating whether the Keycloak account is erased too. On by default.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the confidential service-account client.</summary>
    public string ClientId { get; set; } = "modular-monolith-admin";

    /// <summary>Gets or sets the client secret. Comes from the secret store, never from appsettings.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Gets or sets whether the account is deleted (default) or disabled and logged out.</summary>
    public AccountErasureMode Mode { get; set; } = AccountErasureMode.Delete;
}

/// <summary>What erasure does to the Keycloak account.</summary>
internal enum AccountErasureMode
{
    /// <summary>Delete the account: credentials, OTP secret, attributes and sessions are gone (GDPR erasure).</summary>
    Delete,

    /// <summary>Disable the account and end its sessions; Keycloak keeps the account data (e.g. for a legal hold).</summary>
    Disable,
}
