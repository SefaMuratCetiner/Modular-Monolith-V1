using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure;
using ModularMonolith.Shared.Infrastructure.Migrations;
using ModularMonolith.Shared.Infrastructure.Modules;
using Npgsql;

namespace ModularMonolith.Host.Migrations;

/// <summary>
/// <c>dotnet ModularMonolith.Host.dll migrate</c>: applies all database migrations and exits.
/// It runs as its own short-lived process (Aspire resource, compose service, Kubernetes Job or CI step)
/// with the schema owner's connection string, before the new application version starts. The serving
/// application therefore never holds DDL rights. Exit code 0 = success, 1 = failure.
/// </summary>
internal static class MigrationCommand
{
    public const string Name = "migrate";

    public static async Task<int> RunAsync(string[] args, IReadOnlyCollection<IModule> modules)
    {
        // A plain generic host: no web server, no Valkey, no Hangfire server, only what migrations need.
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);
        builder.Services.AddModuleMigrations(builder.Configuration, modules);
        builder.Services.AddSingleton<IDatabaseSetupStep, HangfireSchemaStep>();

        using var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(MigrationCommand));
        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        try
        {
            await host.Services.GetRequiredService<ModuleMigrator>().MigrateAsync(lifetime.ApplicationStopping);
            HostLog.MigrationsCompleted(logger);
            return 0;
        }
        catch (Exception exception) when (exception is InvalidOperationException or NpgsqlException or OptionsValidationException)
        {
            HostLog.MigrationsFailed(logger, exception);
            return 1;
        }
    }
}
