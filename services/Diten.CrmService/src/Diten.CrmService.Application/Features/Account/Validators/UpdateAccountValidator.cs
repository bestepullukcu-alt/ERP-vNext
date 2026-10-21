using Diten.CrmService.Application.Features.Account.Commands;
using FluentValidation;

namespace Diten.CrmService.Application.Features.Account.Validators;

/// <summary>MOD-0149 §18 for an update; every rule carries its machine code (CRM-ACC-EDIT, errors = [message, code]).</summary>
public sealed class UpdateAccountValidator : AbstractValidator<UpdateAccountCommand>
{
    public UpdateAccountValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithErrorCode(AccountErrors.NotFound);
        RuleFor(x => x.ExpectedVersion).NotNull()
            .WithMessage("expectedVersion (the version you read) is required.").WithErrorCode(AccountErrors.ExpectedVersionRequired);
        AccountFieldRules.Apply(this, x => x.AccountName, x => x.AccountType, x => x.Status, x => x.AddressLine, x => x.Latitude, x => x.Longitude,
            x => x.ResponsiblePersonName, x => x.ResponsiblePersonPhone, x => x.ResponsiblePersonEmail, x => x.Notes, x => x.LogoDataUri);
        RuleFor(x => x.RemoveLogo).Must((command, remove) => !remove || string.IsNullOrWhiteSpace(command.LogoDataUri))
            .WithMessage("Send either a new logo or removeLogo, not both.").WithErrorCode(AccountErrors.LogoConflict);
    }
}
