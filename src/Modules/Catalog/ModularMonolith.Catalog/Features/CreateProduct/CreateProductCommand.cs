using ModularMonolith.Catalog.Domain;
using ModularMonolith.Shared.Kernel.Cqrs;

namespace ModularMonolith.Catalog.Features.CreateProduct;

/// <summary>Creates a product for the current tenant; returns the new product id.</summary>
internal sealed record CreateProductCommand(string Name, string Sku, decimal Price, string Currency) : ICommand<ProductId>;
