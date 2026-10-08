namespace ModularMonolith.Identity.Domain;

/// <summary>Strongly typed user profile identifier (UUIDv7).</summary>
/// <param name="Value">The underlying identifier.</param>
internal readonly record struct UserProfileId(Guid Value)
{
    /// <summary>Creates a new, time-ordered identifier.</summary>
    public static UserProfileId New(DateTimeOffset now) => new(Guid.CreateVersion7(now));

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
