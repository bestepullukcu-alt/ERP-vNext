using Diten.CrmService.Application.Features.Account.Commands;
using FluentValidation;

namespace Diten.CrmService.Application.Features.Account.Validators;

public sealed class CreateAccountValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountValidator()
    {
        RuleFor(x => x.AccountName).NotEmpty().MaximumLength(200);
        // AccountCode is optional on input (auto-generated when blank); only shape-validated when supplied.
        RuleFor(x => x.AccountCode!)
            .MaximumLength(64)
            .Matches("^[A-Za-z0-9._-]+$").WithMessage("AccountCode contains invalid characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.AccountCode));
        RuleFor(x => x.AccountType).NotEmpty();
        RuleFor(x => x.Status).NotEmpty();
        RuleFor(x => x.AddressLine!).MaximumLength(500).When(x => x.AddressLine is not null);
        RuleFor(x => x.Latitude!.Value).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude!.Value).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
        RuleFor(x => x.ResponsiblePersonName!).MaximumLength(200).When(x => x.ResponsiblePersonName is not null);
        RuleFor(x => x.ResponsiblePersonPhone!).MaximumLength(32).When(x => x.ResponsiblePersonPhone is not null);
        RuleFor(x => x.ResponsiblePersonEmail!)
            .MaximumLength(256).EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.ResponsiblePersonEmail));
        RuleFor(x => x.Notes!).MaximumLength(2000).When(x => x.Notes is not null);
        RuleFor(x => x.ExternalReference!.ExternalId)
            .NotEmpty().WithMessage("ExternalReference.ExternalId is required when an external reference is supplied.")
            .When(x => x.ExternalReference is not null);
        RuleFor(x => x.LogoDataUri!)
            .Matches(AccountLogoRules.DataUriPattern).WithMessage(AccountLogoRules.FormatMessage)
            .MaximumLength(AccountLogoRules.MaxLength).WithMessage(AccountLogoRules.SizeMessage)
            .When(x => !string.IsNullOrWhiteSpace(x.LogoDataUri));
    }
}
