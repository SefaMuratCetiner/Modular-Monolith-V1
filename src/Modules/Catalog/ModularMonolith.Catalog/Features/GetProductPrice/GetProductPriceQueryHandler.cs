using Dapper;
using ModularMonolith.Catalog.Domain;
using ModularMonolith.Catalog.Infrastructure.ExchangeRates;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Catalog.Features.GetProductPrice;

/// <summary>
/// Reads the price (RLS-scoped) and converts it through the exchange-rate anti-corruption layer.
/// </summary>
internal sealed class GetProductPriceQueryHandler(
    IDbSession session,
    IExchangeRateProvider exchangeRates) : IQueryHandler<GetProductPriceQuery, ProductPrice>
{
    public async Task<Result<ProductPrice>> HandleAsync(GetProductPriceQuery query, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        var stored = await connection.QuerySingleOrDefaultAsync<StoredPrice>(new CommandDefinition(
            "select price as Amount, currency as Currency from catalog.products where id = @Id and not is_deleted",
            new { Id = query.ProductId.Value },
            cancellationToken: cancellationToken));

        if (stored is null)
        {
            return Result.Failure<ProductPrice>(CatalogErrors.ProductNotFound);
        }

        var target = query.Currency?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(target) || string.Equals(target, stored.Currency, StringComparison.Ordinal))
        {
            return Result.Success(new ProductPrice(query.ProductId.Value, stored.Amount, stored.Currency, null, null));
        }

        var rate = await exchangeRates.GetRateAsync(stored.Currency, target, cancellationToken);
        if (rate is null)
        {
            return Result.Failure<ProductPrice>(CatalogErrors.ExchangeRateUnavailable);
        }

        var converted = decimal.Round(stored.Amount * rate.Rate, 2, MidpointRounding.ToEven);
        return Result.Success(new ProductPrice(query.ProductId.Value, converted, target, rate.Rate, rate.AsOf));
    }

    private sealed record StoredPrice(decimal Amount, string Currency);
}
