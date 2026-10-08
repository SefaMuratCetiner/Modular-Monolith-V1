namespace ModularMonolith.Shared.Infrastructure.Caching;

/// <summary>
/// A named group of cache entries. Every key in the application is built from a region, so the
/// key layout is defined in one place (<see cref="CacheKeyFactory"/>):
/// <c>mm:{environment}:{tenant|shared}:{module}:{resource}:s{schemaVersion}[:g{generation}]:{item}</c>.
/// </summary>
/// <param name="Module">The owning module, e.g. <c>catalog</c>.</param>
/// <param name="Resource">The cached resource, e.g. <c>product-list</c>.</param>
/// <param name="Scope">Tenant or shared.</param>
/// <param name="SchemaVersion">Bump when the cached value's shape changes, so old entries are ignored.</param>
/// <param name="Invalidatable">
/// When true, every entry of the region can be dropped at once with <see cref="ICacheStore.InvalidateAsync"/>
/// (costs one extra round trip per lookup to read the region generation).
/// </param>
public sealed record CacheRegion(string Module, string Resource, CacheScope Scope = CacheScope.Tenant, int SchemaVersion = 1, bool Invalidatable = false);

/// <summary>An entry inside a region.</summary>
/// <param name="Region">The region.</param>
/// <param name="Item">
/// The item discriminator, e.g. <c>page:1:size:20</c>. Anything outside <c>[A-Za-z0-9:._-]</c> or longer
/// than 100 characters is replaced by its SHA-256 hash, so raw user input never appears in a key.
/// </param>
public sealed record CacheKey(CacheRegion Region, string Item);

/// <summary>Who a cache entry belongs to.</summary>
public enum CacheScope
{
    /// <summary>Per tenant (default): the key includes the current tenant id.</summary>
    Tenant = 0,

    /// <summary>Global, tenant-independent data such as exchange rates. Never put tenant data here.</summary>
    Shared = 1,
}
