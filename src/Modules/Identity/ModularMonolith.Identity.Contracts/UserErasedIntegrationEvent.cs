using ModularMonolith.Shared.Kernel.Messaging;

namespace ModularMonolith.Identity.Contracts;

/// <summary>
/// Published by Identity when a user's personal data was erased (right to erasure, GDPR Art. 17).
/// Every module that keeps a copy of the user's personal data must consume it and anonymize that copy.
/// It carries only pseudonymous identifiers.
/// </summary>
/// <param name="EventId">The event identifier.</param>
/// <param name="OccurredAt">When the profile was erased.</param>
/// <param name="TenantId">The tenant the user belonged to.</param>
/// <param name="UserProfileId">The erased Identity profile.</param>
/// <param name="SubjectId">The identity provider subject (<c>sub</c> claim) of the erased user.</param>
[IntegrationEvent("identity.user-erased", version: 1)]
public sealed record UserErasedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string TenantId,
    Guid UserProfileId,
    string SubjectId) : IIntegrationEvent;
