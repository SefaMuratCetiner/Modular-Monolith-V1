namespace ModularMonolith.Host.Configuration;

/// <summary>
/// OpenID Connect / JWT settings (section <c>Authentication</c>).
/// </summary>
internal sealed class IdentityProviderOptions
{
    public const string SectionName = "Authentication";

    /// <summary>Gets or sets the realm URL, e.g. <c>https://id.example.com/realms/modular-monolith</c>. Required.</summary>
    public string? Authority { get; set; }

    /// <summary>Gets or sets the audience access tokens must contain.</summary>
    public string Audience { get; set; } = "modular-monolith-api";

    /// <summary>Gets or sets the public (PKCE) client used for browser sign-in.</summary>
    public string WebClientId { get; set; } = "modular-monolith-web";

    /// <summary>Gets or sets a value indicating whether the authority must use HTTPS. Only disable for local development.</summary>
    public bool RequireHttpsMetadata { get; set; } = true;
}
