using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.FeatureManagement;
using ModularMonolith.Catalog;
using ModularMonolith.Host.Configuration;
using ModularMonolith.Host.Migrations;
using ModularMonolith.Host.OpenApi;
using ModularMonolith.Identity;
using ModularMonolith.Messaging;
using ModularMonolith.Privacy;
using ModularMonolith.ServiceDefaults;
using ModularMonolith.Shared.Infrastructure;
using ModularMonolith.Shared.Infrastructure.Modules;
using Serilog;

// `healthcheck`: container readiness probe for the shell-less (chiseled) image; nothing else is built.
if (args is [HealthProbeCommand.Name, ..])
{
    return await HealthProbeCommand.RunAsync(args);
}

// The modules composed into this host. This is the ONLY place the host knows about modules.
IModule[] modules = [new CatalogModule(), new IdentityModule(), new PrivacyModule(), new MessagingModule()];

// `migrate`: apply database migrations with the schema owner's credentials and exit.
// The serving application below never runs DDL.
if (args is [MigrationCommand.Name, ..])
{
    return await MigrationCommand.RunAsync(args[1..], modules);
}

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddSecretFiles();     // secret store: one file per key in /run/secrets (overrides all sources)

builder.AddServiceDefaults();               // OpenTelemetry, health checks, resilient HttpClient
builder.AddLogging();                       // Serilog -> console, Loki, OTLP
builder.AddHttpPlatform();                  // Kestrel limits, proxy headers, HTTPS/HSTS, JSON, problem details, timeouts,
                                            // compression, graceful shutdown (readiness drain)
builder.AddSecurity();                      // JWT (API) + cookie/OIDC (browser), authorization, rate limits
builder.AddKeyRing();                       // Data Protection keys in PostgreSQL, encrypted at rest
builder.AddBackgroundJobs();                // Hangfire on PostgreSQL (outbox dispatch)
builder.Services.AddSharedInfrastructure(builder.Configuration, modules);
builder.Services.AddFeatureManagement();
builder.Services.AddFastEndpoints(options =>
{
    // Every endpoint lives in a module; the host has none.
    options.Assemblies = [.. modules.Select(module => module.Assembly)];
    options.DisableAutoDiscovery = true;
});

// The OpenAPI generator is registered everywhere (the `openapi` command needs it); the Swagger UI and
// the JSON endpoint are only served in Development (see UseSwaggerGen below).
builder.Services.SwaggerDocument(options =>
{
    options.MaxEndpointVersion = 1;
    options.DocumentSettings = settings =>
    {
        settings.DocumentName = OpenApiCommand.DocumentName;
        settings.Title = "ModularMonolith API";
        settings.Version = "1";
    };
});

var app = builder.Build();

app.UseHttpPlatform();                      // forwarded headers, exception handler, HSTS/HTTPS, security headers, compression
app.UseSerilogRequestLogging();
app.UseConfiguredRequestLocalization();
app.UseAuthentication();
app.UseTenantResolution();                  // after authentication: tenant comes from the token
app.UseRequestTimeouts();
app.UseRateLimiter();                       // after tenancy: partitions by tenant + user
app.UseAuthorization();
app.UseOutputCache();                       // after authorization: only authorized requests reach the cache
app.UseFastEndpoints(ApiConventions.Apply); // /api/v{n}/..., shared JSON, problem-details validation errors

// `openapi [--check] [path]`: export (or verify) the committed OpenAPI contract and exit; nothing is started.
if (OpenApiCommand.IsRequested(args))
{
    return await OpenApiCommand.RunAsync(app, args);
}
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerGen();                    // contract: /swagger/v1/swagger.json, UI: /swagger
}

app.MapDefaultEndpoints();                  // /health/live, /health/ready, /metrics (anonymous)
app.MapJobDashboard();                      // /hangfire (operator role)

await app.Services.InitializeModulesAsync(app.Lifetime.ApplicationStopping);
await app.RunAsync();
return 0;
