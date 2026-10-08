using ModularMonolith.Identity.Features.GetCurrentUser;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Paging;

namespace ModularMonolith.Identity.Features.ListUsers;

/// <summary>Lists one page of the current tenant's profiles (admin only), ordered by display name.</summary>
internal sealed record ListUsersQuery(PageRequest Paging) : IQuery<PagedResult<UserProfileView>>;
