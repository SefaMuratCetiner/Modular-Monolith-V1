using ModularMonolith.Identity.Contracts;
using ModularMonolith.Identity.Domain;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Kernel.Context;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Messaging;

namespace ModularMonolith.Identity.Features.EraseUser;

/// <summary>Publishes <see cref="UserErasedIntegrationEvent"/> in the erasure transaction.</summary>
internal sealed class UserErasedDomainEventHandler(
    IOutbox outbox,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : IDomainEventHandler<UserErasedDomainEvent>
{
    public async Task HandleAsync(UserErasedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await outbox.AddAsync(
            new UserErasedIntegrationEvent(
                domainEvent.EventId,
                domainEvent.OccurredAt,
                tenantContext.TenantId,
                domainEvent.UserProfileId.Value,
                domainEvent.SubjectId),
            cancellationToken);

        unitOfWork.OnCommitted(_ =>
        {
            IdentityTelemetry.UsersErased.Add(1);
            return Task.CompletedTask;
        });
    }
}
