namespace ModularMonolith.Shared.Kernel.Domain;

/// <summary>
/// Entity that records who created and last modified it.
/// Values are stamped by infrastructure from <c>ICurrentUser</c> and <see cref="TimeProvider"/>.
/// </summary>
public interface IAuditable
{
    /// <summary>Gets the identifier of the user who created the entity.</summary>
    string CreatedBy { get; }

    /// <summary>Gets the creation instant.</summary>
    DateTimeOffset CreatedAt { get; }

    /// <summary>Gets the identifier of the user who last modified the entity.</summary>
    string? ModifiedBy { get; }

    /// <summary>Gets the last modification instant.</summary>
    DateTimeOffset? ModifiedAt { get; }

    /// <summary>Records creation metadata.</summary>
    /// <param name="userId">The acting user.</param>
    /// <param name="at">The creation instant.</param>
    void MarkCreated(string userId, DateTimeOffset at);

    /// <summary>Records modification metadata.</summary>
    /// <param name="userId">The acting user.</param>
    /// <param name="at">The modification instant.</param>
    void MarkModified(string userId, DateTimeOffset at);
}
