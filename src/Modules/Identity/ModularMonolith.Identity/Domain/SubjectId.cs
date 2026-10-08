using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Identity.Domain;

/// <summary>Value object: the identity provider's stable subject (<c>sub</c> claim) of a user.</summary>
internal sealed record SubjectId
{
    public const int MaxLength = 255;

    private SubjectId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<SubjectId> Create(string? value)
        => string.IsNullOrWhiteSpace(value) || value.Length > MaxLength
            ? Result.Failure<SubjectId>(IdentityErrors.NotAuthenticated)
            : Result.Success(new SubjectId(value));

    /// <inheritdoc />
    public override string ToString() => Value;
}
