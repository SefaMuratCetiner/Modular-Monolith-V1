using ModularMonolith.Shared.Kernel.Cqrs;

namespace ModularMonolith.Identity.Features.GetCurrentUser;

/// <summary>Gets the caller's profile.</summary>
internal sealed record GetCurrentUserQuery(string SubjectId) : IQuery<UserProfileView>;

/// <summary>Profile read model shared by the Identity read slices. CreatedAt is UTC (Npgsql maps timestamptz to DateTime).</summary>
internal sealed record UserProfileView(Guid Id, string SubjectId, string Email, string DisplayName, bool IsBetaUser, DateTime CreatedAt);
