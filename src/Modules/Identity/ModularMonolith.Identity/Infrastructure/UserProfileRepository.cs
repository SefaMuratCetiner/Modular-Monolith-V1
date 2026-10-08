using Dapper;
using ModularMonolith.Identity.Domain;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Kernel.Context;

namespace ModularMonolith.Identity.Infrastructure;

/// <summary>Dapper repository joined to the scoped session and unit of work.</summary>
internal sealed class UserProfileRepository(
    IDbSession session,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IUserProfileRepository
{
    public async Task<bool> SubjectExistsAsync(string subjectId, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "select exists (select 1 from identity.user_profiles where subject_id = @SubjectId and not is_deleted)",
            new { SubjectId = subjectId },
            session.Transaction,
            cancellationToken: cancellationToken));
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "select exists (select 1 from identity.user_profiles where lower(email) = lower(@Email) and not is_deleted)",
            new { Email = email.Trim() },
            session.Transaction,
            cancellationToken: cancellationToken));
    }

    public async Task AddAsync(UserProfile profile, CancellationToken cancellationToken)
    {
        profile.MarkCreated(currentUser.AuditId, timeProvider.GetUtcNow());
        var connection = await session.GetConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            insert into identity.user_profiles (id, subject_id, email, display_name, is_beta_user, created_by, created_at)
            values (@Id, @SubjectId, @Email, @DisplayName, @IsBetaUser, @CreatedBy, @CreatedAt)
            """,
            new
            {
                profile.Id,
                profile.SubjectId,
                profile.Email,
                profile.DisplayName,
                profile.IsBetaUser,
                profile.CreatedBy,
                profile.CreatedAt,
            },
            session.Transaction,
            cancellationToken: cancellationToken));
        unitOfWork.Track(profile);
    }
}
