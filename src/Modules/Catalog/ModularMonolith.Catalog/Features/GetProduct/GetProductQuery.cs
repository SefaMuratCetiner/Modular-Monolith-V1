using ModularMonolith.Shared.Kernel.Cqrs;

namespace ModularMonolith.Catalog.Features.GetProduct;

/// <summary>Gets one product of the current tenant.</summary>
internal sealed record GetProductQuery(Guid ProductId) : IQuery<ProductDetails>;

/// <summary>Product read model (separate from the aggregate).</summary>
internal sealed record ProductDetails(Guid Id, string Name, string Sku, decimal Price, string Currency, string SellerId, string? SellerName);
