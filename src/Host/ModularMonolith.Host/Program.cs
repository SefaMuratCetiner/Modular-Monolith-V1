using System.Diagnostics;
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
builder.AddSecurity();                      // JWT (API) + cookie/OIDC (browser), authorization, rate limits
builder.AddBackgroundJobs();                // Hangfire on PostgreSQL (outbox dispatch)
builder.Services.AddSharedInfrastructure(builder.Configuration, modules);
builder.Services.AddFeatureManagement();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier);
builder.Services.AddFastEndpoints(options =>
{
    options.Assemblies = modules.Select(module => module.Assembly);
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

app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseConfiguredRequestLocalization();
app.UseAuthentication();
app.UseTenantResolution();                  // after authentication: tenant comes from the token
app.UseRateLimiter();                       // after tenancy: partitions by tenant + user
app.UseAuthorization();
app.UseFastEndpoints(config =>
{
    config.Endpoints.RoutePrefix = "api";
    config.Versioning.Prefix = "v";
    config.Versioning.PrependToRoute = true;
    config.Versioning.DefaultVersion = 1;
});
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerGen();
}

app.MapDefaultEndpoints();                  // /health/live, /health/ready, /metrics (anonymous)
app.MapJobDashboard();                      // /hangfire (admin role)

await app.Services.InitializeModulesAsync(app.Lifetime.ApplicationStopping);
await app.RunAsync();
