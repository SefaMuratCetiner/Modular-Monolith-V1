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
        profile.Raise(new UserRegisteredDomainEvent(Guid.CreateVersion7(now), now, profile.Id, subjectId.Value, trimmedName));
        return Result.Success(profile);
    }

    /// <summary>Rebuilds a profile from storage (no events, no validation of historical data).</summary>
    public static UserProfile Rehydrate(
        UserProfileId id,
        string subjectId,
        string email,
        string displayName,
        bool isBetaUser,
        int version,
        string createdBy,
        DateTimeOffset createdAt,
        string? modifiedBy,
        DateTimeOffset? modifiedAt)
    {
        var profile = new UserProfile(id, SubjectId.Create(subjectId).Value, Email.Create(email).Value, displayName, isBetaUser);
        profile.RestorePersistedState(version, createdBy, createdAt, modifiedBy, modifiedAt);
        return profile;
    }

    /// <summary>
    /// Erases the profile's personal data (right to erasure) and raises <see cref="UserErasedDomainEvent"/>,
    /// which tells the other modules to anonymize their copies. The row stays (soft-deleted, anonymized),
    /// so audit rows and products keep a valid, pseudonymous reference.
    /// </summary>
    public void Erase(DateTimeOffset now)
    {
        MarkDeleted();
        Raise(new UserErasedDomainEvent(Guid.CreateVersion7(now), now, Id, SubjectId.Value));
    }
}
