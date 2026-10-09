using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModularMonolith.Identity.Contracts;
using ModularMonolith.Identity.Infrastructure;
using ModularMonolith.Identity.Infrastructure.Keycloak;
using ModularMonolith.Shared.Infrastructure.Configuration;
using ModularMonolith.Shared.Infrastructure.Modules;
using ModularMonolith.Shared.Infrastructure.Security;
using Refit;

namespace ModularMonolith.Identity;

/// <summary>
/// Identity module entry point: the only public type of the module.
/// </summary>
public sealed class IdentityModule : IModule
{
    /// <inheritdoc />
    public string Name => "Identity";

    /// <inheritdoc />
    public string Schema => "identity";

    /// <inheritdoc />
    public Assembly Assembly => typeof(IdentityModule).Assembly;

    /// <inheritdoc />
    public Assembly ContractsAssembly => typeof(UserRegisteredIntegrationEvent).Assembly;

    /// <inheritdoc />
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<IdentityOptions, IdentityOptionsValidator>(configuration, IdentityOptions.SectionName)
            .PostConfigure(options => options.Authority ??= configuration["Authentication:Authority"]);

        services.AddPermissions(IdentityPermissions.All);
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddRefitClient<IKeycloakMetadataApi>()
            .ConfigureHttpClient((provider, client) =>
            {
                var authority = provider.GetRequiredService<IOptions<IdentityOptions>>().Value.Authority!;
                client.BaseAddress = new Uri(authority.TrimEnd('/'));
            })

            // Dedicated pipeline: a readiness probe must answer quickly, so no long retry chains.
            .AddStandardResilienceHandler(resilience =>
            {
                resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
                resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
                resilience.Retry.MaxRetryAttempts = 1;
                resilience.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                resilience.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
            });
        services.AddHealthChecks().AddCheck<KeycloakHealthCheck>("keycloak", tags: ["ready"]);

        AddKeycloakAdminApi(services);
    }

    // Admin REST API for account erasure: a service-account token (client credentials, cached) on every
    // request. The calls are idempotent (DELETE, PUT, logout), so standard retries are safe.
    private static void AddKeycloakAdminApi(IServiceCollection services)
    {
        services.AddSingleton<KeycloakAdminTokenProvider>();
        services.AddTransient<KeycloakAdminAuthenticationHandler>();
        services.AddHttpClient(KeycloakAdminTokenProvider.HttpClientName)
            .AddStandardResilienceHandler(resilience => resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10));
        services.AddRefitClient<IKeycloakAdminApi>()
            .ConfigureHttpClient((provider, client) =>
                client.BaseAddress = provider.GetRequiredService<IOptions<IdentityOptions>>().Value.ServerUrl)
            .AddHttpMessageHandler<KeycloakAdminAuthenticationHandler>()
            .AddStandardResilienceHandler(resilience => resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15));
    }
}
