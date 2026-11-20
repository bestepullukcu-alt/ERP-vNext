using Diten.MdmService.Application.Features.BankMaster.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.BankMaster.Validators;

public sealed class BulkDeleteBankValidator : AbstractValidator<BulkDeleteBankCommand>
{
    public BulkDeleteBankValidator()
    {
        RuleFor(x => x.Request).NotNull().WithMessage(BankValidationMessages.RequestRequired);
        RuleFor(x => x.Request.Ids)
            .Must(ids => ids is { Count: > 0 } && ids.Count <= BankRules.BulkDeleteMaxIds
                         && ids.All(id => id != Guid.Empty) && ids.Distinct().Count() == ids.Count)
            .When(x => x.Request is not null)
            .WithMessage(BankValidationMessages.IdsInvalid);
    }
}
