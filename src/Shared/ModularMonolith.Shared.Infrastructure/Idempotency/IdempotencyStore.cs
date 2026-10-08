using System.Text.Json;
using ModularMonolith.Shared.Infrastructure.Serialization;
using StackExchange.Redis;

namespace ModularMonolith.Shared.Infrastructure.Idempotency;

/// <summary>
/// Valkey-backed idempotency records. A key is claimed atomically (<c>SET NX</c>), completed with
/// the response on success, and released on failure so that the client may retry.
/// </summary>
internal sealed class IdempotencyStore(IConnectionMultiplexer connection)
{
    private static readonly TimeSpan InFlightExpiry = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan CompletedExpiry = TimeSpan.FromHours(24);

    public async Task<IdempotencyBeginResult> BeginAsync(string key, string fingerprint)
    {
        var database = connection.GetDatabase();
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

    public Task CompleteAsync(string key, string fingerprint, int statusCode, string body)
        => connection.GetDatabase().StringSetAsync(
            key,
            JsonSerializer.Serialize(new IdempotencyRecord(fingerprint, statusCode, body), JsonDefaults.Options),
            CompletedExpiry);

    public Task ReleaseAsync(string key) => connection.GetDatabase().KeyDeleteAsync(key);

    private sealed record IdempotencyRecord(string Fingerprint, int? StatusCode, string? Body);
}
