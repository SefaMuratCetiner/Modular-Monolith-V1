using ModularMonolith.Shared.Infrastructure.Security;

namespace ModularMonolith.Privacy;

/// <summary>Permissions owned by the Privacy module.</summary>
internal static class PrivacyPermissions
{
    /// <summary>Export one's own personal data from every module.</summary>
    public static Permission PersonalDataExport { get; } = new("privacy.personal-data.export", "privacy.read", [AppRoles.User]);

    /// <summary>Gets every Privacy permission (registered by <see cref="PrivacyModule"/>).</summary>
    public static Permission[] All { get; } = [PersonalDataExport];
}
