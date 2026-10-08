using ModularMonolith.Catalog.Domain;
using ModularMonolith.Shared.Kernel.Cqrs;

namespace ModularMonolith.Catalog.Features.ChangeProductPrice;

/// <summary>
/// Changes a product's price if it still has <paramref name="ExpectedVersion"/>; returns the new version.
/// </summary>
/// <param name="ProductId">The product.</param>
/// <param name="Price">The new amount.</param>
/// <param name="Currency">The new currency.</param>
/// <param name="ExpectedVersion">The version the caller read (optimistic concurrency token).</param>
internal sealed record ChangeProductPriceCommand(ProductId ProductId, decimal Price, string Currency, int ExpectedVersion) : ICommand<int>;
