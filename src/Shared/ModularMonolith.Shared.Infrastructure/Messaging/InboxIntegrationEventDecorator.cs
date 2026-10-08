using Dapper;
using ModularMonolith.Shared.Infrastructure.Cqrs;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Kernel.Messaging;

namespace ModularMonolith.Shared.Infrastructure.Messaging;

/// <summary>
/// Idempotent consumer: records (message id, handler) in the consumer module's inbox in the same
/// transaction as the handler's own writes. A redelivered message is skipped by handlers that
/// already processed it, which makes at-least-once delivery effectively exactly-once per handler.
/// </summary>
internal sealed class InboxIntegrationEventDecorator<TEvent>(
    IIntegrationEventHandler<TEvent> inner,
    IntegrationHandlerRegistration registration,
    DbSession session,
    DomainEventDispatcher domainEventDispatcher,
    TimeProvider timeProvider) : IIntegrationEventHandler<TEvent>
    where TEvent : IIntegrationEvent
{
    public async Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken)
    {
        await session.BeginAsync(cancellationToken);
        try
        {
            var connection = await session.GetConnectionAsync(cancellationToken);
            var sql = $"""
                insert into {SqlIdentifier.Quote(registration.ConsumerSchema)}.inbox_messages (message_id, handler, processed_at)
                values (@MessageId, @Handler, @ProcessedAt)
                on conflict (message_id, handler) do nothing
                """;
            var inserted = await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { MessageId = integrationEvent.EventId, Handler = registration.HandlerName, ProcessedAt = timeProvider.GetUtcNow() },
                session.Transaction,
                cancellationToken: cancellationToken));

            if (inserted == 0)
            {
                // Already processed by this handler.
                await session.RollbackAsync(cancellationToken);
                return;
            }

            await inner.HandleAsync(integrationEvent, cancellationToken);
            await domainEventDispatcher.DispatchAsync(session, cancellationToken);
            await session.CommitAsync(cancellationToken);
        }
        catch
        {
            await session.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
