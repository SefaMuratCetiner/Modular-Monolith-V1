namespace ModularMonolith.Shared.Kernel.Domain;

/// <summary>
/// Optimistic concurrency. Every update is conditional on the version the caller loaded
/// (<c>... where id = @Id and version = @Version</c>) and increments it; zero affected rows means
/// someone else changed the entity first and the caller gets a 409 conflict.
/// </summary>
public interface IVersioned
{
    /// <summary>Gets the version the entity had when it was loaded (1 for a new entity).</summary>
    int Version { get; }
}
