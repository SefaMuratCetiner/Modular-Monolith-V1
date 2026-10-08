namespace ModularMonolith.Shared.Kernel.Domain;

/// <summary>
/// Base entity with identity, audit metadata and soft delete.
/// Note: there is no TenantId here. Tenancy is an infrastructure concern enforced by
/// PostgreSQL row-level security, so the domain model stays tenant-agnostic.
/// </summary>
public abstract class Entity : IAuditable, ISoftDeletable
{
    /// <summary>Initializes a new instance of the <see cref="Entity"/> class.</summary>
    /// <param name="id">The entity identifier.</param>
    protected Entity(Guid id)
    {
        Id = id;
    }

    /// <summary>Gets the entity identifier.</summary>
    public Guid Id { get; }

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
}
