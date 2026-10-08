using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModularMonolith.Shared.Infrastructure.Observability;
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
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(factory);
        var redisKey = TenantKey(key);
        try
        {
            var cached = await connection.GetDatabase().StringGetAsync(redisKey);
            if (cached.HasValue)
            {
                var value = JsonSerializer.Deserialize<T>(cached.ToString(), JsonOptions);
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
            await connection.GetDatabase().StringSetAsync(redisKey, JsonSerializer.Serialize(created, JsonOptions), timeToLive);
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

    private string TenantKey(string key) => $"cache:{tenantContext.TenantId}:{key}";
}
