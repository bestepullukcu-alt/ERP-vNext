using Diten.MdmService.Application.Features.BankMaster.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.BankMaster.Validators;

public sealed class UpdateBankBranchValidator : AbstractValidator<UpdateBankBranchCommand>
{
    public UpdateBankBranchValidator()
    {
        RuleFor(x => x.Request).NotNull().WithMessage(BankValidationMessages.RequestRequired)
            .SetValidator(new BankBranchWriteRequestValidator());
    }
}
