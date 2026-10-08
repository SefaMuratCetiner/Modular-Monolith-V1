using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ModularMonolith.Shared.Infrastructure.Caching;
using ModularMonolith.Shared.Infrastructure.Serialization;
using StackExchange.Redis;

namespace ModularMonolith.Shared.Infrastructure.Idempotency;

/// <summary>
/// Valkey-backed idempotency records. A key is claimed atomically (<c>SET NX</c>), completed with
/// the response on success, and released on failure so that the client may retry.
/// Record keys are <c>mm:idempotency:v1:{tenant}:{sha256(user, method, path, client key)}</c>:
/// the client-supplied key never appears verbatim.
/// </summary>
public sealed class IdempotencyStore(ValkeyConnection valkey)
{
    private static readonly TimeSpan InFlightExpiry = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan CompletedExpiry = TimeSpan.FromHours(24);

    internal static string BuildKey(string tenant, string user, string method, string path, string clientKey)
    {
        var material = string.Join('\n', user, method, path, clientKey);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
        return $"{CacheKeyFactory.Prefix}:idempotency:v1:{tenant}:{hash}";
    }

    internal async Task<IdempotencyBeginResult> BeginAsync(string key, string fingerprint)
    {
        var database = valkey.Multiplexer.GetDatabase();
        var inFlight = JsonSerializer.Serialize(new IdempotencyRecord(fingerprint, null, null), JsonDefaults.Options);

        // Two attempts handle the race where the existing key expires between SET NX and GET.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (await database.StringSetAsync(key, inFlight, InFlightExpiry, When.NotExists))
            {
                return IdempotencyBeginResult.Started;
            }

            var existing = await database.StringGetAsync(key);
            if (!existing.HasValue)
            {
                continue;
            }

            var record = JsonSerializer.Deserialize<IdempotencyRecord>(existing.ToString(), JsonDefaults.Options);
            if (record is null || !string.Equals(record.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                return IdempotencyBeginResult.Mismatch;
            }

            return record is { StatusCode: { } status, Body: { } body }
                ? IdempotencyBeginResult.Replay(status, body)
                : IdempotencyBeginResult.InProgress;
        }

        return IdempotencyBeginResult.InProgress;
    }

    internal Task CompleteAsync(string key, string fingerprint, int statusCode, string body)
        => valkey.Multiplexer.GetDatabase().StringSetAsync(
            key,
            JsonSerializer.Serialize(new IdempotencyRecord(fingerprint, statusCode, body), JsonDefaults.Options),
            CompletedExpiry);

    internal Task ReleaseAsync(string key) => valkey.Multiplexer.GetDatabase().KeyDeleteAsync(key);

    private sealed record IdempotencyRecord(string Fingerprint, int? StatusCode, string? Body);
}
