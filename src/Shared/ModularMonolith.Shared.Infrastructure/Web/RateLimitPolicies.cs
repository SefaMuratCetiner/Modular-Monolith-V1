namespace ModularMonolith.Shared.Infrastructure.Web;

/// <summary>Rate limiter policy names (policies are configured by the host).</summary>
public static class RateLimitPolicies
{
    /// <summary>Fixed window partitioned per tenant and user (falls back to client IP).</summary>
    public const string PerUser = "per-user";
}
