namespace ModularMonolith.Catalog;

/// <summary>
/// Catalog settings (section <c>Modules:Catalog</c>).
/// </summary>
internal sealed class CatalogOptions
{
    public const string SectionName = "Modules:Catalog";

    /// <summary>Gets or sets how long the product list stays cached.</summary>
    public int ProductCacheSeconds { get; set; } = 60;

    /// <summary>Gets or sets how long exchange rates stay cached.</summary>
    public int ExchangeRateCacheSeconds { get; set; } = 600;

    /// <summary>Gets or sets how long price responses are served from the output cache.</summary>
    public int PriceOutputCacheSeconds { get; set; } = 30;

    /// <summary>Gets or sets the base URL of the free exchange-rate API.</summary>
    public string ExchangeRatesBaseUrl { get; set; } = "https://open.er-api.com";
}
