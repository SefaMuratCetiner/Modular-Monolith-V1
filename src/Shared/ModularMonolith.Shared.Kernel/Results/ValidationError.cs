namespace ModularMonolith.Shared.Kernel.Results;

/// <summary>
/// Validation failure carrying per-field messages (rendered as RFC 9457 validation problem details).
/// </summary>
/// <param name="Errors">Messages keyed by property name.</param>
public sealed record ValidationError(IReadOnlyDictionary<string, string[]> Errors)
    : Error("validation_failed", "One or more validation errors occurred.", ErrorType.Validation);
