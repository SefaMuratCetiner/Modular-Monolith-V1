using System.Reflection;
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
/// Cross-cutting defaults for every runnable project (resilience pipelines are per dependency, not global).
/// <list type="bullet">
/// <item>Traces: OTLP (Aspire dashboard or the OpenTelemetry Collector), parent-based ratio head sampling.</item>
/// <item>Resource: service.name, service.version, service.instance.id, deployment.environment.name.</item>
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
        // No global resilience pipeline: every outbound dependency registers its own, with timeouts,
        // retries and circuit breaking sized for that dependency (see the module registrations).
        builder.Services.ConfigureHttpClientDefaults(http => http.AddServiceDiscovery());
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

    /// <summary>Gets the application version reported as <c>service.version</c> (informational version incl. commit).</summary>
    /// <returns>The version.</returns>
    public static string ServiceVersion()
        => Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

    private static void ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        var serviceName = builder.Environment.ApplicationName;
        var environmentName = builder.Environment.EnvironmentName;
        var configuration = builder.Configuration;

        // Head sampling: keep this fraction of new traces (children follow their parent's decision).
        // With a collector in front of the backend, keep 1.0 here and let its tail sampling decide
        // (errors and slow traces are always kept); lower it only to cut volume at the source.
        var sampleRatio = configuration.GetValue("Observability:TraceSampleRatio", 1.0);
        if (sampleRatio is < 0 or > 1)
        {
            throw new InvalidOperationException("Observability:TraceSampleRatio must be between 0 and 1.");
        }

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName, serviceVersion: ServiceVersion(), serviceInstanceId: Environment.MachineName)
                .AddAttributes(
                [
                    new KeyValuePair<string, object>("deployment.environment.name", environmentName),
                    new KeyValuePair<string, object>("deployment.environment", environmentName), // older backends
                ]))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("ModularMonolith.*", "Npgsql")
                .AddPrometheusExporter())
            .WithTracing(tracing => tracing
                .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(sampleRatio)))
                .AddSource(serviceName, "ModularMonolith.*", "Npgsql")
                .AddAspNetCoreInstrumentation(options => options.Filter = context => !IsInfrastructurePath(context.Request.Path))
                .AddHttpClientInstrumentation());

        if (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddOtlpExporter());

            // Only enable when the OTLP receiver accepts metrics (Aspire dashboard); metrics are otherwise scraped.
            if (configuration.GetValue<bool>("Observability:OtlpMetrics"))
            {
                builder.Services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddOtlpExporter());
            }
        }

        // Optional second trace destination: the OpenTelemetry Collector (which tail-samples and forwards
        // to Tempo) while the default OTLP exporter feeds the Aspire dashboard.
        var collectorEndpoint = configuration["Observability:CollectorEndpoint"];
        if (Uri.TryCreate(collectorEndpoint, UriKind.Absolute, out var collectorUri))
        {
            builder.Services.ConfigureOpenTelemetryTracerProvider(tracing =>
                tracing.AddOtlpExporter("collector", options => options.Endpoint = collectorUri));
        }
    }

    private static bool IsInfrastructurePath(PathString path)
        => path.StartsWithSegments(LivenessPath, StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments(ReadinessPath, StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments(MetricsPath, StringComparison.OrdinalIgnoreCase);
}
