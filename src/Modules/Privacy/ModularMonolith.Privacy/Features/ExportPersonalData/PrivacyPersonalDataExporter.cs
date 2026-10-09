using Dapper;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Kernel.Privacy;

namespace ModularMonolith.Privacy.Features.ExportPersonalData;

/// <summary>Privacy's own part of an export: the user's earlier data-subject requests.</summary>
internal sealed class PrivacyPersonalDataExporter(IDbSession session) : IPersonalDataExporter
{
    public string Section => "privacy";

    public async Task<object?> ExportAsync(string subjectId, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        var requests = await connection.QueryAsync<DataSubjectRequest>(new CommandDefinition(
            """
            select request_type as Type, requested_at as RequestedAt, completed_at as CompletedAt
            from privacy.data_subject_requests
            where subject_id = @SubjectId
            order by requested_at, id
            """,
            new { SubjectId = subjectId },
            cancellationToken: cancellationToken));

        var list = requests.AsList();
        return list.Count == 0 ? null : list;
    }

    /// <summary>One earlier request.</summary>
    private sealed record DataSubjectRequest(string Type, DateTimeOffset RequestedAt, DateTimeOffset? CompletedAt);
}
