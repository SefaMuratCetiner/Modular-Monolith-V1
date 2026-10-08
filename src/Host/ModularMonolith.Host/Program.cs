using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.FeatureManagement;
using ModularMonolith.Catalog;
using ModularMonolith.Host.Configuration;
using ModularMonolith.Identity;
using ModularMonolith.ServiceDefaults;
using ModularMonolith.Shared.Infrastructure;
using ModularMonolith.Shared.Infrastructure.Modules;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// The modules composed into this host. This is the ONLY place the host knows about modules.
IModule[] modules = [new CatalogModule(), new IdentityModule()];

builder.AddServiceDefaults();               // OpenTelemetry, health checks, resilient HttpClient
builder.AddLogging();                       // Serilog -> console, Loki, OTLP
builder.AddHttpPlatform();                  // Kestrel limits, proxy headers, HTTPS/HSTS, JSON, problem details, timeouts, compression
builder.AddSecurity();                      // JWT (API) + cookie/OIDC (browser), authorization, rate limits
builder.AddBackgroundJobs();                // Hangfire on PostgreSQL (outbox dispatch)
builder.Services.AddSharedInfrastructure(builder.Configuration, modules);
builder.Services.AddFeatureManagement();
builder.Services.AddFastEndpoints(options =>
{
    // Module endpoints plus the host's own operator endpoints (Operations/).
    options.Assemblies = [.. modules.Select(module => module.Assembly), typeof(ApiConventions).Assembly];
    options.DisableAutoDiscovery = true;
});
if (builder.Environment.IsDevelopment())
{
    builder.Services.SwaggerDocument(options =>
    {
        options.MaxEndpointVersion = 1;
        options.DocumentSettings = settings =>
        {
            settings.Title = "ModularMonolith API";
            settings.Version = "v1";
        };
    });
}

var app = builder.Build();

app.UseHttpPlatform();                      // forwarded headers, exception handler, HSTS/HTTPS, security headers, compression
app.UseSerilogRequestLogging();
app.UseConfiguredRequestLocalization();
app.UseAuthentication();
app.UseTenantResolution();                  // after authentication: tenant comes from the token
app.UseRequestTimeouts();
app.UseRateLimiter();                       // after tenancy: partitions by tenant + user
app.UseAuthorization();
app.UseFastEndpoints(ApiConventions.Apply); // /api/v{n}/..., shared JSON, problem-details validation errors
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerGen();                    // contract: /swagger/v1/swagger.json, UI: /swagger
}

app.MapDefaultEndpoints();                  // /health/live, /health/ready, /metrics (anonymous)
app.MapJobDashboard();                      // /hangfire (operator role)

await app.Services.InitializeModulesAsync(app.Lifetime.ApplicationStopping);
await app.RunAsync();
