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
}
