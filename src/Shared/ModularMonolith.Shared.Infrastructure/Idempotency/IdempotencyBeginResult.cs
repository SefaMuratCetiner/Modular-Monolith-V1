namespace ModularMonolith.Shared.Infrastructure.Idempotency;

/// <summary>Outcome of claiming an idempotency key.</summary>
internal sealed record IdempotencyBeginResult(IdempotencyState State, int StatusCode = 0, string? Body = null)
{
    public static IdempotencyBeginResult Started { get; } = new(IdempotencyState.Started);

    public static IdempotencyBeginResult InProgress { get; } = new(IdempotencyState.InProgress);

    public static IdempotencyBeginResult Mismatch { get; } = new(IdempotencyState.Mismatch);

    public static IdempotencyBeginResult Replay(int statusCode, string body) => new(IdempotencyState.Replay, statusCode, body);
}

/// <summary>Idempotency key states.</summary>
internal enum IdempotencyState
{
    /// <summary>The key was claimed by this request.</summary>
    Started = 0,

    /// <summary>Another request with the same key is still running.</summary>
    InProgress = 1,

    /// <summary>The key was used with a different payload.</summary>
    Mismatch = 2,

    /// <summary>The key completed earlier; the stored response is replayed.</summary>
    Replay = 3,
}
