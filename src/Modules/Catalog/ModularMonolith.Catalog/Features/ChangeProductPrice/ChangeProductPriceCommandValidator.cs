using FluentValidation;
using Microsoft.Extensions.Localization;

namespace ModularMonolith.Catalog.Features.ChangeProductPrice;

/// <summary>Input validation for a price change (localized messages).</summary>
internal sealed class ChangeProductPriceCommandValidator : AbstractValidator<ChangeProductPriceCommand>
{
    public ChangeProductPriceCommandValidator(IStringLocalizer<CatalogModule> localizer)
    {
        RuleFor(command => command.Price)
            .GreaterThan(0).WithMessage(_ => localizer["PriceInvalid"]);

        RuleFor(command => command.Currency)
            .NotEmpty().WithMessage(_ => localizer["CurrencyInvalid"])
            .Length(3).WithMessage(_ => localizer["CurrencyInvalid"]);

        RuleFor(command => command.ExpectedVersion)
            .GreaterThan(0).WithMessage(_ => localizer["VersionInvalid"]);
    }
}
