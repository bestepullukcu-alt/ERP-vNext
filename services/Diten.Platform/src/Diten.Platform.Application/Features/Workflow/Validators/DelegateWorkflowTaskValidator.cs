using Diten.Platform.Application.Features.Workflow.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.Workflow.Validators;

public sealed class DelegateWorkflowTaskValidator : AbstractValidator<DelegateWorkflowTaskCommand>
{
    public DelegateWorkflowTaskValidator()
    {
        RuleFor(x => x.TaskId).NotEmpty();
        RuleFor(x => x.Request.ActorId).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Request.DelegatePrincipalId)
            .NotEmpty()
            .WithMessage(WorkflowReasonCodes.WorkflowDelegatePrincipalRequired)
            .MaximumLength(256);
        RuleFor(x => x.Request)
            .Must(x => !string.Equals(x.ActorId?.Trim(), x.DelegatePrincipalId?.Trim(), StringComparison.Ordinal))
            .WithMessage(WorkflowReasonCodes.WorkflowDelegateSameActorInvalid);
        RuleFor(x => x.Request.ReasonCode).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Request.IdempotencyKey).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Request.Comment).MaximumLength(2000).When(x => x.Request.Comment is not null);
    }
}
