using ModularMonolith.Identity.Features.GetCurrentUser;
using ModularMonolith.Shared.Kernel.Cqrs;

namespace ModularMonolith.Identity.Features.ListUsers;

/// <summary>Lists the profiles of the current tenant (admin only), paged.</summary>
internal sealed record ListUsersQuery(int Page, int PageSize) : IQuery<IReadOnlyList<UserProfileView>>;
