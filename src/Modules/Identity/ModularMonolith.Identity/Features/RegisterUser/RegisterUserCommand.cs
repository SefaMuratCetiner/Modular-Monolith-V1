using ModularMonolith.Identity.Domain;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Privacy;

namespace ModularMonolith.Identity.Features.RegisterUser;

/// <summary>
/// Registers a profile for the authenticated caller; returns the profile id.
/// The personal-data fields are written to the audit log as <c>[redacted]</c>.
/// </summary>
internal sealed record RegisterUserCommand(
    [property: PersonalData] string Email,
    [property: PersonalData] string DisplayName) : ICommand<UserProfileId>;
