using System.Globalization;
using Serilog;
using Serilog.Sinks.Grafana.Loki;

namespace ModularMonolith.Host.Configuration;

/// <summary>
/// Serilog is the single logging pipeline. Sinks are chosen by configuration:
/// <list type="bullet">
/// <item>Console: always, through an asynchronous buffer.</item>
/// <item>Loki: when <c>Observability:LokiUrl</c> is set (compose and Aspire).</item>
/// <item>OTLP: when <c>Observability:OtlpLogs</c> is true and an OTLP endpoint exists (Aspire dashboard).
/// Never sent to Tempo, which does not accept logs.</item>
/// </list>
/// Serilog captures the current trace and span ids, so logs and traces correlate in Grafana.
/// </summary>
internal static class LoggingExtensions
{
    public static WebApplicationBuilder AddLogging(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        var serviceName = builder.Environment.ApplicationName;

        builder.Services.AddSerilog((services, logger) =>
        {
            logger
                .ReadFrom.Configuration(configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("service_name", serviceName)
                // Console writes happen on a background thread; request threads never block on stdout.
                // When the buffer is full, events are dropped rather than slowing requests down.
                .WriteTo.Async(
                    sink => sink.Console(formatProvider: CultureInfo.InvariantCulture),
                    bufferSize: 10_000,
                    blockWhenFull: false);

            var lokiUrl = configuration["Observability:LokiUrl"];
            if (!string.IsNullOrWhiteSpace(lokiUrl))
            {
                logger.WriteTo.GrafanaLoki(
                    lokiUrl,
                    labels: [new LokiLabel { Key = "service_name", Value = serviceName }],
                    propertiesAsLabels: ["level"]);
            }

            if (configuration.GetValue<bool>("Observability:OtlpLogs")
                && !string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            {
                // Endpoint, protocol and headers are read from the standard OTEL_* environment variables.
                logger.WriteTo.OpenTelemetry(options => options.ResourceAttributes["service.name"] = serviceName);
            }
        });

        return builder;
    }
}
