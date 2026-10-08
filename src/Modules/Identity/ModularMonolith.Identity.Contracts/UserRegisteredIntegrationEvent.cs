using ModularMonolith.Shared.Kernel.Messaging;

namespace ModularMonolith.Identity.Contracts;

/// <summary>
/// Published by Identity when a user completes registration (delivered via the transactional outbox).
/// </summary>
/// <param name="EventId">The event identifier.</param>
/// <param name="OccurredAt">When the user registered.</param>
/// <param name="TenantId">The tenant the user belongs to.</param>
/// <param name="UserProfileId">The Identity profile identifier.</param>
/// <param name="SubjectId">The identity provider subject (<c>sub</c> claim).</param>
/// <param name="Email">The e-mail address.</param>
/// <param name="DisplayName">The display name.</param>
public sealed record UserRegisteredIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string TenantId,
    Guid UserProfileId,
    string SubjectId,
    string Email,
    string DisplayName) : IIntegrationEvent;
