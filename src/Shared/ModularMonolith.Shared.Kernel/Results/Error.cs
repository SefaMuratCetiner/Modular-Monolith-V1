namespace ModularMonolith.Shared.Kernel.Results;

/// <summary>
/// An expected, typed failure. Handlers return errors instead of throwing exceptions
/// for business outcomes; exceptions are reserved for truly exceptional situations.
/// </summary>
/// <param name="Code">Stable machine-readable code, e.g. <c>catalog.sku_taken</c>.</param>
/// <param name="Message">Human-readable description.</param>
/// <param name="Type">Error category.</param>
public record Error(string Code, string Message, ErrorType Type)
{
    /// <summary>Gets the "no error" sentinel used by successful results.</summary>
    public static Error None { get; } = new(string.Empty, string.Empty, ErrorType.Failure);

    /// <summary>Creates a validation error.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="message">The message.</param>
    /// <returns>The error.</returns>
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    /// <summary>Creates a not-found error.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="message">The message.</param>
    /// <returns>The error.</returns>
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    /// <summary>Creates a conflict error.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="message">The message.</param>
    /// <returns>The error.</returns>
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    /// <summary>Creates a forbidden error.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="message">The message.</param>
    /// <returns>The error.</returns>
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    /// <summary>Creates an unavailable error.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="message">The message.</param>
    /// <returns>The error.</returns>
    public static Error Unavailable(string code, string message) => new(code, message, ErrorType.Unavailable);
}
