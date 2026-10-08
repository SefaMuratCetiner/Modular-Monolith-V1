using System.Globalization;
using FluentValidation;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Caching;
using ModularMonolith.Shared.Infrastructure.Cqrs;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Infrastructure.Health;
using ModularMonolith.Shared.Infrastructure.Idempotency;
using ModularMonolith.Shared.Infrastructure.Localization;
using ModularMonolith.Shared.Infrastructure.Messaging;
using ModularMonolith.Shared.Infrastructure.Migrations;
using ModularMonolith.Shared.Infrastructure.Modules;
using ModularMonolith.Shared.Infrastructure.Security;
using ModularMonolith.Shared.Infrastructure.Tenancy;
using ModularMonolith.Shared.Infrastructure.Web;
using ModularMonolith.Shared.Kernel.Context;
using ModularMonolith.Shared.Kernel.Messaging;
using Npgsql;
using StackExchange.Redis;

namespace ModularMonolith.Shared.Infrastructure;

/// <summary>
/// Composition helpers used by the host.
/// </summary>
public static class SharedInfrastructureExtensions
{
    /// <summary>The Valkey connection string name (<c>ConnectionStrings:cache</c>).</summary>
    public const string CacheConnectionName = "cache";

    /// <summary>
    /// Registers all building blocks and every module. This is the only place modules are wired in.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="modules">The modules composed into this host.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddSharedInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IReadOnlyCollection<IModule> modules)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var catalog = new ModuleCatalog(modules);
        services.AddSingleton(catalog);
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();

        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<TenancyOptions>(configuration.GetSection(TenancyOptions.SectionName));
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.Configure<Localization.LocalizationOptions>(configuration.GetSection(Localization.LocalizationOptions.SectionName));

        AddData(services, configuration);
        AddMessaging(services);
        AddCaching(services, configuration);

        // Context
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantContext>());
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        // CQRS pipeline collaborators
        services.AddScoped<DomainEventDispatcher>();
        services.AddScoped<AuditWriter>();

        // Web conventions
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddSingleton<IStringLocalizerFactory, JsonStringLocalizerFactory>();
        services.AddTransient(typeof(IStringLocalizer<>), typeof(StringLocalizer<>));
        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"])
            .AddCheck<ValkeyHealthCheck>("valkey", tags: ["ready"]);

        // Modules: handlers, validators and module-specific services.
        var integrationHandlers = new IntegrationHandlerMap();
        services.AddSingleton(integrationHandlers);
        foreach (var module in catalog.Modules)
        {
            services.AddModuleHandlers(module, integrationHandlers);
            services.AddValidatorsFromAssembly(module.Assembly, ServiceLifetime.Scoped, includeInternalTypes: true);
            module.AddServices(services, configuration);
        }

        return services;
    }

    /// <summary>
    /// Adds request localization and tenant resolution. Call after <c>UseAuthentication</c>.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder.</returns>
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
        => app.UseMiddleware<TenantResolutionMiddleware>();

    /// <summary>Configures request culture resolution from <c>Accept-Language</c>.</summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder.</returns>
    public static IApplicationBuilder UseConfiguredRequestLocalization(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var options = app.ApplicationServices.GetRequiredService<IOptions<Localization.LocalizationOptions>>().Value;
        CultureInfo[] cultures = [.. options.SupportedCultures.Select(CultureInfo.GetCultureInfo)];
        if (cultures.Length == 0)
        {
            cultures = [CultureInfo.GetCultureInfo("en")];
        }

        return app.UseRequestLocalization(new RequestLocalizationOptions
        {
            DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(cultures[0]),
            SupportedCultures = cultures,
            SupportedUICultures = cultures,
        });
    }

    /// <summary>
    /// Runs module migrations (advisory-locked) and schedules one recurring outbox sweep per module.
    /// </summary>
    /// <param name="services">The root service provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public static async Task InitializeModulesAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.GetRequiredService<IOptions<DatabaseOptions>>().Value.MigrateOnStartup)
        {
            await services.GetRequiredService<ModuleMigrator>().MigrateAsync(cancellationToken);
        }

        var sweepCron = services.GetRequiredService<IOptions<OutboxOptions>>().Value.SweepCron;
        var jobs = services.GetRequiredService<IRecurringJobManager>();
        foreach (var module in services.GetRequiredService<ModuleCatalog>().Modules)
        {
            var schema = module.Schema;
            jobs.AddOrUpdate<OutboxProcessor>(
                $"outbox-{schema}",
                processor => processor.ProcessAsync(schema, CancellationToken.None),
                sweepCron);
        }
    }

    private static void AddData(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(provider =>
        {
            var name = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionStringName;
            var connectionString = configuration.GetConnectionString(name)
                ?? throw new InvalidOperationException($"Connection string '{name}' is not configured.");
            return new NpgsqlDataSourceBuilder(connectionString).Build();
        });
        services.AddScoped<DbSession>();
        services.AddScoped<IDbSession>(provider => provider.GetRequiredService<DbSession>());
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<DbSession>());
        services.AddSingleton<ModuleMigrator>();
    }

    private static void AddMessaging(IServiceCollection services)
    {
        services.AddSingleton<IntegrationEventRegistry>();
        services.AddSingleton<InProcessEventBus>();
        services.AddScoped<OutboxDispatchTrigger>();
        services.AddScoped<IOutbox, Outbox>();
        services.AddTransient<OutboxProcessor>();
    }

    private static void AddCaching(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var connectionString = configuration.GetConnectionString(CacheConnectionName)
                ?? throw new InvalidOperationException($"Connection string '{CacheConnectionName}' is not configured.");
            var options = ConfigurationOptions.Parse(connectionString);
            options.AbortOnConnectFail = false; // start even if Valkey is down; the cache fails open
            return ConnectionMultiplexer.Connect(options);
        });
        services.AddScoped<ICacheStore, ValkeyCacheStore>();
        services.AddSingleton<IdempotencyStore>();
    }
}
