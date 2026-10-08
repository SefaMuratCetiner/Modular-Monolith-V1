using System.Diagnostics;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Http;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Kernel.Context;

namespace ModularMonolith.Shared.Infrastructure.Cqrs;

/// <summary>
/// Writes an audit row into the module's <c>audit_log</c> table inside the command transaction,
/// so an audited change can never be committed without its audit record (and vice versa).
/// </summary>
internal sealed class AuditWriter(
    DbSession session,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task WriteAsync<TCommand>(string schema, string action, TCommand command, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        var sql = $"""
            insert into {SqlIdentifier.Quote(schema)}.audit_log (id, user_id, action, payload, occurred_at, ip_address, trace_id)
            values (@Id, @UserId, @Action, cast(@Payload as jsonb), @OccurredAt, @IpAddress, @TraceId)
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                Id = Guid.CreateVersion7(),
                UserId = currentUser.AuditId,
                Action = action,
                Payload = JsonSerializer.Serialize(command, JsonOptions),
                OccurredAt = timeProvider.GetUtcNow(),
                IpAddress = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                TraceId = Activity.Current?.TraceId.ToString(),
            },
            session.Transaction,
            cancellationToken: cancellationToken));
    }
}
