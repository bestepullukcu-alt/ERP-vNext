using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Validators;

public sealed class WithdrawGlobalProductIdentityApprovalValidator
    : AbstractValidator<WithdrawGlobalProductIdentityApprovalCommand>
{
    public WithdrawGlobalProductIdentityApprovalValidator()
    {
        RuleFor(x => x.Request).NotNull();
        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.GlobalProductId).NotEmpty();
            RuleFor(x => x.Request.OperationId).NotEmpty();
            RuleFor(x => x.Request.ExpectedVersion).GreaterThanOrEqualTo(1);
            RuleFor(x => x.Request.ReasonCode).NotEmpty().MaximumLength(128)
                .Must(IsExactText);
            RuleFor(x => x.Request.Comment).MaximumLength(2000)
                .Must(value => value is null || IsExactText(value));
        });
    }

    private static bool IsExactText(string value) =>
        string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl);
}
