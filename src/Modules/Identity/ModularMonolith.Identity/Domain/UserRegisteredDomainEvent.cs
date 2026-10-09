using ModularMonolith.Shared.Kernel.Domain;

namespace ModularMonolith.Identity.Domain;

/// <summary>Internal domain event raised when a profile is registered.</summary>
internal sealed record UserRegisteredDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    UserProfileId UserProfileId,
    string SubjectId,
    string DisplayName) : IDomainEvent;
