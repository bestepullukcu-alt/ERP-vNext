using Diten.CrmService.Application.Features.Account.Commands;
using FluentValidation;

namespace Diten.CrmService.Application.Features.Account.Validators;

/// <summary>MOD-0149 §18 for a create; the same field rules and codes as the update (CRM-ACC-EDIT).</summary>
public sealed class CreateAccountValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountValidator()
    {
        AccountFieldRules.Apply(this, x => x.AccountName, x => x.AccountType, x => x.Status, x => x.AddressLine, x => x.Latitude, x => x.Longitude,
            x => x.ResponsiblePersonName, x => x.ResponsiblePersonPhone, x => x.ResponsiblePersonEmail, x => x.Notes, x => x.LogoDataUri);
        // AccountCode is optional on input (auto-generated when blank); only shape-validated when supplied.
        RuleFor(x => x.AccountCode!)
            .MaximumLength(64).WithMessage("AccountCode is at most 64 characters.").WithErrorCode(AccountErrors.CodeInvalid)
            .Matches("^[A-Za-z0-9._-]+$").WithMessage("AccountCode contains invalid characters.").WithErrorCode(AccountErrors.CodeInvalid)
            .When(x => !string.IsNullOrWhiteSpace(x.AccountCode));
        RuleFor(x => x.ExternalReference!.ExternalId)
            .NotEmpty().WithMessage("ExternalReference.ExternalId is required when an external reference is supplied.")
            .WithErrorCode(AccountErrors.ExternalIdRequired)
            .When(x => x.ExternalReference is not null);
    }
}
