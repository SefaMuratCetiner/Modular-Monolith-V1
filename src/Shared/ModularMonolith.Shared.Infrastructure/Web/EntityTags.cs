using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using ModularMonolith.Shared.Infrastructure.Serialization;

namespace ModularMonolith.Shared.Infrastructure.Web;

/// <summary>
/// ETags and conditional requests (RFC 9110).
/// <list type="bullet">
/// <item>ETag format: <c>"{version}-{fingerprint}"</c>. The version is the entity's optimistic-concurrency
/// version; the fingerprint is a hash of the whole representation, so data joined from other tables
/// (e.g. a seller name) also changes the tag.</item>
/// <item><c>If-None-Match</c> on reads returns 304 when the representation is unchanged.</item>
/// <item><c>If-Match</c> on writes carries the expected version (412 when it is stale).</item>
/// </list>
/// </summary>
public static class EntityTags
{
    private const int FingerprintLength = 16;

    /// <summary>Builds the strong ETag of a versioned representation.</summary>
    /// <typeparam name="T">The representation type.</typeparam>
    /// <param name="version">The entity version.</param>
    /// <param name="representation">The response body.</param>
    /// <returns>The ETag.</returns>
    public static EntityTagHeaderValue For<T>(int version, T representation)
    {
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(representation, JsonDefaults.Options));
        var fingerprint = Convert.ToHexStringLower(hash)[..FingerprintLength];
        return new EntityTagHeaderValue($"\"{version.ToString(CultureInfo.InvariantCulture)}-{fingerprint}\"");
    }

    /// <summary>Checks whether the request's <c>If-None-Match</c> matches the current ETag (weak comparison).</summary>
    /// <param name="request">The request.</param>
    /// <param name="current">The current ETag.</param>
    /// <returns><see langword="true"/> when a 304 Not Modified may be returned.</returns>
    public static bool IsNotModified(HttpRequest request, EntityTagHeaderValue current)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.GetTypedHeaders().IfNoneMatch.Any(tag => tag.Equals(EntityTagHeaderValue.Any) || tag.Compare(current, useStrongComparison: false));
    }

    /// <summary>Reads the expected version from <c>If-Match</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The precondition carried by the header.</returns>
    public static IfMatchPrecondition ReadIfMatch(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tags = request.GetTypedHeaders().IfMatch;
        if (tags.Count == 0)
        {
            return IfMatchPrecondition.None;
        }

        if (tags.Count != 1 || tags[0].IsWeak || tags[0].Equals(EntityTagHeaderValue.Any))
        {
            return IfMatchPrecondition.Malformed;
        }

        var value = tags[0].Tag.Value?.Trim('"') ?? string.Empty;
        var dash = value.IndexOf('-', StringComparison.Ordinal);
        var versionText = dash < 0 ? value : value[..dash];
        return int.TryParse(versionText, NumberStyles.None, CultureInfo.InvariantCulture, out var version) && version > 0
            ? new IfMatchPrecondition(IsPresent: true, IsMalformed: false, version)
            : IfMatchPrecondition.Malformed;
    }
}

/// <summary>The outcome of reading an <c>If-Match</c> header.</summary>
/// <param name="IsPresent">Whether the header was sent.</param>
/// <param name="IsMalformed">Whether it could not be interpreted as one strong version tag.</param>
/// <param name="Version">The expected version.</param>
public sealed record IfMatchPrecondition(bool IsPresent, bool IsMalformed, int Version)
{
    /// <summary>Gets the result for a request without <c>If-Match</c>.</summary>
    public static IfMatchPrecondition None { get; } = new(IsPresent: false, IsMalformed: false, 0);

    /// <summary>Gets the result for an unusable <c>If-Match</c> (weak, wildcard, several tags, no version).</summary>
    public static IfMatchPrecondition Malformed { get; } = new(IsPresent: true, IsMalformed: true, 0);
}
