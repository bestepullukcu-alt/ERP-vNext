using Diten.Platform.Application.Features.Workflow.Queries;
using FluentValidation;

namespace Diten.Platform.Application.Features.Workflow.Validators;

public sealed class EvaluateWorkflowTransitionGateValidator : AbstractValidator<EvaluateWorkflowTransitionGateQuery>
{
    public EvaluateWorkflowTransitionGateValidator()
    {
        RuleFor(x => x.Request.ObjectType)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Request.ObjectId)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Request.ObjectRef)
            .NotEmpty()
            .MaximumLength(512);

        RuleFor(x => x.Request.RequestedTransition)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Request.RequestedTargetState)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Request.ActorId)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.Request.ReasonCode)
            .MaximumLength(128)
            .When(x => x.Request.ReasonCode is not null);

        RuleFor(x => x.Request.TargetScope)
            .Must(value => !value.HasValue || Enum.IsDefined(value.Value))
            .WithMessage(WorkflowReasonCodes.WorkflowInvalidTargetScope);

        RuleFor(x => x.Request.TargetTenantId)
            .Must(value => !value.HasValue || value.Value != Guid.Empty)
            .WithMessage(WorkflowReasonCodes.WorkflowInvalidTargetScope);

        RuleFor(x => x.Request.TargetTenantId)
            .NotNull()
            .When(x => x.Request.TargetScope == WorkflowTransitionGateTargetScope.Tenant)
            .WithMessage(WorkflowReasonCodes.WorkflowTargetTenantRequired);

        RuleFor(x => x.Request.TargetTenantId)
            .Null()
            .When(x => (x.Request.TargetScope ?? WorkflowTransitionGateTargetScope.CurrentTenant) == WorkflowTransitionGateTargetScope.CurrentTenant)
            .WithMessage(WorkflowReasonCodes.WorkflowInvalidTargetScope);

        RuleFor(x => x.Request.TargetTenantSource)
            .NotEmpty()
            .MaximumLength(128)
            .When(x => x.Request.TargetScope == WorkflowTransitionGateTargetScope.Tenant)
            .WithMessage(WorkflowReasonCodes.WorkflowInvalidTargetScope);
    }
}
