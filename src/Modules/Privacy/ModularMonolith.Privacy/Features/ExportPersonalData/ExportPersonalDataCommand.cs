using ModularMonolith.Shared.Kernel.Cqrs;

namespace ModularMonolith.Privacy.Features.ExportPersonalData;

/// <summary>
/// Exports everything the application stores about a user. A command, not a query: the request itself is
/// recorded (<c>privacy.data_subject_requests</c>) and audited in the same transaction.
/// </summary>
/// <param name="SubjectId">The identity provider subject (<c>sub</c>) of the user.</param>
internal sealed record ExportPersonalDataCommand(string SubjectId) : ICommand<PersonalDataExport>;
