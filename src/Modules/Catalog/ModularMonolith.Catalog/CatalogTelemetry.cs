using System.Diagnostics.Metrics;

namespace ModularMonolith.Catalog;

/// <summary>
/// Catalog business metrics. Recorded only after the transaction commits, so they count facts,
/// not attempts. Tags are bounded (currency), never tenant or user ids.
/// The meter name matches the <c>ModularMonolith.*</c> subscription in ServiceDefaults.
/// </summary>
internal static class CatalogTelemetry
{
    public const string MeterName = "ModularMonolith.Catalog";

    private static readonly Meter Meter = new(MeterName);

    public static Counter<long> ProductsCreated { get; } =
        Meter.CreateCounter<long>("modmono.catalog.products.created", description: "Products created (committed).");

    public static Counter<long> PriceChanges { get; } =
        Meter.CreateCounter<long>("modmono.catalog.products.price_changes", description: "Product price changes (committed).");
}
