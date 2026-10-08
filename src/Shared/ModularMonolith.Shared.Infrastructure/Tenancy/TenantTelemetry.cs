namespace ModularMonolith.Shared.Infrastructure.Tenancy;

/// <summary>
/// Puts the tenant on logs (logger scope, picked up by Serilog) and on spans (tag).
/// Tenants are deliberately NOT added to metric tags to keep metric cardinality bounded.
/// </summary>
public static class TenantTelemetry
{
    /// <summary>The span tag carrying the tenant id.</summary>
    public const string TagName = "tenant.id";

    /// <summary>The log property carrying the tenant id.</summary>
    public const string LogPropertyName = "TenantId";

    /// <summary>Creates the logger scope state for a tenant.</summary>
    /// <param name="tenantId">The tenant id.</param>
    /// <returns>The scope state.</returns>
    public static IReadOnlyDictionary<string, object> LogScope(string tenantId)
        => new Dictionary<string, object>(StringComparer.Ordinal) { [LogPropertyName] = tenantId };
}
