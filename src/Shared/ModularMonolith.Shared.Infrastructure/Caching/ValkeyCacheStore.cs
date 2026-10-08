using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModularMonolith.Shared.Infrastructure.Observability;
using ModularMonolith.Shared.Infrastructure.Serialization;
using ModularMonolith.Shared.Kernel.Context;
using StackExchange.Redis;

namespace ModularMonolith.Shared.Infrastructure.Caching;

/// <summary>
/// Valkey implementation of <see cref="ICacheStore"/> (Redis protocol via StackExchange.Redis).
/// The cache is best-effort: if Valkey is unavailable the source is queried directly.
/// </summary>
internal sealed class ValkeyCacheStore(
    IConnectionMultiplexer connection,
    ITenantContext tenantContext,
    ILogger<ValkeyCacheStore> logger) : ICacheStore
{
    public async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(factory);
        var redisKey = TenantKey(key);
        try
        {
            var cached = await connection.GetDatabase().StringGetAsync(redisKey);
            if (cached.HasValue)
            {
                var value = JsonSerializer.Deserialize<T>(cached.ToString(), JsonDefaults.Options);
                if (value is not null)
                {
                    return value;
                }
            }
        }
        catch (RedisException exception)
        {
            logger.CacheUnavailable(exception, redisKey);
            return await factory(cancellationToken);
        }

        var created = await factory(cancellationToken);
        try
        {
            await connection.GetDatabase().StringSetAsync(redisKey, JsonSerializer.Serialize(created, JsonDefaults.Options), timeToLive);
        }
        catch (RedisException exception)
        {
            logger.CacheUnavailable(exception, redisKey);
        }

        return created;
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        var redisKey = TenantKey(key);
        try
        {
            await connection.GetDatabase().KeyDeleteAsync(redisKey);
        }
        catch (RedisException exception)
        {
            logger.CacheUnavailable(exception, redisKey);
        }
    }

    public async Task<T> GetOrCreateInRegionAsync<T>(string region, string key, Func<CancellationToken, Task<T>> factory, TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(factory);
        long version;
        try
        {
            var stored = await connection.GetDatabase().StringGetAsync(RegionVersionKey(region));
            version = stored.HasValue && long.TryParse(stored.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        }
        catch (RedisException exception)
        {
            logger.CacheUnavailable(exception, RegionVersionKey(region));
            return await factory(cancellationToken);
        }

        return await GetOrCreateAsync($"{region}:v{version}:{key}", factory, timeToLive, cancellationToken);
    }

    public async Task InvalidateRegionAsync(string region, CancellationToken cancellationToken)
    {
        var versionKey = RegionVersionKey(region);
        try
        {
            await connection.GetDatabase().StringIncrementAsync(versionKey);
        }
        catch (RedisException exception)
        {
            logger.CacheUnavailable(exception, versionKey);
        }
    }

    private string TenantKey(string key) => $"cache:{tenantContext.TenantId}:{key}";

    private string RegionVersionKey(string region) => TenantKey($"{region}:version");
}
