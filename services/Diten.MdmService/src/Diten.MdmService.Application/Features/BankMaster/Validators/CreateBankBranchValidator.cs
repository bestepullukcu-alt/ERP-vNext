using Diten.MdmService.Application.Features.BankMaster.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.BankMaster.Validators;

public sealed class CreateBankBranchValidator : AbstractValidator<CreateBankBranchCommand>
{
    public CreateBankBranchValidator()
    {
        RuleFor(x => x.Request).NotNull().WithMessage(BankValidationMessages.RequestRequired)
            .SetValidator(new BankBranchWriteRequestValidator());
    }
}
