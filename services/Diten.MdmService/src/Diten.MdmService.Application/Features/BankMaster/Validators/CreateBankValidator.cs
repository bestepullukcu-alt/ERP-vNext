using Diten.MdmService.Application.Features.BankMaster.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.BankMaster.Validators;

public sealed class CreateBankValidator : AbstractValidator<CreateBankCommand>
{
    public CreateBankValidator()
    {
        RuleFor(x => x.Request).NotNull().WithMessage(BankValidationMessages.RequestRequired)
            .SetValidator(new BankWriteRequestValidator());
    }
}
