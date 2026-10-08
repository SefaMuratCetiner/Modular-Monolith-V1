using ModularMonolith.Identity.Contracts;
using ModularMonolith.Identity.Domain;
using ModularMonolith.Shared.Kernel.Context;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Messaging;

namespace ModularMonolith.Identity.Features.RegisterUser;

/// <summary>Maps the domain event to the public integration event inside the same transaction.</summary>
internal sealed class PublishUserRegistered(IOutbox outbox, ITenantContext tenantContext) : IDomainEventHandler<UserRegisteredDomainEvent>
{
    public Task HandleAsync(UserRegisteredDomainEvent domainEvent, CancellationToken cancellationToken)
        => outbox.AddAsync(
            new UserRegisteredIntegrationEvent(
                domainEvent.EventId,
                domainEvent.OccurredAt,
                tenantContext.TenantId,
                domainEvent.UserProfileId,
                domainEvent.SubjectId,
                domainEvent.Email,
                domainEvent.DisplayName),
            cancellationToken);
}
