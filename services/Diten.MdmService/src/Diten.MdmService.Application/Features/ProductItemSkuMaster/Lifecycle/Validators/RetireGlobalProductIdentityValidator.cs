using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Validators;

public sealed class RetireGlobalProductIdentityValidator : AbstractValidator<RetireGlobalProductIdentityCommand>
{
    public RetireGlobalProductIdentityValidator()
    {
        RuleFor(x => x.Request).NotNull();
        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.GlobalProductId).NotEmpty();
            RuleFor(x => x.Request.ExpectedVersion).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Request.OperationId).NotEmpty();
            RuleFor(x => x.Request.ReasonCode)
                .NotEmpty().MaximumLength(128).Must(IsExact);
            RuleFor(x => x.Request.Comment)
                .MaximumLength(2000)
                .Must(value => value is null || IsExact(value));
        });
    }

    private static bool IsExact(string value) =>
        string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl);
}
