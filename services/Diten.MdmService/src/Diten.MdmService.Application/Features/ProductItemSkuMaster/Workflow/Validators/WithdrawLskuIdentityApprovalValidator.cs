using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Validators;

public sealed class WithdrawLskuIdentityApprovalValidator
    : AbstractValidator<WithdrawLskuIdentityApprovalCommand>
{
    public WithdrawLskuIdentityApprovalValidator()
    {
        RuleFor(x => x.Request).NotNull();
        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.LskuId).NotEmpty();
            RuleFor(x => x.Request.ExpectedVersion).GreaterThan(0);
            RuleFor(x => x.Request.OperationId).NotEmpty();
            RuleFor(x => x.Request.ReasonCode).NotEmpty().MaximumLength(128)
                .Must(value => value == value.Trim() && !value.Any(char.IsControl));
            RuleFor(x => x.Request.Comment).MaximumLength(2000)
                .Must(value => value is null || value == value.Trim() && !value.Any(char.IsControl));
        });
    }
}
