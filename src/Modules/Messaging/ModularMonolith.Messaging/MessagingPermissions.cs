using ModularMonolith.Shared.Infrastructure.Security;

namespace ModularMonolith.Messaging;

/// <summary>Permissions owned by the Messaging module (platform operators only).</summary>
internal static class MessagingPermissions
{
    /// <summary>Inspect dead-lettered outbox messages of every module and tenant.</summary>
    public static Permission DeadLettersRead { get; } = new("messaging.dead-letters.read", "platform.ops", [AppRoles.Operator]);

    /// <summary>Re-queue dead-lettered outbox messages of every module and tenant.</summary>
    public static Permission DeadLettersReplay { get; } = new("messaging.dead-letters.replay", "platform.ops", [AppRoles.Operator]);

    /// <summary>Gets every Messaging permission (registered by <see cref="MessagingModule"/>).</summary>
    public static Permission[] All { get; } = [DeadLettersRead, DeadLettersReplay];
}
