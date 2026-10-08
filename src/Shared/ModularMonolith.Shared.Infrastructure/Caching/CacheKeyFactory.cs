using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using ModularMonolith.Shared.Kernel.Context;

namespace ModularMonolith.Shared.Infrastructure.Caching;

/// <summary>
/// The single place where cache keys are built. Deterministic, versioned and environment-prefixed;
/// tenant-scoped keys always contain the tenant, shared keys never do.
/// </summary>
public sealed partial class CacheKeyFactory(IHostEnvironment environment)
{
    /// <summary>The application prefix of every key.</summary>
    public const string Prefix = "mm";

    private const int MaxItemLength = 100;

    private readonly string _environment = Normalize(environment.EnvironmentName);

    /// <summary>Builds the key of the generation counter of an invalidatable region.</summary>
    /// <param name="region">The region.</param>
    /// <param name="tenant">The tenant context (used for tenant-scoped regions).</param>
    /// <returns>The key.</returns>
    public string Generation(CacheRegion region, ITenantContext tenant)
        => $"{RegionPrefix(region, tenant)}:generation";

    /// <summary>Builds the full key of an entry.</summary>
    /// <param name="key">The logical key.</param>
    /// <param name="tenant">The tenant context (used for tenant-scoped regions).</param>
    /// <param name="generation">The region generation, or <see langword="null"/> for non-invalidatable regions.</param>
    /// <returns>The key.</returns>
    public string Entry(CacheKey key, ITenantContext tenant, long? generation)
    {
        ArgumentNullException.ThrowIfNull(key);
        var generationSegment = generation is { } value ? $":g{value.ToString(CultureInfo.InvariantCulture)}" : string.Empty;
        return $"{RegionPrefix(key.Region, tenant)}{generationSegment}:{SafeItem(key.Item)}";
    }

    private string RegionPrefix(CacheRegion region, ITenantContext tenant)
    {
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(tenant);
        var scope = region.Scope == CacheScope.Shared ? "shared" : $"t:{tenant.TenantId}";
        return $"{Prefix}:{_environment}:{scope}:{Segment(region.Module)}:{Segment(region.Resource)}:s{region.SchemaVersion.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string Segment(string value)
        => SegmentPattern().IsMatch(value)
            ? value
            : throw new ArgumentException($"Cache key segment '{value}' must match [a-z0-9-]+.", nameof(value));

    private static string SafeItem(string item)
        => item.Length <= MaxItemLength && ItemPattern().IsMatch(item)
            ? item
            : "h:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(item)));

    private static string Normalize(string environmentName)
    {
        var lower = environmentName.ToLowerInvariant();
        return SegmentPattern().IsMatch(lower) ? lower : "default";
    }

    [GeneratedRegex("^[a-z0-9-]{1,40}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SegmentPattern();

    [GeneratedRegex("^[A-Za-z0-9:._-]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ItemPattern();
}
