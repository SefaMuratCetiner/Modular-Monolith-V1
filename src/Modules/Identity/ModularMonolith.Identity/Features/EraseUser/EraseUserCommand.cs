using ModularMonolith.Identity.Domain;
using ModularMonolith.Shared.Kernel.Cqrs;

namespace ModularMonolith.Identity.Features.EraseUser;

/// <summary>Erases the personal data of the user with this subject id in the current tenant.</summary>
internal sealed record EraseUserCommand(string SubjectId) : ICommand<UserProfileId>;
