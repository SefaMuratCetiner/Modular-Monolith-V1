using ModularMonolith.Shared.Kernel.Cqrs;

namespace ModularMonolith.Messaging.Features.RequeueDeadLetters;

/// <summary>Re-queues one dead-lettered message, or all of a module's dead letters; returns how many.</summary>
/// <param name="Module">The module schema, e.g. <c>catalog</c>.</param>
/// <param name="MessageId">The message, or <see langword="null"/> for every dead letter of the module.</param>
internal sealed record RequeueDeadLettersCommand(string Module, Guid? MessageId) : ICommand<int>;
