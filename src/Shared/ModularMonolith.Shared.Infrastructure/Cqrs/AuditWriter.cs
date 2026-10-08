using System.Diagnostics;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Http;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Infrastructure.Serialization;
using ModularMonolith.Shared.Kernel.Context;

namespace ModularMonolith.Shared.Infrastructure.Cqrs;

/// <summary>
/// Writes an audit row into the module's <c>audit_log</c> table inside the command transaction,
/// so an audited change can never be committed without its audit record (and vice versa).
/// Each row records who, what (command payload), before/after state, when, from where and the trace.
/// </summary>
internal sealed class AuditWriter(
    DbSession session,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor)
{
    public async Task WriteAsync<TCommand>(
        string schema,
        string action,
        TCommand command,
        IReadOnlyList<AuditChange> changes,
        CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        var sql = $"""
            insert into {SqlIdentifier.Quote(schema)}.audit_log (id, user_id, action, payload, changes, occurred_at, ip_address, trace_id)
            values (@Id, @UserId, @Action, cast(@Payload as jsonb), cast(@Changes as jsonb), @OccurredAt, @IpAddress, @TraceId)
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                Id = Guid.CreateVersion7(),
                UserId = currentUser.AuditId,
                Action = action,
                Payload = JsonSerializer.Serialize(command, JsonDefaults.Options),

                // Before/after state of every entity the command changed (null when nothing was recorded).
                Changes = changes.Count == 0 ? null : JsonSerializer.Serialize(changes, JsonDefaults.Options),
                OccurredAt = timeProvider.GetUtcNow(),
                IpAddress = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                TraceId = Activity.Current?.TraceId.ToString(),
            },
            session.Transaction,
            cancellationToken: cancellationToken));
    }
}
