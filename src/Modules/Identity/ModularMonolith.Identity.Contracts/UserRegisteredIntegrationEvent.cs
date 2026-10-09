using ModularMonolith.Shared.Kernel.Messaging;
using ModularMonolith.Shared.Kernel.Privacy;

namespace ModularMonolith.Identity.Contracts;

/// <summary>
/// Published by Identity when a user completes registration (delivered via the transactional outbox).
/// <para>
/// Version history: v1 also carried the e-mail address. v2 removed it (data minimisation: no consumer
/// needed it, and every copy is one more place erasure has to reach). Stored v1 messages are upcast by
/// Identity's <c>UserRegisteredV1ToV2Upcaster</c>.
/// </para>
/// </summary>
/// <param name="EventId">The event identifier.</param>
/// <param name="OccurredAt">When the user registered.</param>
/// <param name="TenantId">The tenant the user belongs to.</param>
/// <param name="UserProfileId">The Identity profile identifier.</param>
/// <param name="SubjectId">The identity provider subject (<c>sub</c> claim), a pseudonymous id.</param>
/// <param name="DisplayName">The display name (personal data: consumers must also handle <see cref="UserErasedIntegrationEvent"/>).</param>
[IntegrationEvent("identity.user-registered", version: 2)]
public sealed record UserRegisteredIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string TenantId,
    Guid UserProfileId,
    string SubjectId,
    [property: PersonalData] string DisplayName) : IIntegrationEvent;
