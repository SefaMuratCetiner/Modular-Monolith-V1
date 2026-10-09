using System.Reflection;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModularMonolith.Catalog.Contracts;
using ModularMonolith.Catalog.Infrastructure;
using ModularMonolith.Catalog.Infrastructure.ExchangeRates;
using ModularMonolith.Shared.Infrastructure.Configuration;
using ModularMonolith.Shared.Infrastructure.Modules;
using ModularMonolith.Shared.Infrastructure.Security;
using Refit;

namespace ModularMonolith.Catalog;

/// <summary>
/// Catalog module entry point: the only public type of the module.
/// Handlers, validators and endpoints are discovered automatically from this assembly;
/// only module-specific infrastructure is registered here.
/// </summary>
public sealed class CatalogModule : IModule
{
    /// <inheritdoc />
    public string Name => "Catalog";

    /// <inheritdoc />
    public string Schema => "catalog";

    /// <inheritdoc />
    public Assembly Assembly => typeof(CatalogModule).Assembly;

    /// <inheritdoc />
    public Assembly ContractsAssembly => typeof(ProductCreatedIntegrationEvent).Assembly;

    /// <inheritdoc />
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<CatalogOptions, CatalogOptionsValidator>(configuration, CatalogOptions.SectionName);
        services.AddPermissions(CatalogPermissions.All);

        services.AddOptions<OutputCacheOptions>()
            .Configure<IOptions<CatalogOptions>>((outputCache, catalog) => outputCache.AddPolicy(
                CatalogOutputCache.PricePolicy,
                CatalogOutputCache.CreatePricePolicy(TimeSpan.FromSeconds(catalog.Value.PriceOutputCacheSeconds))));

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IExchangeRateProvider, ExchangeRateProvider>();
        services.AddRefitClient<IExchangeRatesApi>()
            .ConfigureHttpClient((provider, client) =>
                client.BaseAddress = new Uri(provider.GetRequiredService<IOptions<CatalogOptions>>().Value.ExchangeRatesBaseUrl))

            // Dedicated pipeline for this dependency: a slow free API must fail fast (the price
            // endpoint degrades to 503) and must not be hammered with retries.
            .AddStandardResilienceHandler(resilience =>
            {
                resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
                resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(8);
                resilience.Retry.MaxRetryAttempts = 2;
                resilience.Retry.UseJitter = true;
                resilience.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                resilience.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
            });
    }
}
