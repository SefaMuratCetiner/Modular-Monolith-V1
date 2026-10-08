using ModularMonolith.Shared.Kernel.Domain;

namespace ModularMonolith.Identity.Domain;

/// <summary>Internal domain event raised when a profile is registered.</summary>
internal sealed record UserRegisteredDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid UserProfileId,
    string SubjectId,
    string Email,
    string DisplayName) : IDomainEvent;
