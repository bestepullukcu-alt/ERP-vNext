using Diten.Platform.Application.Features.Workflow.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.Workflow.Validators;

public sealed class RejectWorkflowTaskValidator : AbstractValidator<RejectWorkflowTaskCommand>
{
    public RejectWorkflowTaskValidator()
    {
        RuleFor(x => x.TaskId).NotEmpty();
        RuleFor(x => x.Request.ActorId).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Request.ReasonCode).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Request.IdempotencyKey).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Request.Comment).MaximumLength(2000).When(x => x.Request.Comment is not null);
        RuleFor(x => x.Request.EvidenceRef).MaximumLength(512).When(x => x.Request.EvidenceRef is not null);
    }
}
