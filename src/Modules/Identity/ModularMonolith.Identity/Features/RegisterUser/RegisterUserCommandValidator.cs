using FluentValidation;
using Microsoft.Extensions.Localization;
using ModularMonolith.Identity.Domain;

namespace ModularMonolith.Identity.Features.RegisterUser;

/// <summary>Input validation with localized messages (Resources/Identity.{culture}.json).</summary>
internal sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator(IStringLocalizer<IdentityModule> localizer)
    {
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage(_ => localizer["EmailInvalid"])
            .EmailAddress().WithMessage(_ => localizer["EmailInvalid"])
            .MaximumLength(254).WithMessage(_ => localizer["EmailInvalid"]);

        RuleFor(command => command.DisplayName)
            .NotEmpty().WithMessage(_ => localizer["DisplayNameRequired"])
            .MaximumLength(UserProfile.MaxDisplayNameLength).WithMessage(_ => localizer["DisplayNameTooLong", UserProfile.MaxDisplayNameLength]);
    }
}
