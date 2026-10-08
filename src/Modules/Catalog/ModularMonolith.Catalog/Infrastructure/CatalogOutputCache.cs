using ModularMonolith.Shared.Infrastructure.Web;

namespace ModularMonolith.Catalog.Infrastructure;

/// <summary>Output-cache policies of the Catalog module (HTTP response caching, tenant-scoped).</summary>
internal static class CatalogOutputCache
{
    /// <summary>Policy for <c>GET /catalog/products/{id}/price</c> (the response depends on an external API).</summary>
    public const string PricePolicy = "catalog-prices";

    private const string PriceResource = "catalog-prices";

    public static TenantOutputCachePolicy CreatePricePolicy(TimeSpan duration) => new(PriceResource, duration);

    /// <summary>The tag that evicts every cached price response of a tenant.</summary>
    public static string PriceTag(string tenantId) => TenantOutputCachePolicy.TagFor(PriceResource, tenantId);
}
