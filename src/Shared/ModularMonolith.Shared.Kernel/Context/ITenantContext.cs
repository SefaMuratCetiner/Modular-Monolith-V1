namespace ModularMonolith.Shared.Kernel.Context;

/// <summary>
/// The tenant of the current scope (HTTP request or background message).
/// </summary>
public interface ITenantContext
{
    /// <summary>Gets a value indicating whether a tenant has been resolved for this scope.</summary>
    bool IsResolved { get; }

    /// <summary>Gets the tenant identifier. Throws when no tenant is resolved (fail closed).</summary>
    string TenantId { get; }
}
