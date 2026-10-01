using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Validators;

public sealed class StartGlobalProductIdentityWorkflowValidator
    : AbstractValidator<StartGlobalProductIdentityWorkflowCommand>
{
    public StartGlobalProductIdentityWorkflowValidator()
    {
        RuleFor(command => command.Request).NotNull();
        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.GlobalProductId).NotEmpty();
            RuleFor(command => command.Request.OperationId).NotEmpty();
            RuleFor(command => command.Request.ExpectedVersion).GreaterThanOrEqualTo(0);
        });
    }
}
