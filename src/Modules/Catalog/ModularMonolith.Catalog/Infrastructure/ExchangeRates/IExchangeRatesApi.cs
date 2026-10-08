using System.Text.Json.Serialization;
using Refit;

namespace ModularMonolith.Catalog.Infrastructure.ExchangeRates;

/// <summary>
/// Refit client for the free open.er-api.com service (no API key required).
/// Resilience (retry, circuit breaker, timeout) comes from ServiceDefaults.
/// </summary>
internal interface IExchangeRatesApi
{
    [Get("/v6/latest/{baseCurrency}")]
    Task<ExchangeRatesResponse> GetLatestAsync(string baseCurrency, CancellationToken cancellationToken);
}

/// <summary>External API response shape (kept inside Infrastructure; never exposed by the module).</summary>
internal sealed record ExchangeRatesResponse(
    [property: JsonPropertyName("result")] string Result,
    [property: JsonPropertyName("base_code")] string BaseCode,
    [property: JsonPropertyName("time_last_update_unix")] long LastUpdateUnix,
    [property: JsonPropertyName("rates")] IReadOnlyDictionary<string, decimal> Rates);
