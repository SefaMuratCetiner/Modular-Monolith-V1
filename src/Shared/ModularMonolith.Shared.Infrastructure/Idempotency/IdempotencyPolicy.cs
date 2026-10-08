namespace ModularMonolith.Shared.Infrastructure.Idempotency;

/// <summary>
/// Per-endpoint idempotency behaviour, attached as endpoint metadata:
/// <code>Options(b => b.WithMetadata(IdempotencyPolicy.Critical));</code>
/// Endpoints without metadata use <see cref="Default"/>.
/// </summary>
/// <param name="RequireKey">When true, a missing <c>Idempotency-Key</c> header is rejected with 400.</param>
/// <param name="FailClosed">
/// When true and the idempotency store (Valkey) is unavailable, the request is rejected with 503
/// instead of being processed without duplicate protection.
/// </param>
public sealed record IdempotencyPolicy(bool RequireKey, bool FailClosed)
{
    /// <summary>Gets the default policy: the header is optional and an outage of the store fails open.</summary>
    public static IdempotencyPolicy Default { get; } = new(RequireKey: false, FailClosed: false);

    /// <summary>
    /// Gets the policy for operations where a duplicate is costly (payments, orders): the header is
    /// still optional, but requests carrying it are never processed without protection.
    /// </summary>
    public static IdempotencyPolicy Critical { get; } = new(RequireKey: false, FailClosed: true);

    /// <summary>Gets the strictest policy: the header is mandatory and the store must be available.</summary>
    public static IdempotencyPolicy Required { get; } = new(RequireKey: true, FailClosed: true);
}
