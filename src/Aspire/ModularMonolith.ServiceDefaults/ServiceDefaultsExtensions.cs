using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ModularMonolith.ServiceDefaults;

/// <summary>
/// Cross-cutting defaults for every runnable project.
/// <list type="bullet">
/// <item>Traces: OTLP (Aspire dashboard or Tempo) plus an optional dedicated Tempo exporter.</item>
/// <item>Metrics: one pipeline, scraped by Prometheus on <c>/metrics</c> (optionally also pushed via OTLP).</item>
/// <item>Logs: handled by Serilog in the host (console, Loki and optional OTLP).</item>
/// </list>
/// </summary>
public static class ServiceDefaultsExtensions
{
    /// <summary>Liveness endpoint (process is up; no dependency checks).</summary>
    public const string LivenessPath = "/health/live";

    /// <summary>Readiness endpoint (dependencies tagged <c>ready</c>).</summary>
    public const string ReadinessPath = "/health/ready";

    /// <summary>Prometheus scraping endpoint.</summary>
    public const string MetricsPath = "/metrics";

    /// <summary>Adds OpenTelemetry, health checks, service discovery and resilient HTTP clients.</summary>
    /// <typeparam name="TBuilder">The host builder type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder.</returns>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ConfigureOpenTelemetry();
        builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });
        return builder;
    }

    /// <summary>Maps health and metrics endpoints (anonymous).</summary>
    /// <param name="app">The application.</param>
    /// <returns>The application.</returns>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapHealthChecks(LivenessPath, new HealthCheckOptions { Predicate = check => check.Tags.Contains("live") }).AllowAnonymous();
        app.MapHealthChecks(ReadinessPath, new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();
        app.MapPrometheusScrapingEndpoint(MetricsPath).AllowAnonymous();
        return app;
    }

    private static void ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        var serviceName = builder.Environment.ApplicationName;
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("ModularMonolith.*", "Npgsql")
                .AddPrometheusExporter())
            .WithTracing(tracing => tracing
                .AddSource(serviceName, "ModularMonolith.*", "Npgsql")
                .AddAspNetCoreInstrumentation(options => options.Filter = context => !IsInfrastructurePath(context.Request.Path))
                .AddHttpClientInstrumentation());

        var configuration = builder.Configuration;
        if (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddOtlpExporter());

            // Only enable when the OTLP receiver accepts metrics (Aspire dashboard); Tempo does not.
            if (configuration.GetValue<bool>("Observability:OtlpMetrics"))
            {
                builder.Services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddOtlpExporter());
            }
        }

        // Optional second trace exporter, e.g. Tempo while the Aspire dashboard receives the default one.
        var tempoEndpoint = configuration["Observability:TempoEndpoint"];
        if (Uri.TryCreate(tempoEndpoint, UriKind.Absolute, out var tempoUri))
        {
            builder.Services.ConfigureOpenTelemetryTracerProvider(tracing =>
                tracing.AddOtlpExporter("tempo", options => options.Endpoint = tempoUri));
        }
    }

    private static bool IsInfrastructurePath(PathString path)
        => path.StartsWithSegments(LivenessPath, StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments(ReadinessPath, StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments(MetricsPath, StringComparison.OrdinalIgnoreCase);
}
