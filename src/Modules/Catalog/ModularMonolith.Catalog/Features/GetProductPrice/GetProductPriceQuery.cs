using ModularMonolith.Shared.Kernel.Cqrs;

namespace ModularMonolith.Catalog.Features.GetProductPrice;

/// <summary>Gets a product's price, optionally converted to another currency.</summary>
internal sealed record GetProductPriceQuery(Guid ProductId, string? Currency) : IQuery<ProductPrice>;

/// <summary>Price read model.</summary>
internal sealed record ProductPrice(Guid ProductId, decimal Amount, string Currency, decimal? ExchangeRate, DateTimeOffset? RateAsOf);
