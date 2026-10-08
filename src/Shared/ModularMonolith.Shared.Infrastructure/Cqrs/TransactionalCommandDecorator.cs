using ModularMonolith.Shared.Infrastructure.Data;
using ModularMonolith.Shared.Infrastructure.Modules;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Shared.Infrastructure.Cqrs;

/// <summary>
/// Innermost command decorator. Everything below happens in ONE database transaction:
/// the handler's writes, in-module domain event handlers (which write integration events to
/// the outbox) and the audit record. A failed <see cref="Result"/> rolls everything back.
/// </summary>
internal sealed class TransactionalCommandDecorator<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    DbSession session,
    DomainEventDispatcher domainEventDispatcher,
    AuditWriter auditWriter,
    ModuleCatalog modules) : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        if (session.IsActive)
        {
            // Nested command inside an existing unit of work: join it.
            return await inner.HandleAsync(command, cancellationToken);
        }

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
            await auditWriter.WriteAsync(schema, typeof(TCommand).Name, command, cancellationToken);

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
