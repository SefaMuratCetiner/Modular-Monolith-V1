namespace ModularMonolith.Shared.Infrastructure.Data;

/// <summary>
/// A before/after snapshot of one entity changed by a command, written to <c>audit_log.changes</c>.
/// Snapshots are small, explicit records chosen by the repository (never the whole aggregate),
/// so secrets and irrelevant fields stay out of the audit trail.
/// </summary>
/// <param name="EntityType">The entity type, e.g. <c>product</c>.</param>
/// <param name="EntityId">The entity identifier.</param>
/// <param name="Operation">The operation: <c>create</c>, <c>update</c> or <c>delete</c>.</param>
/// <param name="Before">The state before the change (<see langword="null"/> for creates).</param>
/// <param name="After">The state after the change (<see langword="null"/> for deletes).</param>
public sealed record AuditChange(string EntityType, string EntityId, string Operation, object? Before, object? After)
{
    /// <summary>The create operation name.</summary>
    public const string Create = "create";

    /// <summary>The update operation name.</summary>
    public const string Update = "update";

    /// <summary>The delete operation name.</summary>
    public const string Delete = "delete";
}
