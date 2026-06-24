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
    }
}
