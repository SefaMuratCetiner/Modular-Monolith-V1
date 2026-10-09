namespace ModularMonolith.Privacy.Features.ExportPersonalData;

/// <summary>The export document: one section per module.</summary>
/// <param name="SubjectId">The identity provider subject of the user.</param>
/// <param name="ExportedAt">When the export was produced.</param>
/// <param name="Sections">Module sections (<see langword="null"/> when a module holds nothing).</param>
internal sealed record PersonalDataExport(string SubjectId, DateTimeOffset ExportedAt, IReadOnlyDictionary<string, object?> Sections);
