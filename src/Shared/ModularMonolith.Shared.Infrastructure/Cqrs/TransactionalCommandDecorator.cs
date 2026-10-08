using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Infrastructure.Modules;
using ModularMonolith.Shared.Infrastructure.Observability;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Results;
using Npgsql;

namespace ModularMonolith.Shared.Infrastructure.Cqrs;

/// <summary>
/// Innermost command decorator. Everything below happens in ONE database transaction:
/// the handler's writes, in-module domain event handlers (which write integration events to
/// the outbox) and the audit record. A failed <see cref="Result"/> rolls everything back.
/// <para>
/// Transient database failures (connection loss, deadlock, serialization failure) that happen
/// <b>before commit</b> re-run the whole unit of work on a fresh connection. This is safe because
/// handlers only touch the database; every other side effect is an after-commit callback or an
/// outbox message. A failure during commit is never retried: the outcome is unknown.
/// </para>
/// </summary>
internal sealed class TransactionalCommandDecorator<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    DbSession session,
    DomainEventDispatcher domainEventDispatcher,
    AuditWriter auditWriter,
    ModuleCatalog modules,
    IOptions<DatabaseOptions> options,
    ILogger<TransactionalCommandDecorator<TCommand, TResponse>> logger) : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        if (session.IsActive)
        {
            // Nested command inside an existing unit of work: join it.
            return await inner.HandleAsync(command, cancellationToken);
        }

        var maxRetries = options.Value.TransientRetries;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await ExecuteOnceAsync(command, cancellationToken);
            }
            catch (NpgsqlException exception) when (exception.IsTransient && !session.CommitStarted && attempt <= maxRetries)
            {
                logger.TransientDatabaseRetry(exception, typeof(TCommand).Name, attempt);
                await session.DiscardConnectionAsync();
                await Task.Delay(TimeSpan.FromMilliseconds(50 * Math.Pow(2, attempt - 1) + Random.Shared.Next(0, 50)), cancellationToken);
            }
        }
    }

    private async Task<Result<TResponse>> ExecuteOnceAsync(TCommand command, CancellationToken cancellationToken)
    {
        await session.BeginAsync(cancellationToken);
        try
        {
            var result = await inner.HandleAsync(command, cancellationToken);
            if (result.IsFailure)
            {
                await session.RollbackAsync(cancellationToken);
                return result;
            }

            await domainEventDispatcher.DispatchAsync(session, cancellationToken);

            var schema = modules.ForAssembly(typeof(TCommand).Assembly).Schema;
            await auditWriter.WriteAsync(schema, typeof(TCommand).Name, command, session.Changes, cancellationToken);

            await session.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await session.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
