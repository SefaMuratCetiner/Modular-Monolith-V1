using ModularMonolith.Shared.Kernel.Cqrs;

namespace ModularMonolith.Identity.Features.RegisterUser;

/// <summary>Registers a profile for the authenticated caller; returns the profile id.</summary>
internal sealed record RegisterUserCommand(string Email, string DisplayName) : ICommand<Guid>;
