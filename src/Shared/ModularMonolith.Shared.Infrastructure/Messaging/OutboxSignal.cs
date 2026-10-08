using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ModularMonolith.Shared.Infrastructure.Messaging;

/// <summary>
/// In-process wake-up signal for the outbox dispatcher. A commit that wrote outbox rows notifies
/// the module schema; repeated notifications for a schema that is already queued are coalesced.
/// No storage writes are involved (unlike enqueuing a background job per commit). Messages whose
/// signal is lost (crash, other instance) are delivered by the recurring Hangfire sweep.
/// </summary>
public sealed class OutboxSignal
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<string, byte> _queued = new(StringComparer.Ordinal);

    /// <summary>Requests a dispatch run for a module schema.</summary>
    /// <param name="schema">The module schema.</param>
    public void Notify(string schema)
    {
        if (_queued.TryAdd(schema, 0))
        {
            _channel.Writer.TryWrite(schema);
        }
    }

    /// <summary>Waits for the next schema to dispatch.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The schema.</returns>
    internal async ValueTask<string> WaitAsync(CancellationToken cancellationToken)
    {
        var schema = await _channel.Reader.ReadAsync(cancellationToken);

        // Clear before processing so a commit that happens during the run schedules another run.
        _queued.TryRemove(schema, out _);
        return schema;
    }
}
