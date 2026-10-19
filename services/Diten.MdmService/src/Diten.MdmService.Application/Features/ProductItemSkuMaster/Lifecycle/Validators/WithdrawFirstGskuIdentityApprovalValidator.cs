using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Validators;

public sealed class WithdrawFirstGskuIdentityApprovalValidator
    : AbstractValidator<WithdrawFirstGskuIdentityApprovalCommand>
{
    public WithdrawFirstGskuIdentityApprovalValidator()
    {
        RuleFor(command => command.Request).NotNull();
        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.GskuId).NotEmpty();
            RuleFor(command => command.Request.OperationId).NotEmpty();
            RuleFor(command => command.Request.ExpectedGskuVersion).GreaterThanOrEqualTo(1);
            RuleFor(command => command.Request.ReasonCode)
                .NotEmpty().MaximumLength(128).Must(IsExactText);
            RuleFor(command => command.Request.Comment)
                .MaximumLength(2000).Must(value => value is null || IsExactText(value));
        });
    }

    private static bool IsExactText(string value) =>
        string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl);
}
