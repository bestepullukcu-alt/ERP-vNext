using FluentValidation;

using Diten.MdmService.Application.Features.Countries.Commands;

namespace Diten.MdmService.Application.Features.Countries.Validators;

public sealed class UpdateCountryValidator : AbstractValidator<UpdateCountryCommand>
{
    public UpdateCountryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Country ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Country name is required.")
            .MaximumLength(100).WithMessage("Country name cannot exceed 100 characters.");

        RuleFor(x => x.Iso2Code)
            .NotEmpty().WithMessage("ISO 2 code is required.")
            .Length(2).WithMessage("ISO 2 code must be exactly 2 characters.")
            .Matches("^[A-Z]{2}$").WithMessage("ISO 2 code must contain only uppercase letters.");

        RuleFor(x => x.Iso3Code)
            .NotEmpty().WithMessage("ISO 3 code is required.")
            .Length(3).WithMessage("ISO 3 code must be exactly 3 characters.")
            .Matches("^[A-Z]{3}$").WithMessage("ISO 3 code must contain only uppercase letters.");

        RuleFor(x => x.NumericCode)
            .MaximumLength(3).WithMessage("Numeric code cannot exceed 3 characters.");

        RuleFor(x => x.PhoneCode)
            .MaximumLength(10).WithMessage("Phone code cannot exceed 10 characters.");

        RuleFor(x => x.CurrencyCode)
            .Length(3).WithMessage("Currency code must be exactly 3 characters.")
            .When(x => !string.IsNullOrEmpty(x.CurrencyCode));

        RuleFor(x => x.CurrencyName)
            .MaximumLength(50).WithMessage("Currency name cannot exceed 50 characters.");

        RuleFor(x => x.CurrencySymbol)
            .MaximumLength(10).WithMessage("Currency symbol cannot exceed 10 characters.");

        RuleFor(x => x.Region)
            .MaximumLength(50).WithMessage("Region cannot exceed 50 characters.");

        RuleFor(x => x.SubRegion)
            .MaximumLength(50).WithMessage("Sub-region cannot exceed 50 characters.");

        RuleFor(x => x.Capital)
            .MaximumLength(100).WithMessage("Capital cannot exceed 100 characters.");

        RuleFor(x => x.NativeName)
            .MaximumLength(100).WithMessage("Native name cannot exceed 100 characters.");
    }
}