using Dapper;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Infrastructure.Modules;
using ModularMonolith.Shared.Kernel.Messaging;

namespace ModularMonolith.Shared.Infrastructure.Messaging;

/// <summary>
/// Writes integration events into the publishing module's own <c>outbox_messages</c> table, inside
/// the active unit of work. After commit it triggers an immediate Hangfire dispatch.
/// </summary>
internal sealed class Outbox(DbSession session, ModuleCatalog modules, OutboxDispatchTrigger trigger) : IOutbox
{
    private readonly HashSet<string> _triggeredSchemas = new(StringComparer.Ordinal);

    public async Task AddAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        if (session.Transaction is null)
        {
            throw new InvalidOperationException("Outbox writes must happen inside a unit of work (a command or an event handler).");
        }

        // The publisher is the module whose Contracts assembly defines the event.
        var schema = modules.ForAssembly(integrationEvent.GetType().Assembly).Schema;
        var connection = await session.GetConnectionAsync(cancellationToken);
        var sql = $"""
            insert into {SqlIdentifier.Quote(schema)}.outbox_messages (id, type, payload, occurred_at, tenant_id)
            values (@Id, @Type, cast(@Payload as jsonb), @OccurredAt, @TenantId)
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                Id = integrationEvent.EventId,
                Type = IntegrationEventRegistry.NameOf(integrationEvent.GetType()),
                Payload = IntegrationEventRegistry.Serialize(integrationEvent),
                integrationEvent.OccurredAt,
                integrationEvent.TenantId,
            },
            session.Transaction,
            cancellationToken: cancellationToken));

        if (_triggeredSchemas.Add(schema))
        {
            session.OnCommitted(_ =>
            {
                _triggeredSchemas.Remove(schema);
                trigger.Trigger(schema);
                return Task.CompletedTask;
            });
        }
    }
}
