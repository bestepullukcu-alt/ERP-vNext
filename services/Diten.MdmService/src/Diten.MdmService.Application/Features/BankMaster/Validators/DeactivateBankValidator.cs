using Diten.MdmService.Application.Features.BankMaster.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.BankMaster.Validators;

/// <summary>A deactivation names its reason: 1..64 characters, trimmed.</summary>
public sealed class DeactivateBankValidator : AbstractValidator<DeactivateBankCommand>
{
    public DeactivateBankValidator()
    {
        RuleFor(x => x.Request).NotNull().WithMessage(BankValidationMessages.RequestRequired)
            .SetValidator(new DeactivateRequestValidator());
    }
}
