using FluentValidation;
using Microsoft.Extensions.Localization;
using ModularMonolith.Catalog.Domain;

namespace ModularMonolith.Catalog.Features.CreateProduct;

/// <summary>
/// Input validation with localized messages (Resources/Catalog.{culture}.json).
/// Runs in the ValidationCommandDecorator before any transaction is opened.
/// The aggregate re-checks its invariants, so validators are a fast first line of defense.
/// </summary>
internal sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator(IStringLocalizer<CatalogModule> localizer)
    {
        RuleFor(command => command.Name)
            .NotEmpty().WithMessage(_ => localizer["NameRequired"])
            .MaximumLength(Product.MaxNameLength).WithMessage(_ => localizer["NameTooLong", Product.MaxNameLength]);

        RuleFor(command => command.Sku)
            .NotEmpty().WithMessage(_ => localizer["SkuInvalid"])
            .Matches("^[A-Za-z0-9][A-Za-z0-9-]{2,63}$").WithMessage(_ => localizer["SkuInvalid"]);

        RuleFor(command => command.Price)
            .GreaterThan(0).WithMessage(_ => localizer["PriceInvalid"]);

        RuleFor(command => command.Currency)
            .NotEmpty().WithMessage(_ => localizer["CurrencyInvalid"])
            .Length(3).WithMessage(_ => localizer["CurrencyInvalid"]);
    }
}
