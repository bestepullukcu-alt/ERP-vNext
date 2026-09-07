using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Validators;

public sealed class StartLskuRetirementRequestWorkflowValidator
    : AbstractValidator<StartLskuRetirementRequestWorkflowCommand>
{
    public StartLskuRetirementRequestWorkflowValidator()
    {
        RuleFor(x => x.Request).NotNull();
        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.LskuId).NotEmpty();
            RuleFor(x => x.Request.OperationId).NotEmpty();
            RuleFor(x => x.Request.ExpectedVersion).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Request.RequestReason)
                .Must(LskuRetirementRequestWorkflowStartRequestFactory.HasValidReason);
        });
    }
}
