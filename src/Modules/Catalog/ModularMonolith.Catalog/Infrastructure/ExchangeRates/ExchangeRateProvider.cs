using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Caching;
using Refit;

namespace ModularMonolith.Catalog.Infrastructure.ExchangeRates;

/// <summary>
/// Anti-corruption layer over the external exchange-rate API with Valkey caching. Rates are the same
/// for every tenant, so they live in a shared (not tenant-scoped) cache region.
/// Returns <see langword="null"/> when no rate is available instead of leaking HTTP failures.
/// </summary>
internal sealed class ExchangeRateProvider(
    IExchangeRatesApi api,
    ICacheStore cache,
    IOptions<CatalogOptions> options) : IExchangeRateProvider
{
    public async Task<ExchangeRate?> GetRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken)
    {
        try
        {
            var rates = await cache.GetOrCreateAsync(
                CatalogCache.ExchangeRatesFor(fromCurrency),
                ct => api.GetLatestAsync(fromCurrency, ct),
                TimeSpan.FromSeconds(options.Value.ExchangeRateCacheSeconds),
                cancellationToken);

            if (!string.Equals(rates.Result, "success", StringComparison.OrdinalIgnoreCase)
                || !rates.Rates.TryGetValue(toCurrency, out var rate))
            {
                return null;
            }

            return new ExchangeRate(rate, DateTimeOffset.FromUnixTimeSeconds(rates.LastUpdateUnix));
        }
        catch (ApiException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}

/// <summary>A conversion rate and the instant it was published.</summary>
internal sealed record ExchangeRate(decimal Rate, DateTimeOffset AsOf);
