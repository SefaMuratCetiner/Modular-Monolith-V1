using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModularMonolith.Catalog.Contracts;
using ModularMonolith.Catalog.Infrastructure;
using ModularMonolith.Catalog.Infrastructure.ExchangeRates;
using ModularMonolith.Shared.Infrastructure.Modules;
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
        services.AddOptions<CatalogOptions>()
            .Bind(configuration.GetSection(CatalogOptions.SectionName))
            .Validate(options => Uri.IsWellFormedUriString(options.ExchangeRatesBaseUrl, UriKind.Absolute), "Catalog:ExchangeRatesBaseUrl must be an absolute URL.")
            .ValidateOnStart();

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ExchangeRateProvider>();
        services.AddRefitClient<IExchangeRatesApi>()
            .ConfigureHttpClient((provider, client) =>
                client.BaseAddress = new Uri(provider.GetRequiredService<IOptions<CatalogOptions>>().Value.ExchangeRatesBaseUrl));
    }
}
