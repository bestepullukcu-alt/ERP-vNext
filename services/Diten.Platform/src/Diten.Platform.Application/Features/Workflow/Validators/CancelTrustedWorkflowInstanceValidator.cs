using Diten.Platform.Application.Features.Workflow.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.Workflow.Validators;

public sealed class CancelTrustedWorkflowInstanceValidator : AbstractValidator<CancelTrustedWorkflowInstanceCommand>
{
    public CancelTrustedWorkflowInstanceValidator()
    {
        RuleFor(x => x.ServiceClientId).NotEmpty();
        RuleFor(x => x.DelegatedRequesterUserId).NotEmpty();
        RuleFor(x => x.Request.WorkflowInstanceId).NotEmpty();
        RuleFor(x => x.Request.ApprovalTaskId).NotEmpty();
        RuleFor(x => x.Request.ExpectedObjectType).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Request.ExpectedObjectId).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Request.ExpectedMakerSubjectId).NotEmpty();
        RuleFor(x => x.Request.ExpectedWorkflowInstanceVersion).GreaterThan(0);
        RuleFor(x => x.Request.ExpectedApprovalTaskVersion).GreaterThan(0);
        RuleFor(x => x.Request.ReasonCode).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Request.Comment).MaximumLength(1000);
        RuleFor(x => x.Request.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}
