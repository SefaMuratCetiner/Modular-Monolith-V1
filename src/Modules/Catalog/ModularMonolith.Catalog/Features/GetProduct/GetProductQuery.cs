using ModularMonolith.Catalog.Domain;
using ModularMonolith.Shared.Kernel.Cqrs;

namespace ModularMonolith.Catalog.Features.GetProduct;

/// <summary>Gets one product of the current tenant.</summary>
internal sealed record GetProductQuery(ProductId ProductId) : IQuery<ProductDetails>;

/// <summary>
/// Product read model (separate from the aggregate). <c>Version</c> is the optimistic-concurrency
/// token clients send back when they change the product.
/// </summary>
internal sealed record ProductDetails(
    Guid Id,
    string Name,
    string Sku,
    decimal Price,
    string Currency,
    string SellerId,
    string? SellerName,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);
