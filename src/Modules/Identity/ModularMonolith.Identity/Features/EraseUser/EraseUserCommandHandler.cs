using ModularMonolith.Identity.Domain;
using ModularMonolith.Identity.Infrastructure;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Identity.Features.EraseUser;

/// <summary>
/// Right to erasure (GDPR Art. 17). Identity anonymizes its profile in the command transaction; the
/// <see cref="UserErasedDomainEventHandler"/> publishes <c>identity.user-erased</c> through the outbox in the
/// same transaction, so every other module anonymizes its copy exactly once (inbox-protected).
/// The identity provider account is not touched: disable or delete it in Keycloak.
/// </summary>
internal sealed class EraseUserCommandHandler(
    IUserProfileRepository profiles,
    TimeProvider timeProvider) : ICommandHandler<EraseUserCommand, UserProfileId>
{
    public async Task<Result<UserProfileId>> HandleAsync(EraseUserCommand command, CancellationToken cancellationToken)
    {
        var subject = SubjectId.Create(command.SubjectId);
        if (subject.IsFailure)
        {
            return Result.Failure<UserProfileId>(IdentityErrors.UserNotFound);
        }

        var profile = await profiles.GetBySubjectAsync(subject.Value, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<UserProfileId>(IdentityErrors.UserNotFound);
        }

        profile.Erase(timeProvider.GetUtcNow());
        return await profiles.EraseAsync(profile, cancellationToken)
            ? Result.Success(profile.Id)
            : Result.Failure<UserProfileId>(Error.ConcurrencyConflict);
    }
}
