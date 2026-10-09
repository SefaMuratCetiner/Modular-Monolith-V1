using ModularMonolith.Shared.Kernel.Domain;

namespace ModularMonolith.Identity.Domain;

/// <summary>Internal domain event raised when a profile's personal data is erased.</summary>
internal sealed record UserErasedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    UserProfileId UserProfileId,
    string SubjectId) : IDomainEvent;
