using Dapper;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Kernel.Privacy;

namespace ModularMonolith.Identity.Features.ExportPersonalData;

/// <summary>Identity's part of a personal-data export: the caller's profile in the current tenant.</summary>
internal sealed class IdentityPersonalDataExporter(IDbSession session) : IPersonalDataExporter
{
    public string Section => "identity";

    public async Task<object?> ExportAsync(string subjectId, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<IdentityPersonalData>(new CommandDefinition(
            """
            select id as ProfileId, subject_id as SubjectId, email as Email, display_name as DisplayName,
                   is_beta_user as IsBetaUser, created_at as RegisteredAt, modified_at as ModifiedAt
            from identity.user_profiles
            where subject_id = @SubjectId and not is_deleted
            """,
            new { SubjectId = subjectId },
            cancellationToken: cancellationToken));
    }
}

/// <summary>The exported profile.</summary>
internal sealed record IdentityPersonalData(
    Guid ProfileId,
    string SubjectId,
    string Email,
    string DisplayName,
    bool IsBetaUser,
    DateTimeOffset RegisteredAt,
    DateTimeOffset? ModifiedAt);
