using System.Text.RegularExpressions;
using ModularMonolith.Shared.Kernel.Domain;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Identity.Domain;

/// <summary>
/// User profile aggregate. Credentials live in Keycloak; this module owns the tenant-local profile
/// linked to the identity provider subject (<c>sub</c>).
/// </summary>
internal sealed partial class UserProfile : AggregateRoot
{
    public const int MaxDisplayNameLength = 100;

    private UserProfile(Guid id, string subjectId, string email, string displayName, bool isBetaUser)
        : base(id)
    {
        SubjectId = subjectId;
        Email = email;
        DisplayName = displayName;
        IsBetaUser = isBetaUser;
    }

    public string SubjectId { get; }

    public string Email { get; }

    public string DisplayName { get; }

    public bool IsBetaUser { get; }

    /// <summary>Registers a profile and raises <see cref="UserRegisteredDomainEvent"/>.</summary>
    public static Result<UserProfile> Register(string subjectId, string? email, string? displayName, bool isBetaUser, DateTimeOffset now)
    {
        var trimmedEmail = email?.Trim();
        if (trimmedEmail is null || trimmedEmail.Length > 254 || !EmailPattern().IsMatch(trimmedEmail))
        {
            return Result.Failure<UserProfile>(IdentityErrors.EmailInvalid);
        }

        var trimmedName = displayName?.Trim();
        if (string.IsNullOrEmpty(trimmedName) || trimmedName.Length > MaxDisplayNameLength)
        {
            return Result.Failure<UserProfile>(IdentityErrors.DisplayNameInvalid);
        }

        var profile = new UserProfile(Guid.CreateVersion7(now), subjectId, trimmedEmail, trimmedName, isBetaUser);
        profile.Raise(new UserRegisteredDomainEvent(Guid.CreateVersion7(now), now, profile.Id, subjectId, trimmedEmail, trimmedName));
        return Result.Success(profile);
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EmailPattern();
}
