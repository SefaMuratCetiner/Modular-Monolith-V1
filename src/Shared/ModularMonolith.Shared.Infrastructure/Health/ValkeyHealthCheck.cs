using Microsoft.Extensions.Diagnostics.HealthChecks;
using ModularMonolith.Shared.Infrastructure.Caching;
using StackExchange.Redis;

namespace ModularMonolith.Shared.Infrastructure.Health;

/// <summary>
/// Readiness check for Valkey. Reported as degraded (not unhealthy) because the cache and
/// idempotency store are designed to fail open.
/// </summary>
internal sealed class ValkeyHealthCheck(ValkeyConnection valkey) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var latency = await valkey.Multiplexer.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy($"Ping {latency.TotalMilliseconds:F1} ms");
        }
        catch (RedisException exception)
        {
            return HealthCheckResult.Degraded("Valkey is unreachable.", exception);
        }
    }
}
