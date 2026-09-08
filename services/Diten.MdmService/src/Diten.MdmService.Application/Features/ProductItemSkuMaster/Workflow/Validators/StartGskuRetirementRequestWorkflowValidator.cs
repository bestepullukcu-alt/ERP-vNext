using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Validators;

public sealed class StartGskuRetirementRequestWorkflowValidator : AbstractValidator<StartGskuRetirementRequestWorkflowCommand>
{
    public StartGskuRetirementRequestWorkflowValidator()
    {
        RuleFor(x => x.Request).NotNull();
        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.GskuId).NotEmpty();
            RuleFor(x => x.Request.OperationId).NotEmpty();
            RuleFor(x => x.Request.ExpectedGskuVersion).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.RequestReason).NotEmpty().MaximumLength(2000)
            .Must(x => x == x.Trim() && !x.Any(char.IsControl));
        });
    }
}
