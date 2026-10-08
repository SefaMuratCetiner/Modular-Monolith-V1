using Dapper;
using ModularMonolith.Identity.Domain;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Identity.Features.GetCurrentUser;

/// <summary>Reads the caller's profile (RLS-scoped to the current tenant).</summary>
internal sealed class GetCurrentUserQueryHandler(IDbSession session) : IQueryHandler<GetCurrentUserQuery, UserProfileView>
{
    public async Task<Result<UserProfileView>> HandleAsync(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        var profile = await connection.QuerySingleOrDefaultAsync<UserProfileView>(new CommandDefinition(
            """
            select id as Id, subject_id as SubjectId, email as Email, display_name as DisplayName,
                   is_beta_user as IsBetaUser, created_at as CreatedAt
            from identity.user_profiles
            where subject_id = @SubjectId and not is_deleted
            """,
            new { query.SubjectId },
            cancellationToken: cancellationToken));

        return profile is null
            ? Result.Failure<UserProfileView>(IdentityErrors.ProfileNotFound)
            : Result.Success(profile);
    }
}
