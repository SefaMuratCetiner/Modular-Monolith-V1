namespace ModularMonolith.Identity;

/// <summary>
/// Identity settings (section <c>Modules:Identity</c>).
/// </summary>
internal sealed class IdentityOptions
{
    public const string SectionName = "Modules:Identity";

    /// <summary>Gets or sets the Keycloak realm URL. Falls back to <c>Authentication:Authority</c>.</summary>
    public string? Authority { get; set; }
}
