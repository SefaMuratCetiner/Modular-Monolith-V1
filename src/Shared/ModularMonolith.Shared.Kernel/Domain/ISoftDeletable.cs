namespace ModularMonolith.Shared.Kernel.Domain;

/// <summary>
/// Entity that is hidden instead of physically deleted.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>Gets a value indicating whether the entity is deleted.</summary>
    bool IsDeleted { get; }

    /// <summary>Marks the entity as deleted.</summary>
    void MarkDeleted();
}
