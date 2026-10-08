using ModularMonolith.Shared.Kernel.Domain;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Identity.Domain;

/// <summary>
/// User profile aggregate. Credentials live in Keycloak; this module owns the tenant-local profile
/// linked to the identity provider subject (<see cref="SubjectId"/>).
/// </summary>
internal sealed class UserProfile : AggregateRoot<UserProfileId>
{
    public const int MaxDisplayNameLength = 100;

    private UserProfile(UserProfileId id, SubjectId subjectId, Email email, string displayName, bool isBetaUser)
        : base(id)
    {
        SubjectId = subjectId;
        Email = email;
        DisplayName = displayName;
        IsBetaUser = isBetaUser;
    }

    public SubjectId SubjectId { get; }

    public Email Email { get; }

    public string DisplayName { get; }

    public bool IsBetaUser { get; }

    /// <summary>Registers a profile and raises <see cref="UserRegisteredDomainEvent"/>.</summary>
    public static Result<UserProfile> Register(SubjectId subjectId, Email email, string? displayName, bool isBetaUser, DateTimeOffset now)
    {
        var trimmedName = displayName?.Trim();
        if (string.IsNullOrEmpty(trimmedName) || trimmedName.Length > MaxDisplayNameLength)
        {
            return Result.Failure<UserProfile>(IdentityErrors.DisplayNameInvalid);
        }

        var profile = new UserProfile(UserProfileId.New(now), subjectId, email, trimmedName, isBetaUser);
        profile.Raise(new UserRegisteredDomainEvent(Guid.CreateVersion7(now), now, profile.Id, subjectId.Value, email.Value, trimmedName));
        return Result.Success(profile);
    }
}
