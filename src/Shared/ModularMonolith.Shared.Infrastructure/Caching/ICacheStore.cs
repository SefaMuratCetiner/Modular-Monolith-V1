namespace ModularMonolith.Shared.Infrastructure.Caching;

/// <summary>
/// Tenant-scoped distributed cache (Valkey). Keys are automatically prefixed with the current
/// tenant, so one tenant can never read another tenant's cached data.
/// </summary>
public interface ICacheStore
{
    /// <summary>Gets a cached value or creates, stores and returns it.</summary>
    /// <typeparam name="T">The value type (must be JSON serializable).</typeparam>
    /// <param name="key">The logical key (without tenant prefix).</param>
    /// <param name="factory">Produces the value on a cache miss.</param>
    /// <param name="timeToLive">The expiry.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The value.</returns>
    Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan timeToLive, CancellationToken cancellationToken);

    /// <summary>Removes a cached value.</summary>
    /// <param name="key">The logical key (without tenant prefix).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task RemoveAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Gets or creates a value inside a cache region. All entries of a region are invalidated at once
    /// by <see cref="InvalidateRegionAsync"/> (e.g. every page of a paged list).
    /// </summary>
    /// <typeparam name="T">The value type (must be JSON serializable).</typeparam>
    /// <param name="region">The logical region, e.g. <c>catalog:products</c>.</param>
    /// <param name="key">The key inside the region, e.g. <c>page:1:size:20</c>.</param>
    /// <param name="factory">Produces the value on a cache miss.</param>
    /// <param name="timeToLive">The expiry.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The value.</returns>
    Task<T> GetOrCreateInRegionAsync<T>(string region, string key, Func<CancellationToken, Task<T>> factory, TimeSpan timeToLive, CancellationToken cancellationToken);

    /// <summary>
    /// Invalidates every entry of a region for the current tenant by bumping the region version.
    /// Old entries are never read again and expire through their TTL.
    /// </summary>
    /// <param name="region">The logical region.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task InvalidateRegionAsync(string region, CancellationToken cancellationToken);
}
