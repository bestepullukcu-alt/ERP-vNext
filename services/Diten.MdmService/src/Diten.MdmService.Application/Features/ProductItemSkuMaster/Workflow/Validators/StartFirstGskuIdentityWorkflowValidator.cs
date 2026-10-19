using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Validators;

public sealed class StartFirstGskuIdentityWorkflowValidator
    : AbstractValidator<StartFirstGskuIdentityWorkflowCommand>
{
    public StartFirstGskuIdentityWorkflowValidator()
    {
        RuleFor(command => command.Request).NotNull();
        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.GskuId).NotEmpty();
            RuleFor(command => command.Request.ExpectedGskuVersion).GreaterThanOrEqualTo(0);
            RuleFor(command => command.Request.OperationId).NotEmpty();
        });
    }
}
