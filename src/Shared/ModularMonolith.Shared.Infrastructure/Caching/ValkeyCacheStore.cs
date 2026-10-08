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
/// <list type="bullet">
/// <item>Stampede protection: on a miss only one caller per key (per instance) runs the factory;
/// concurrent callers wait and then read the fresh value.</item>
/// <item>TTL jitter: expirations of entries written together are spread out.</item>
/// <item>Best effort: Valkey failures are logged and the factory result is returned uncached.</item>
/// </list>
/// </summary>
internal sealed class ValkeyCacheStore(
    ValkeyConnection valkey,
    CacheKeyFactory keys,
    ITenantContext tenantContext,
    ILogger<ValkeyCacheStore> logger) : ICacheStore
{
    // Striped locks: bounded memory, no per-key bookkeeping, rare false sharing between keys.
    private static readonly SemaphoreSlim[] Stripes = [.. Enumerable.Range(0, 256).Select(_ => new SemaphoreSlim(1, 1))];

    public async Task<T> GetOrCreateAsync<T>(CacheKey key, Func<CancellationToken, Task<T>> factory, TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(factory);

        string redisKey;
        try
        {
            redisKey = keys.Entry(key, tenantContext, await GetGenerationAsync(key.Region));
            var hit = await TryGetAsync<T>(redisKey);
            if (hit.Found)
            {
                return hit.Value!;
            }
        }
        catch (RedisException exception)
        {
            logger.CacheUnavailable(exception, key.Region.Resource);
            return await factory(cancellationToken);
        }

        var stripe = Stripes[(redisKey.GetHashCode(StringComparison.Ordinal) & int.MaxValue) % Stripes.Length];
        await stripe.WaitAsync(cancellationToken);
        try
        {
            // Another caller may have filled the entry while we waited.
            var filled = await TryGetBestEffortAsync<T>(redisKey, key.Region);
            if (filled.Found)
            {
                return filled.Value!;
            }

            var created = await factory(cancellationToken);
            try
            {
                await SetAsync(redisKey, created, timeToLive);
            }
            catch (RedisException exception)
            {
                logger.CacheUnavailable(exception, key.Region.Resource);
            }

            return created;
        }
        finally
        {
            stripe.Release();
        }
    }

    public async Task RemoveAsync(CacheKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        try
        {
            var redisKey = keys.Entry(key, tenantContext, await GetGenerationAsync(key.Region));
            await valkey.Multiplexer.GetDatabase().KeyDeleteAsync(redisKey);
        }
        catch (RedisException exception)
        {
            logger.CacheUnavailable(exception, key.Region.Resource);
        }
    }

    public async Task InvalidateAsync(CacheRegion region, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(region);
        if (!region.Invalidatable)
        {
            throw new InvalidOperationException($"Cache region '{region.Module}:{region.Resource}' is not invalidatable.");
        }

        try
        {
            await valkey.Multiplexer.GetDatabase().StringIncrementAsync(keys.Generation(region, tenantContext));
        }
        catch (RedisException exception)
        {
            logger.CacheUnavailable(exception, region.Resource);
        }
    }

    private async Task<long?> GetGenerationAsync(CacheRegion region)
    {
        if (!region.Invalidatable)
        {
            return null;
        }

        var stored = await valkey.Multiplexer.GetDatabase().StringGetAsync(keys.Generation(region, tenantContext));
        return stored.HasValue && long.TryParse(stored.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var generation)
            ? generation
            : 0;
    }

    private async Task<CacheHit<T>> TryGetAsync<T>(string redisKey)
    {
        var cached = await valkey.Multiplexer.GetDatabase().StringGetAsync(redisKey);
        if (!cached.HasValue)
        {
            return default;
        }

        var value = JsonSerializer.Deserialize<T>(cached.ToString(), JsonDefaults.Options);
        return value is null ? default : new CacheHit<T>(Found: true, value);
    }

    private async Task<CacheHit<T>> TryGetBestEffortAsync<T>(string redisKey, CacheRegion region)
    {
        try
        {
            return await TryGetAsync<T>(redisKey);
        }
        catch (RedisException exception)
        {
            logger.CacheUnavailable(exception, region.Resource);
            return default;
        }
    }

    private async Task SetAsync<T>(string redisKey, T value, TimeSpan timeToLive)
    {
#pragma warning disable CA5394 // Jitter is not security-sensitive.
        var jitter = TimeSpan.FromMilliseconds(timeToLive.TotalMilliseconds * 0.1 * Random.Shared.NextDouble());
#pragma warning restore CA5394
        await valkey.Multiplexer.GetDatabase().StringSetAsync(redisKey, JsonSerializer.Serialize(value, JsonDefaults.Options), timeToLive - jitter);
    }

    private readonly record struct CacheHit<T>(bool Found, T? Value);
}
