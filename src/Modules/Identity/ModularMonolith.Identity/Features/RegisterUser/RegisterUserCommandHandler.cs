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
    TimeProvider timeProvider) : ICommandHandler<RegisterUserCommand, UserProfileId>
{
    public async Task<Result<UserProfileId>> HandleAsync(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var subject = SubjectId.Create(currentUser.UserId);
        if (subject.IsFailure)
        {
            return Result.Failure<UserProfileId>(subject.Error);
        }

        var email = Email.Create(command.Email);
        if (email.IsFailure)
        {
            return Result.Failure<UserProfileId>(email.Error);
        }

        if (await profiles.SubjectExistsAsync(subject.Value, cancellationToken))
        {
            return Result.Failure<UserProfileId>(IdentityErrors.AlreadyRegistered);
        }

        if (await profiles.EmailExistsAsync(email.Value, cancellationToken))
        {
            return Result.Failure<UserProfileId>(IdentityErrors.EmailTaken);
        }

        var isBetaUser = await featureManager.IsEnabledAsync(IdentityFeatures.BetaOnboarding, cancellationToken);
        var registered = UserProfile.Register(subject.Value, email.Value, command.DisplayName, isBetaUser, timeProvider.GetUtcNow());
        if (registered.IsFailure)
        {
            return Result.Failure<UserProfileId>(registered.Error);
        }

        await profiles.AddAsync(registered.Value, cancellationToken);
        return Result.Success(registered.Value.Id);
    }
}
