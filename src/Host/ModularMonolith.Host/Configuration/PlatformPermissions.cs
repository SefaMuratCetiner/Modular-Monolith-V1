using ModularMonolith.Shared.Infrastructure.Security;

namespace ModularMonolith.Host.Configuration;

/// <summary>Permissions of the host's own endpoints (operations and privacy).</summary>
internal static class PlatformPermissions
{
    /// <summary>Inspect dead-lettered outbox messages of every tenant.</summary>
    public static Permission OutboxRead { get; } = new("platform.outbox.read", "platform.ops", [AppRoles.Operator]);

    /// <summary>Re-queue dead-lettered outbox messages of every tenant.</summary>
    public static Permission OutboxReplay { get; } = new("platform.outbox.replay", "platform.ops", [AppRoles.Operator]);

    /// <summary>Export one's own personal data from every module.</summary>
    public static Permission PersonalDataExport { get; } = new("privacy.personal-data.export", "identity.read", [AppRoles.User]);

    /// <summary>Gets every host permission.</summary>
    public static Permission[] All { get; } = [OutboxRead, OutboxReplay, PersonalDataExport];
}
