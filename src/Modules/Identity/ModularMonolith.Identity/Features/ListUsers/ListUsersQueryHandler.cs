using Dapper;
using ModularMonolith.Identity.Features.GetCurrentUser;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Paging;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Identity.Features.ListUsers;

/// <summary>Pages through the tenant's profiles (RLS-scoped) and returns the total count for navigation.</summary>
internal sealed class ListUsersQueryHandler(IDbSession session) : IQueryHandler<ListUsersQuery, PagedResult<UserProfileView>>
{
    private const string CountSql = "select count(*) from identity.user_profiles where not is_deleted";

    // "order by display_name, id" keeps paging deterministic when names repeat.
    private const string PageSql = """
        select id as Id, subject_id as SubjectId, email as Email, display_name as DisplayName,
               is_beta_user as IsBetaUser, created_at as CreatedAt
        from identity.user_profiles
        where not is_deleted
        order by display_name, id
        limit @Limit offset @Offset
        """;

    public async Task<Result<PagedResult<UserProfileView>>> HandleAsync(ListUsersQuery query, CancellationToken cancellationToken)
    {
        var paging = query.Paging;
        var connection = await session.GetConnectionAsync(cancellationToken);
        var total = await connection.ExecuteScalarAsync<long>(new CommandDefinition(CountSql, cancellationToken: cancellationToken));
        var rows = await connection.QueryAsync<UserProfileView>(new CommandDefinition(
            PageSql,
            new { Limit = paging.PageSize, paging.Offset },
            cancellationToken: cancellationToken));

        return Result.Success(PagedResult.Create(paging, rows.AsList(), total));
    }
}
