namespace ModularMonolith.Shared.Kernel.Privacy;

/// <summary>
/// Implemented by every module that stores personal data, for the right of access and data portability
/// (GDPR Art. 15 and 20). Each module exports only its own tables; the host combines the results.
/// Implementations are internal classes discovered by assembly scanning and run in the caller's tenant
/// (row-level security applies). Erasure is event-driven instead: Identity publishes
/// <c>identity.user-erased</c> and each module anonymizes its own copies.
/// </summary>
public interface IPersonalDataExporter
{
    /// <summary>Gets the section name in the export, e.g. <c>catalog</c>.</summary>
    string Section { get; }

    /// <summary>Exports everything this module stores about a user.</summary>
    /// <param name="subjectId">The identity provider subject (<c>sub</c>) of the user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A JSON-serializable object, or <see langword="null"/> when the module holds nothing.</returns>
    Task<object?> ExportAsync(string subjectId, CancellationToken cancellationToken);
}
