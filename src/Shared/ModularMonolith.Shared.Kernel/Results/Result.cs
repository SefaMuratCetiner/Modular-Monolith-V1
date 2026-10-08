namespace ModularMonolith.Shared.Kernel.Results;

/// <summary>
/// Outcome of an operation that either succeeds or fails with an <see cref="Results.Error"/>.
/// </summary>
public class Result
{
    /// <summary>Initializes a new instance of the <see cref="Result"/> class.</summary>
    /// <param name="isSuccess">Whether the operation succeeded.</param>
    /// <param name="error">The error, or <see cref="Error.None"/> on success.</param>
    protected Result(bool isSuccess, Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (isSuccess != (error == Error.None))
        {
            throw new ArgumentException("A successful result cannot carry an error and vice versa.", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets a value indicating whether the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>Gets the error (or <see cref="Error.None"/>).</summary>
    public Error Error { get; }

    /// <summary>Creates a successful result.</summary>
    /// <returns>The result.</returns>
    public static Result Success() => new(isSuccess: true, Error.None);

    /// <summary>Creates a failed result.</summary>
    /// <param name="error">The error.</param>
    /// <returns>The result.</returns>
    public static Result Failure(Error error) => new(isSuccess: false, error);

    /// <summary>Creates a successful result with a value.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The result.</returns>
    public static Result<T> Success<T>(T value) => new(value, isSuccess: true, Error.None);

    /// <summary>Creates a failed result for a value-returning operation.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="error">The error.</param>
    /// <returns>The result.</returns>
    public static Result<T> Failure<T>(Error error) => new(default, isSuccess: false, error);
}
