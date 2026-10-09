using Diten.MdmService.Application.Features.ProductItemSkuMaster.Validators;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Validators;

public sealed class StartGskuCorrectionWorkflowValidator : AbstractValidator<StartGskuCorrectionWorkflowCommand>
{
    public StartGskuCorrectionWorkflowValidator()
    {
        RuleFor(x => x.Request).NotNull();
        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.GskuId).NotEmpty();
            RuleFor(x => x.Request.OperationId).NotEmpty();
            RuleFor(x => x.Request.ExpectedGskuVersion).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Request.PackQuantity).GreaterThan(0);
            RuleFor(x => x.Request.PackUomCode).NotEmpty().MaximumLength(16)
                .Must(x => x == x.Trim() && !x.Any(char.IsControl));
            // The same pack rules as create and update (B11): allowed units, C62 a whole number, scale at most 3.
            RuleFor(x => x.Request).Must(x => CreateFirstGskuDraftValidator.IsValidPack(x.PackQuantity, x.PackUomCode))
                .WithMessage("PACK_QUANTITY_PRECISION_INVALID");
        });
    }
}
