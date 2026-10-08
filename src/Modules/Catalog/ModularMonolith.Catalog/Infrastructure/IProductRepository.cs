using ModularMonolith.Catalog.Domain;

namespace ModularMonolith.Catalog.Infrastructure;

/// <summary>
/// Write-side persistence for the <see cref="Product"/> aggregate.
/// Read-side queries live in their slices and use <c>IDbSession</c> directly.
/// </summary>
internal interface IProductRepository
{
    Task<bool> SkuExistsAsync(Sku sku, CancellationToken cancellationToken);

    Task<Product?> GetAsync(ProductId id, CancellationToken cancellationToken);

    Task AddAsync(Product product, CancellationToken cancellationToken);

    /// <summary>
    /// Persists a price change if the stored version still equals <c>product.Version</c>.
    /// </summary>
    /// <returns><see langword="false"/> when another request changed the product first (optimistic concurrency).</returns>
    Task<bool> UpdatePriceAsync(Product product, CancellationToken cancellationToken);
}
