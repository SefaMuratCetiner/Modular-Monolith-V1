using Microsoft.FeatureManagement;
using ModularMonolith.Identity.Domain;
using ModularMonolith.Identity.Infrastructure;
using ModularMonolith.Shared.Kernel.Context;
using ModularMonolith.Shared.Kernel.Cqrs;
using ModularMonolith.Shared.Kernel.Results;

namespace ModularMonolith.Identity.Features.RegisterUser;

/// <summary>Registers the caller's profile in the current tenant.</summary>
internal sealed class RegisterUserCommandHandler(
    IUserProfileRepository profiles,
    ICurrentUser currentUser,
    IFeatureManager featureManager,
    TimeProvider timeProvider) : ICommandHandler<RegisterUserCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } subjectId)
        {
            return Result.Failure<Guid>(IdentityErrors.NotAuthenticated);
        }

        if (await profiles.SubjectExistsAsync(subjectId, cancellationToken))
        {
            return Result.Failure<Guid>(IdentityErrors.AlreadyRegistered);
        }

        if (await profiles.EmailExistsAsync(command.Email, cancellationToken))
        {
            return Result.Failure<Guid>(IdentityErrors.EmailTaken);
        }

        var isBetaUser = await featureManager.IsEnabledAsync(IdentityFeatures.BetaOnboarding, cancellationToken);
        var registered = UserProfile.Register(subjectId, command.Email, command.DisplayName, isBetaUser, timeProvider.GetUtcNow());
        if (registered.IsFailure)
        {
            return Result.Failure<Guid>(registered.Error);
        }

        await profiles.AddAsync(registered.Value, cancellationToken);
        return Result.Success(registered.Value.Id);
    }
}
