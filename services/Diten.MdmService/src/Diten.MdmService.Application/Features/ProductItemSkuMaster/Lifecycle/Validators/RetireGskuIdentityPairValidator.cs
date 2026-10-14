using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Validators;

public sealed class RetireGskuIdentityPairValidator : AbstractValidator<RetireGskuIdentityPairCommand>
{
    public RetireGskuIdentityPairValidator()
    {
        RuleFor(command => command.Request).NotNull();
        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.GskuId).NotEmpty();
            RuleFor(command => command.Request.ExpectedGskuVersion).GreaterThanOrEqualTo(0);
            RuleFor(command => command.Request.OperationId).NotEmpty();
            RuleFor(command => command.Request.ReasonCode)
                .NotEmpty().MaximumLength(128)
                .Must(value => value == value.Trim() && value.All(character => !char.IsControl(character)));
        });
    }
}
