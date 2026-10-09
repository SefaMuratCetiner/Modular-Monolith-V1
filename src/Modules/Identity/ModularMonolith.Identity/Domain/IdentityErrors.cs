using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Identity.Domain;

/// <summary>Identity error catalogue.</summary>
internal static class IdentityErrors
{
    public static Error EmailInvalid { get; } = Error.Validation("identity.email_invalid", "A valid e-mail address is required.");

    public static Error DisplayNameInvalid { get; } = Error.Validation("identity.display_name_invalid", $"Display name is required and must be at most {UserProfile.MaxDisplayNameLength} characters.");

    public static Error NotAuthenticated { get; } = Error.Forbidden("identity.not_authenticated", "An authenticated user is required.");

    public static Error AlreadyRegistered { get; } = Error.Conflict("identity.already_registered", "The current user is already registered.");

    public static Error EmailTaken { get; } = Error.Conflict("identity.email_taken", "The e-mail address is already in use.");

    public static Error UserNotFound { get; } = Error.NotFound("identity.user_not_found", "No profile exists for this user in the current tenant.");

    public static Error ProfileNotFound { get; } = Error.NotFound("identity.profile_not_found", "The current user has not registered yet.");
}
