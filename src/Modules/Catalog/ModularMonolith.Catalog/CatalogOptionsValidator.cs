using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Configuration;

namespace ModularMonolith.Catalog;

/// <summary>Fails startup on invalid Catalog settings.</summary>
internal sealed class CatalogOptionsValidator : IValidateOptions<CatalogOptions>
{
    public ValidateOptionsResult Validate(string? name, CatalogOptions options)
    {
        var failures = new List<string>();
        if (options.ProductCacheSeconds is < 1 or > 86_400)
        {
            failures.Add("ProductCacheSeconds must be between 1 and 86400.");
        }

        if (options.ExchangeRateCacheSeconds is < 1 or > 86_400)
        {
            failures.Add("ExchangeRateCacheSeconds must be between 1 and 86400.");
        }

        if (options.PriceOutputCacheSeconds is < 1 or > 3600)
        {
            failures.Add("PriceOutputCacheSeconds must be between 1 and 3600.");
        }

        if (!OptionsValidation.IsHttpUrl(options.ExchangeRatesBaseUrl))
        {
            failures.Add("ExchangeRatesBaseUrl must be an absolute http(s) URL.");
        }

        return OptionsValidation.ToResult(CatalogOptions.SectionName, failures);
    }
}
