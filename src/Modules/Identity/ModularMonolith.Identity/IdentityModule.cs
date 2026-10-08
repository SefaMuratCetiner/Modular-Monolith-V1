using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModularMonolith.Identity.Contracts;
using ModularMonolith.Identity.Infrastructure;
using ModularMonolith.Identity.Infrastructure.Keycloak;
using ModularMonolith.Shared.Infrastructure.Configuration;
using ModularMonolith.Shared.Infrastructure.Modules;
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

        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddRefitClient<IKeycloakMetadataApi>()
            .ConfigureHttpClient((provider, client) =>
            {
                var authority = provider.GetRequiredService<IOptions<IdentityOptions>>().Value.Authority!;
                client.BaseAddress = new Uri(authority.TrimEnd('/'));
            });
        services.AddHealthChecks().AddCheck<KeycloakHealthCheck>("keycloak", tags: ["ready"]);
    }
}
