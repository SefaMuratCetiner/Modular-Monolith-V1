using System.Text.RegularExpressions;
using ModularMonolith.Shared.Kernel.Context;

namespace ModularMonolith.Shared.Infrastructure.Tenancy;

/// <summary>
/// Scoped, set-once tenant holder. Set by the HTTP middleware from the validated token claim,
/// or by the event bus from the message being consumed.
/// </summary>
public sealed partial class TenantContext : ITenantContext
{
    private string? _tenantId;

    /// <inheritdoc />
    public bool IsResolved => _tenantId is not null;

    /// <inheritdoc />
    public string TenantId => _tenantId ?? throw new InvalidOperationException("No tenant has been resolved for the current scope.");

    /// <summary>Checks whether a value is a well-formed tenant identifier.</summary>
    /// <param name="tenantId">The candidate.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    public static bool IsValid(string? tenantId) => tenantId is not null && TenantIdPattern().IsMatch(tenantId);

    /// <summary>Sets the tenant for the current scope. A scope can never switch tenants.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    public void Set(string tenantId)
    {
        if (!IsValid(tenantId))
        {
            throw new ArgumentException($"'{tenantId}' is not a valid tenant identifier.", nameof(tenantId));
        }

        if (_tenantId is not null && !string.Equals(_tenantId, tenantId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The tenant of a scope cannot be changed once resolved.");
        }

        _tenantId = tenantId;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,62}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TenantIdPattern();
}
