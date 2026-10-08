namespace ModularMonolith.Shared.Infrastructure.Tenancy;

/// <summary>
/// Tenancy settings (section <c>Tenancy</c>).
/// </summary>
public sealed class TenancyOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Tenancy";

    /// <summary>Gets or sets the token claim carrying the tenant identifier.</summary>
    public string ClaimType { get; set; } = "tenant_id";

    /// <summary>
    /// Gets or sets a value indicating whether requests without a tenant claim fall back to
    /// <see cref="DefaultTenant"/>. Development only; never enable in production.
    /// </summary>
    public bool AllowDefaultTenant { get; set; }

    /// <summary>Gets or sets the development fallback tenant.</summary>
    public string DefaultTenant { get; set; } = "default";

    /// <summary>
    /// Gets or sets the root domain (e.g. <c>example.com</c>). When set, a subdomain such as
    /// <c>acme.example.com</c> must match the token's tenant claim; it is never trusted on its own.
    /// </summary>
    public string? RootDomain { get; set; }
}
