using System.Diagnostics.Metrics;

namespace ModularMonolith.Identity;

/// <summary>
/// Identity business metrics, recorded only after the transaction commits.
/// The meter name matches the <c>ModularMonolith.*</c> subscription in ServiceDefaults.
/// </summary>
internal static class IdentityTelemetry
{
    public const string MeterName = "ModularMonolith.Identity";

    private static readonly Meter Meter = new(MeterName);

    public static Counter<long> UsersRegistered { get; } =
        Meter.CreateCounter<long>("modmono.identity.users.registered", description: "User profiles registered (committed).");
}
