using Dapper;
using ModularMonolith.Identity.Domain;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Kernel.Context;

namespace ModularMonolith.Identity.Infrastructure;

/// <summary>
/// Dapper repository joined to the scoped session and unit of work. Writes record an audit snapshot.
/// </summary>
internal sealed class UserProfileRepository(
    IDbSession session,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IUserProfileRepository
{
    private const string EntityType = "user_profile";

    public async Task<bool> SubjectExistsAsync(SubjectId subjectId, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "select exists (select 1 from identity.user_profiles where subject_id = @SubjectId and not is_deleted)",
            new { SubjectId = subjectId.Value },
            session.Transaction,
            cancellationToken: cancellationToken));
    }

    public async Task<bool> EmailExistsAsync(Email email, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "select exists (select 1 from identity.user_profiles where lower(email) = lower(@Email) and not is_deleted)",
            new { Email = email.Value },
            session.Transaction,
            cancellationToken: cancellationToken));
    }

    public async Task AddAsync(UserProfile profile, CancellationToken cancellationToken)
    {
        profile.MarkCreated(currentUser.AuditId, timeProvider.GetUtcNow());
        var connection = await session.GetConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            insert into identity.user_profiles (id, subject_id, email, display_name, is_beta_user, version, created_by, created_at)
            values (@Id, @SubjectId, @Email, @DisplayName, @IsBetaUser, @Version, @CreatedBy, @CreatedAt)
            """,
            new
            {
                Id = profile.Id.Value,
                SubjectId = profile.SubjectId.Value,
                Email = profile.Email.Value,
                profile.DisplayName,
                profile.IsBetaUser,
                profile.Version,
                profile.CreatedBy,
                profile.CreatedAt,
            },
            session.Transaction,
            cancellationToken: cancellationToken));

        unitOfWork.RecordChange(new AuditChange(
            EntityType,
            profile.Id.ToString(),
            AuditChange.Create,
            Before: null,
            After: new UserProfileSnapshot(profile.SubjectId.Value, profile.Email.Value, profile.DisplayName, profile.IsBetaUser, profile.Version)));
        unitOfWork.Track(profile);
    }

    /// <summary>The audited fields of a profile.</summary>
    private sealed record UserProfileSnapshot(string SubjectId, string Email, string DisplayName, bool IsBetaUser, int Version);
}
