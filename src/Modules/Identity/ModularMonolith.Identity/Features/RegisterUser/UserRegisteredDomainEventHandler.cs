using ModularMonolith.Identity.Contracts;
using ModularMonolith.Identity.Domain;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Kernel.Context;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Messaging;

namespace ModularMonolith.Identity.Features.RegisterUser;

/// <summary>
/// Maps the domain event to the public integration event inside the same transaction and counts the
/// registration once it is committed.
/// </summary>
internal sealed class UserRegisteredDomainEventHandler(
    IOutbox outbox,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : IDomainEventHandler<UserRegisteredDomainEvent>
{
    public async Task HandleAsync(UserRegisteredDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await outbox.AddAsync(
            new UserRegisteredIntegrationEvent(
                domainEvent.EventId,
                domainEvent.OccurredAt,
                tenantContext.TenantId,
                domainEvent.UserProfileId.Value,
                domainEvent.SubjectId,
                domainEvent.Email,
                domainEvent.DisplayName),
            cancellationToken);

        unitOfWork.OnCommitted(_ =>
        {
            IdentityTelemetry.UsersRegistered.Add(1);
            return Task.CompletedTask;
        });
    }
}
