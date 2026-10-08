using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;
using ModularMonolith.ServiceDefaults;
using ModularMonolith.Shared.Infrastructure.Configuration;
using ModularMonolith.Shared.Infrastructure.Serialization;
using ModularMonolith.Shared.Infrastructure.Web;

namespace ModularMonolith.Host.Configuration;

/// <summary>
/// HTTP hosting conventions: Kestrel limits, reverse-proxy headers, HTTPS/HSTS, one JSON configuration,
/// problem details for every error, request timeouts, response compression and security headers.
/// </summary>
internal static class HttpPlatformExtensions
{
    public static WebApplicationBuilder AddHttpPlatform(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddValidatedOptions<HttpOptions, HttpOptionsValidator>(builder.Configuration, HttpOptions.SectionName);
        services.AddValidatedOptions<ReverseProxyOptions, ReverseProxyOptionsValidator>(builder.Configuration, ReverseProxyOptions.SectionName);

        AddTransport(services);
        AddResponseConventions(services);
        return builder;
    }

    private static void AddTransport(IServiceCollection services)
    {
        // Kestrel: no Server header, bounded request bodies (413 when exceeded).
        services.AddOptions<KestrelServerOptions>()
            .Configure<IOptions<HttpOptions>>((kestrel, http) =>
            {
                kestrel.AddServerHeader = false;
                kestrel.Limits.MaxRequestBodySize = http.Value.MaxRequestBodyBytes;
            });

        // Trust X-Forwarded-* only from configured proxies (TLS usually terminates at the proxy).
        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<ReverseProxyOptions>>((forwarded, proxy) =>
            {
                forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
                forwarded.ForwardLimit = proxy.Value.ForwardLimit;
                foreach (var address in proxy.Value.KnownProxies)
                {
                    forwarded.KnownProxies.Add(IPAddress.Parse(address));
                }

                foreach (var network in proxy.Value.KnownNetworks)
                {
                    forwarded.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
                }
            });

        services.AddOptions<HstsOptions>()
            .Configure<IOptions<HttpOptions>>((hsts, http) =>
            {
                hsts.MaxAge = TimeSpan.FromDays(http.Value.HstsMaxAgeDays);
                hsts.IncludeSubDomains = true;
            });
        services.AddOptions<HttpsRedirectionOptions>()
            .Configure<IOptions<HttpOptions>>((redirection, http) =>
            {
                redirection.HttpsPort = http.Value.HttpsPort;
                redirection.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
            });
    }

    private static void AddResponseConventions(IServiceCollection services)
    {
        // One JSON convention (also used by FastEndpoints, see ApiConventions).
        services.ConfigureHttpJsonOptions(options => JsonDefaults.Apply(options.SerializerOptions));

        // Every error body is RFC 9457 problem details with a traceId.
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier);

        services.AddRequestTimeouts();
        services.AddOptions<RequestTimeoutOptions>()
            .Configure<IOptions<HttpOptions>>((timeouts, http) => timeouts.DefaultPolicy = new RequestTimeoutPolicy
            {
                Timeout = TimeSpan.FromSeconds(http.Value.RequestTimeoutSeconds),
                TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
                WriteTimeoutResponse = context => ProblemResponses.WriteAsync(
                    context,
                    StatusCodes.Status504GatewayTimeout,
                    "The request timed out.",
                    "The server did not complete the request within the allowed time."),
            });

        // Brotli/gzip. Responses never mix secrets with attacker-controlled reflected input,
        // so compression over HTTPS is acceptable (BREACH).
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/problem+json"]);
        });
        services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
        services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
    }

    /// <summary>The outer middleware: must run before logging, authentication and endpoints.</summary>
    public static WebApplication UseHttpPlatform(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseExceptionHandler();
        app.UseStatusCodePages();                   // empty 4xx/5xx (404, 405, 413, …) become problem details

        var http = app.Services.GetRequiredService<IOptions<HttpOptions>>().Value;
        if (!app.Environment.IsDevelopment() && http.EnforceHttps)
        {
            app.UseHsts();

            // Probes and Prometheus scrape plain HTTP inside the cluster; never redirect them.
            app.UseWhen(context => !IsInfrastructurePath(context.Request.Path), branch => branch.UseHttpsRedirection());
        }

        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseResponseCompression();
        return app;
    }

    private static bool IsInfrastructurePath(PathString path)
        => path.StartsWithSegments(ServiceDefaultsExtensions.LivenessPath, StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments(ServiceDefaultsExtensions.ReadinessPath, StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments(ServiceDefaultsExtensions.MetricsPath, StringComparison.OrdinalIgnoreCase);
}
