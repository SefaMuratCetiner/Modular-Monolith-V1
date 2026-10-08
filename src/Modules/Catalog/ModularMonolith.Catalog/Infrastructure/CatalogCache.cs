using ModularMonolith.Shared.Infrastructure.Caching;

namespace ModularMonolith.Catalog.Infrastructure;

/// <summary>
/// Every cache region of the Catalog module, declared in one place.
/// Bump a region's <c>SchemaVersion</c> whenever the cached value's shape changes.
/// </summary>
internal static class CatalogCache
{
    private const string Module = "catalog";

    /// <summary>Pages of the tenant's product list; invalidated as a whole when products change.</summary>
    public static CacheRegion ProductList { get; } = new(Module, "product-list", CacheScope.Tenant, SchemaVersion: 2, Invalidatable: true);

    /// <summary>Exchange rates per base currency: identical for every tenant, so cached once.</summary>
    public static CacheRegion ExchangeRates { get; } = new(Module, "exchange-rates", CacheScope.Shared, SchemaVersion: 1);

    public static CacheKey ProductListPage(int page, int pageSize) => new(ProductList, $"page:{page}:size:{pageSize}");

    public static CacheKey ExchangeRatesFor(string baseCurrency) => new(ExchangeRates, baseCurrency);
}
