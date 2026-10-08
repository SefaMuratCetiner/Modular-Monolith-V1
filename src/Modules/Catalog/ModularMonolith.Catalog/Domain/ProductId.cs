namespace ModularMonolith.Catalog.Domain;

/// <summary>
/// Strongly typed product identifier (UUIDv7: time-ordered, index friendly). Prevents mixing up
/// product ids with other ids at compile time; converted to <see cref="Guid"/> only at the
/// HTTP/database/contract boundaries.
/// </summary>
/// <param name="Value">The underlying identifier.</param>
internal readonly record struct ProductId(Guid Value)
{
    /// <summary>Creates a new, time-ordered identifier.</summary>
    public static ProductId New(DateTimeOffset now) => new(Guid.CreateVersion7(now));

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
