namespace ModularMonolith.Shared.Kernel.Domain;

/// <summary>
/// Base entity with a strongly typed identity, an optimistic-concurrency version, audit metadata
/// and soft delete.
/// Note: there is no TenantId here. Tenancy is an infrastructure concern enforced by
/// PostgreSQL row-level security, so the domain model stays tenant-agnostic.
/// </summary>
/// <typeparam name="TId">The strongly typed identifier, e.g. <c>ProductId</c>.</typeparam>
public abstract class Entity<TId> : IAuditable, ISoftDeletable, IVersioned
    where TId : struct, IEquatable<TId>
{
    /// <summary>The version of an entity that has never been persisted.</summary>
    public const int InitialVersion = 1;

    /// <summary>Initializes a new instance of the <see cref="Entity{TId}"/> class.</summary>
    /// <param name="id">The entity identifier.</param>
    protected Entity(TId id)
    {
        Id = id;
    }

    /// <summary>Gets the entity identifier.</summary>
    public TId Id { get; }

    /// <inheritdoc />
    public int Version { get; private set; } = InitialVersion;

    /// <inheritdoc />
    public string CreatedBy { get; private set; } = string.Empty;

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; private set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAt { get; private set; }

    /// <inheritdoc />
    public bool IsDeleted { get; private set; }

    /// <inheritdoc />
    public void MarkCreated(string userId, DateTimeOffset at)
    {
        CreatedBy = userId;
        CreatedAt = at;
    }

    /// <inheritdoc />
    public void MarkModified(string userId, DateTimeOffset at)
    {
        ModifiedBy = userId;
        ModifiedAt = at;
    }

    /// <inheritdoc />
    public void MarkDeleted() => IsDeleted = true;

    /// <summary>Restores persisted state when an entity is loaded from storage.</summary>
    /// <param name="version">The stored concurrency version.</param>
    /// <param name="createdBy">The creator.</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <param name="modifiedBy">The last modifier.</param>
    /// <param name="modifiedAt">The last modification instant.</param>
    protected void RestorePersistedState(int version, string createdBy, DateTimeOffset createdAt, string? modifiedBy, DateTimeOffset? modifiedAt)
    {
        Version = version;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        ModifiedBy = modifiedBy;
        ModifiedAt = modifiedAt;
    }
}
