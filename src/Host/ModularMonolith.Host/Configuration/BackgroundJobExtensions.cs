using Hangfire;
using Hangfire.PostgreSql;

namespace ModularMonolith.Host.Configuration;

/// <summary>
/// Hangfire (free LGPL edition) on PostgreSQL. Runs the outbox processors; storage lives in its own
/// <c>hangfire</c> schema of the same database.
/// </summary>
internal static class BackgroundJobExtensions
{
    public static WebApplicationBuilder AddBackgroundJobs(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("postgres")
            ?? throw new InvalidOperationException("Connection string 'postgres' is not configured.");

        builder.Services.AddHangfire(configuration => configuration
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
                options => options.UseNpgsqlConnection(connectionString),
                new PostgreSqlStorageOptions { SchemaName = "hangfire", PrepareSchemaIfNecessary = true }));

        builder.Services.AddHangfireServer(options =>
        {
            options.WorkerCount = Math.Clamp(Environment.ProcessorCount * 2, 2, 10);
            options.SchedulePollingInterval = TimeSpan.FromSeconds(5);
        });

        return builder;
    }

    /// <summary>Maps the Hangfire dashboard, restricted to the <c>admin</c> role.</summary>
    public static WebApplication MapJobDashboard(this WebApplication app)
    {
        app.MapHangfireDashboardWithAuthorizationPolicy(
            SecurityExtensions.AdminPolicy,
            "/hangfire",
            new DashboardOptions { DashboardTitle = "ModularMonolith jobs", DisplayStorageConnectionString = false });
        return app;
    }
}
