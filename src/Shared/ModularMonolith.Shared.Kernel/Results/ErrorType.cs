namespace ModularMonolith.Shared.Kernel.Results;

/// <summary>
/// Classifies an <see cref="Error"/> so the web layer can map it to an HTTP status code.
/// </summary>
public enum ErrorType
{
    /// <summary>Unexpected failure (500).</summary>
    Failure = 0,

    /// <summary>Input or invariant violation (400).</summary>
    Validation = 1,

    /// <summary>Resource does not exist (404).</summary>
    NotFound = 2,

    /// <summary>State conflict such as a duplicate key (409).</summary>
    Conflict = 3,

    /// <summary>Caller is not allowed to perform the operation (403).</summary>
    Forbidden = 4,

    /// <summary>Operation temporarily unavailable, e.g. disabled by a feature flag (503).</summary>
    Unavailable = 5,
}
