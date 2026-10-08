namespace ModularMonolith.Catalog.Infrastructure.ExchangeRates;

/// <summary>
/// Port for currency conversion rates. Slices depend on this abstraction, so the external API
/// (and its anti-corruption layer) can be replaced or faked in tests.
/// </summary>
internal interface IExchangeRateProvider
{
    /// <summary>Gets the rate from one currency to another, or <see langword="null"/> when unavailable.</summary>
    Task<ExchangeRate?> GetRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken);
}
