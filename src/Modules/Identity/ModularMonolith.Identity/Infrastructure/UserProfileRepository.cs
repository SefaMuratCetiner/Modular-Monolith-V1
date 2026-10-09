using Dapper;
using ModularMonolith.Identity.Domain;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Infrastructure.Privacy;
using ModularMonolith.Shared.Kernel.Context;
using ModularMonolith.Shared.Kernel.Privacy;

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

    public async Task<UserProfile?> GetBySubjectAsync(SubjectId subjectId, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<UserProfileRow>(new CommandDefinition(
            """
            select id as Id, subject_id as SubjectId, email as Email, display_name as DisplayName, is_beta_user as IsBetaUser,
                   version as Version, created_by as CreatedBy, created_at as CreatedAt,
                   modified_by as ModifiedBy, modified_at as ModifiedAt
            from identity.user_profiles
            where subject_id = @SubjectId and not is_deleted
            """,
            new { SubjectId = subjectId.Value },
            session.Transaction,
            cancellationToken: cancellationToken));

        return row is null
            ? null
            : UserProfile.Rehydrate(
                new UserProfileId(row.Id), row.SubjectId, row.Email, row.DisplayName, row.IsBetaUser,
                row.Version, row.CreatedBy, row.CreatedAt, row.ModifiedBy, row.ModifiedAt);
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

    public async Task<bool> EraseAsync(UserProfile profile, CancellationToken cancellationToken)
    {
        profile.MarkModified(currentUser.AuditId, timeProvider.GetUtcNow());
        var connection = await session.GetConnectionAsync(cancellationToken);

        // The e-mail placeholder is unique per profile and uses the reserved ".invalid" TLD (RFC 2606),
        // so it can never reach a real mailbox; the soft delete frees the unique indexes for re-registration.
        var updated = await connection.ExecuteAsync(new CommandDefinition(
            """
            update identity.user_profiles
            set email = 'erased-' || id::text || '@erased.invalid', display_name = @ErasedName, is_deleted = true,
                erased_at = @ErasedAt, modified_by = @ModifiedBy, modified_at = @ErasedAt, version = version + 1
            where id = @Id and version = @Version and not is_deleted
            """,
            new
            {
                Id = profile.Id.Value,
                ErasedName = PersonalDataRedaction.ErasedDisplayName,
                ErasedAt = profile.ModifiedAt,
                profile.ModifiedBy,
                profile.Version,
            },
            session.Transaction,
            cancellationToken: cancellationToken));

        if (updated == 0)
        {
            return false;
        }

        unitOfWork.RecordChange(new AuditChange(
            EntityType,
            profile.Id.ToString(),
            AuditChange.Delete,
            Before: new ErasureSnapshot(IsErased: false, profile.Version),
            After: new ErasureSnapshot(IsErased: true, profile.Version + 1)));
        unitOfWork.Track(profile);
        return true;
    }

    /// <summary>The audited fields of a profile; personal data is stored as <c>[redacted]</c>.</summary>
    private sealed record UserProfileSnapshot(
        string SubjectId,
        [property: PersonalData] string Email,
        [property: PersonalData] string DisplayName,
        bool IsBetaUser,
        int Version);

    /// <summary>The audited state of an erasure.</summary>
    private sealed record ErasureSnapshot(bool IsErased, int Version);

    private sealed class UserProfileRow
    {
        public Guid Id { get; init; }

        public string SubjectId { get; init; } = string.Empty;

        public string Email { get; init; } = string.Empty;

        public string DisplayName { get; init; } = string.Empty;

        public bool IsBetaUser { get; init; }

        public int Version { get; init; }

        public string CreatedBy { get; init; } = string.Empty;

        public DateTimeOffset CreatedAt { get; init; }

        public string? ModifiedBy { get; init; }

        public DateTimeOffset? ModifiedAt { get; init; }
    }
}
