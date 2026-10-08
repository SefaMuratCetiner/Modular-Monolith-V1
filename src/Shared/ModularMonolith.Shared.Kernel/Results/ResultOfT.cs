namespace ModularMonolith.Shared.Kernel.Results;

/// <summary>
/// Outcome of an operation that produces a value on success.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>Gets the value. Throws when the result is a failure.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot access the value of a failed result ({Error.Code}).");
}
