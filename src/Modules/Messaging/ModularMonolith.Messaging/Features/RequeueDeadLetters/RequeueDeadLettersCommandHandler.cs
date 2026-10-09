using ModularMonolith.Shared.Infrastructure.Messaging;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Messaging.Features.RequeueDeadLetters;

/// <summary>
/// Replays dead letters after the cause is fixed. Safe because every consumer is inbox-protected: handlers
/// that already succeeded are skipped. As a command it is audited in <c>messaging.audit_log</c>
/// (who replayed what, when, from where).
/// </summary>
internal sealed class RequeueDeadLettersCommandHandler(OutboxAdministration administration)
    : ICommandHandler<RequeueDeadLettersCommand, int>
{
    public Task<Result<int>> HandleAsync(RequeueDeadLettersCommand command, CancellationToken cancellationToken)
        => administration.RequeueAsync(command.Module, command.MessageId, cancellationToken);
}
