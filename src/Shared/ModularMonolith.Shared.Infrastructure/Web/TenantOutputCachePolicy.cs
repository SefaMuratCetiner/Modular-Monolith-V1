using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Shared.Kernel.Context;

namespace ModularMonolith.Shared.Infrastructure.Web;

/// <summary>
/// Output-cache policy for authenticated, tenant-scoped GET responses that are identical for every user
/// of a tenant. ASP.NET Core's default policy never caches authenticated requests; this one does, but
/// varies the cache entry by tenant (and query string) and tags it per tenant for targeted eviction.
/// Only 200 responses are stored. Never use it for user-specific responses.
/// </summary>
/// <param name="resource">The resource name used in the eviction tag, e.g. <c>catalog-prices</c>.</param>
/// <param name="duration">How long a response is served from the cache.</param>
public sealed class TenantOutputCachePolicy(string resource, TimeSpan duration) : IOutputCachePolicy
{
    private const string TenantVaryKey = "tenant";

    /// <summary>Builds the eviction tag of a resource for one tenant.</summary>
    /// <param name="resource">The resource name.</param>
    /// <param name="tenantId">The tenant id.</param>
    /// <returns>The tag.</returns>
    public static string TagFor(string resource, string tenantId) => $"{resource}:{tenantId}";

    /// <inheritdoc />
    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(context);
        var http = context.HttpContext;
        var tenant = http.RequestServices.GetRequiredService<ITenantContext>();
        var cacheable = tenant.IsResolved && (HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method));

        context.EnableOutputCaching = true;
        context.AllowCacheLookup = cacheable;
        context.AllowCacheStorage = cacheable;
        context.AllowLocking = true; // one request computes a missing entry; concurrent ones wait for it
        if (cacheable)
        {
            context.CacheVaryByRules.VaryByValues[TenantVaryKey] = tenant.TenantId;
            context.CacheVaryByRules.QueryKeys = "*";
            context.Tags.Add(TagFor(resource, tenant.TenantId));
            context.ResponseExpirationTimeSpan = duration;
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellation) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(context);
        var response = context.HttpContext.Response;
        if (response.StatusCode != StatusCodes.Status200OK || response.Headers.ContainsKey("Set-Cookie"))
        {
            context.AllowCacheStorage = false;
        }

        return ValueTask.CompletedTask;
    }
}
