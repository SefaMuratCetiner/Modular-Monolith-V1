namespace ModularMonolith.Shared.Infrastructure.Idempotency;

/// <summary>An idempotency key claimed by the current request.</summary>
/// <param name="Key">The fully scoped Valkey key.</param>
/// <param name="Fingerprint">The SHA-256 request fingerprint.</param>
internal sealed record IdempotencyClaim(string Key, string Fingerprint);
