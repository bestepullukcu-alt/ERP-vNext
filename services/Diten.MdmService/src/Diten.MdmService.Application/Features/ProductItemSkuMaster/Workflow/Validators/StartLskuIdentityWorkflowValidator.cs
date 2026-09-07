using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Validators;

public sealed class StartLskuIdentityWorkflowValidator
    : AbstractValidator<StartLskuIdentityWorkflowCommand>
{
    public StartLskuIdentityWorkflowValidator()
    {
        RuleFor(command => command.Request).NotNull();
        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.LskuId).NotEmpty();
            RuleFor(command => command.Request.OperationId).NotEmpty();
            RuleFor(command => command.Request.ExpectedVersion).GreaterThanOrEqualTo(0);
        });
    }
}
