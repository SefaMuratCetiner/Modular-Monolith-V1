using Dapper;
using Microsoft.Extensions.Logging;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Privacy;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Privacy.Features.ExportPersonalData;

/// <summary>
/// Right of access and data portability (GDPR Art. 15 and 20). Asks every module's
/// <see cref="IPersonalDataExporter"/> for its part (each reads only its own tables, in the caller's tenant)
/// and records the request. Exporters run sequentially because they share the request's database session.
/// </summary>
internal sealed partial class ExportPersonalDataCommandHandler(
    IEnumerable<IPersonalDataExporter> exporters,
    IDbSession session,
    TimeProvider timeProvider,
    ILogger<ExportPersonalDataCommandHandler> logger) : ICommandHandler<ExportPersonalDataCommand, PersonalDataExport>
{
    public async Task<Result<PersonalDataExport>> HandleAsync(ExportPersonalDataCommand command, CancellationToken cancellationToken)
    {
        var sections = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var exporter in exporters)
        {
            sections[exporter.Section] = await exporter.ExportAsync(command.SubjectId, cancellationToken);
        }

        var now = timeProvider.GetUtcNow();
        var connection = await session.GetConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            insert into privacy.data_subject_requests (id, subject_id, request_type, requested_at, completed_at)
            values (@Id, @SubjectId, @RequestType, @Now, @Now)
            """,
            new { Id = Guid.CreateVersion7(now), command.SubjectId, RequestType = DataSubjectRequestTypes.Export, Now = now },
            session.Transaction,
            cancellationToken: cancellationToken));

        LogExported(logger, command.SubjectId, sections.Count);
        return Result.Success(new PersonalDataExport(command.SubjectId, now, sections));
    }

    [LoggerMessage(EventId = 4000, Level = LogLevel.Information, Message = "Personal data of subject {SubjectId} exported ({SectionCount} module sections)")]
    private static partial void LogExported(ILogger logger, string subjectId, int sectionCount);
}
