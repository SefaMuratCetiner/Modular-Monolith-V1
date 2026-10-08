using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Web;

namespace ModularMonolith.Shared.Infrastructure.Tenancy;

/// <summary>
/// Resolves the tenant from the authenticated principal. Must run after authentication.
/// The Host header is client-controlled, so a subdomain is only used to cross-check the claim.
/// </summary>
internal sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        TenantContext tenantContext,
        IOptions<TenancyOptions> options,
        ILogger<TenantResolutionMiddleware> logger)
    {
        var settings = options.Value;
        var claim = context.User.FindFirst(settings.ClaimType)?.Value;

        if (!string.IsNullOrEmpty(claim))
        {
            if (!TenantContext.IsValid(claim))
            {
                await ForbidAsync(context, "The tenant claim is malformed.");
                return;
            }

            var subdomain = ResolveSubdomain(context.Request.Host.Host, settings.RootDomain);
            if (subdomain is not null && !string.Equals(subdomain, claim, StringComparison.OrdinalIgnoreCase))
            {
                await ForbidAsync(context, "The requested tenant does not match the authenticated tenant.");
                return;
            }

            tenantContext.Set(claim);
        }
        else if (settings.AllowDefaultTenant)
        {
            tenantContext.Set(settings.DefaultTenant);
        }
        else if (context.User.Identity?.IsAuthenticated == true)
        {
            await ForbidAsync(context, "The access token does not contain a tenant.");
            return;
        }

        if (!tenantContext.IsResolved)
        {
            await next(context);
            return;
        }

        // Tenant on every log line (Serilog picks up logger scopes) and on the request span.
        Activity.Current?.SetTag(TenantTelemetry.TagName, tenantContext.TenantId);
        using (logger.BeginScope(TenantTelemetry.LogScope(tenantContext.TenantId)))
        {
            await next(context);
        }
    }

    private static string? ResolveSubdomain(string host, string? rootDomain)
    {
        if (string.IsNullOrWhiteSpace(rootDomain))
        {
            return null;
        }

        var suffix = "." + rootDomain;
        if (!host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var prefix = host[..^suffix.Length];
        var dot = prefix.LastIndexOf('.');
        return dot < 0 ? prefix : prefix[(dot + 1)..];
    }

    private static Task ForbidAsync(HttpContext context, string detail)
        => ProblemResponses.WriteAsync(context, StatusCodes.Status403Forbidden, "The tenant could not be resolved.", detail);
}
