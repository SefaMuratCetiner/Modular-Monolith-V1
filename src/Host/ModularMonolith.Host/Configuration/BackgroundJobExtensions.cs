using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Options;
using ModularMonolith.Host.Migrations;
using ModularMonolith.Shared.Infrastructure.Data;

namespace ModularMonolith.Host.Configuration;

/// <summary>
/// Hangfire (free LGPL edition) on PostgreSQL. Runs the outbox processors; storage lives in its own
/// <c>hangfire</c> schema of the same database.
/// </summary>
internal static class BackgroundJobExtensions
{
    public static WebApplicationBuilder AddBackgroundJobs(this WebApplicationBuilder builder)
    {
        // Same validated connection (least-privileged application login) as the rest of the application.
        builder.Services.AddHangfire((provider, configuration) => configuration
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
                options => options.UseNpgsqlConnection(provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString),
                new PostgreSqlStorageOptions
                {
                    SchemaName = HangfireSchemaStep.Schema,

                    // The schema is installed by the migrate command; the application has no DDL rights.
                    PrepareSchemaIfNecessary = false,
                }));

        builder.Services.AddHangfireServer(options =>
        {
            options.WorkerCount = Math.Clamp(Environment.ProcessorCount * 2, 2, 10);
            options.SchedulePollingInterval = TimeSpan.FromSeconds(5);
        });

        return builder;
    }

    /// <summary>Maps the Hangfire dashboard (cross-tenant), restricted to platform operators.</summary>
    public static WebApplication MapJobDashboard(this WebApplication app)
    {
        app.MapHangfireDashboardWithAuthorizationPolicy(
            SecurityExtensions.OperatorPolicy,
            "/hangfire",
            new DashboardOptions { DashboardTitle = "ModularMonolith jobs", DisplayStorageConnectionString = false });
        return app;
    }
}
