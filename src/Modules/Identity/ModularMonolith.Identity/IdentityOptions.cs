namespace ModularMonolith.Identity;

/// <summary>
/// Identity settings (section <c>Modules:Identity</c>).
/// </summary>
internal sealed class IdentityOptions
{
    public const string SectionName = "Modules:Identity";

    private const string RealmsSegment = "/realms/";

    /// <summary>Gets or sets the Keycloak realm URL. Falls back to <c>Authentication:Authority</c>.</summary>
    public string? Authority { get; set; }

    /// <summary>Gets or sets what happens to the Keycloak account when a user's personal data is erased.</summary>
    public AccountErasureOptions AccountErasure { get; set; } = new();

    /// <summary>Gets the Keycloak server root (the authority up to <c>/realms/</c>), used by the admin API.</summary>
    public Uri? ServerUrl => SplitAuthority() is { } parts ? new Uri(parts.Server) : null;

    /// <summary>Gets the realm name (the segment after <c>/realms/</c>).</summary>
    public string? Realm => SplitAuthority()?.Realm;

    private (string Server, string Realm)? SplitAuthority()
    {
        var authority = Authority?.TrimEnd('/');
        var index = authority?.IndexOf(RealmsSegment, StringComparison.Ordinal) ?? -1;
        if (authority is null || index <= 0)
        {
            return null;
        }

        var realm = authority[(index + RealmsSegment.Length)..];
        return realm.Length == 0 || realm.Contains('/', StringComparison.Ordinal) ? null : (authority[..index] + "/", realm);
    }
}
