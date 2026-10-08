using Dapper;
using ModularMonolith.Identity.Features.GetCurrentUser;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Identity.Features.ListUsers;

/// <summary>Pages through the tenant's profiles (RLS-scoped).</summary>
internal sealed class ListUsersQueryHandler(IDbSession session) : IQueryHandler<ListUsersQuery, IReadOnlyList<UserProfileView>>
{
    public async Task<Result<IReadOnlyList<UserProfileView>>> HandleAsync(ListUsersQuery query, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<UserProfileView>(new CommandDefinition(
            """
            select id as Id, subject_id as SubjectId, email as Email, display_name as DisplayName,
                   is_beta_user as IsBetaUser, created_at as CreatedAt
            from identity.user_profiles
            where not is_deleted
            order by display_name
            limit @Limit offset @Offset
            """,
            new { Limit = query.PageSize, Offset = (query.Page - 1) * query.PageSize },
            cancellationToken: cancellationToken));

        return Result.Success<IReadOnlyList<UserProfileView>>(rows.AsList());
    }
}
