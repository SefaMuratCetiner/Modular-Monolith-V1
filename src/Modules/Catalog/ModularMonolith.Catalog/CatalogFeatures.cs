namespace ModularMonolith.Catalog;

/// <summary>Feature flags owned by the Catalog module (section <c>FeatureManagement</c>).</summary>
internal static class CatalogFeatures
{
    /// <summary>Kill switch: when enabled, catalog writes return 503.</summary>
    public const string WriteDisabled = "CatalogWriteDisabled";
}
