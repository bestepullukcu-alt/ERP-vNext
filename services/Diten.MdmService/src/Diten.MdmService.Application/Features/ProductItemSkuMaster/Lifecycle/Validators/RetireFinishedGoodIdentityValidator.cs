using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Validators;

public sealed class RetireFinishedGoodIdentityValidator : AbstractValidator<RetireFinishedGoodIdentityCommand>
{
    public RetireFinishedGoodIdentityValidator()
    {
        RuleFor(x => x.Request).NotNull();
        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.FinishedGoodId).NotEmpty();
            RuleFor(x => x.Request.ExpectedVersion).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Request.OperationId).NotEmpty();
            RuleFor(x => x.Request.ReasonCode).NotEmpty().MaximumLength(128)
                .Must(value => value == value.Trim() && !value.Any(char.IsControl));
            RuleFor(x => x.Request.Comment).MaximumLength(1000)
                .Must(value => value is null || value == value.Trim() && !value.Any(char.IsControl));
        });
    }
}
