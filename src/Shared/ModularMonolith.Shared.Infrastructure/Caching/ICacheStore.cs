namespace ModularMonolith.Shared.Infrastructure.Caching;

/// <summary>
/// Distributed cache (Valkey) with structured keys (<see cref="CacheKey"/>), tenant or shared scope,
/// per-key single-flight (stampede protection), TTL jitter and region-wide invalidation.
/// The cache is best-effort: when Valkey is down every call falls through to the factory.
/// </summary>
public interface ICacheStore
{
    /// <summary>Gets a cached value or creates, stores and returns it.</summary>
    /// <typeparam name="T">The value type (must be JSON serializable).</typeparam>
    /// <param name="key">The key.</param>
    /// <param name="factory">Produces the value on a cache miss (runs once per key and instance at a time).</param>
    /// <param name="timeToLive">The nominal expiry; up to 10% jitter is subtracted to spread expirations.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The value.</returns>
    Task<T> GetOrCreateAsync<T>(CacheKey key, Func<CancellationToken, Task<T>> factory, TimeSpan timeToLive, CancellationToken cancellationToken);

    /// <summary>Removes one entry.</summary>
    /// <param name="key">The key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task RemoveAsync(CacheKey key, CancellationToken cancellationToken);

    /// <summary>
    /// Invalidates every entry of an invalidatable region (for the current tenant when tenant-scoped)
    /// by bumping its generation. Old entries are never read again and expire through their TTL.
    /// </summary>
    /// <param name="region">The region (must have <see cref="CacheRegion.Invalidatable"/> set).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task InvalidateAsync(CacheRegion region, CancellationToken cancellationToken);
}
