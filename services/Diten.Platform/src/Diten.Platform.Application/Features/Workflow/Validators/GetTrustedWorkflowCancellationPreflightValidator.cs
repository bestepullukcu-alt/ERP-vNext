using Diten.Platform.Application.Features.Workflow.Queries;
using FluentValidation;

namespace Diten.Platform.Application.Features.Workflow.Validators;

public sealed class GetTrustedWorkflowCancellationPreflightValidator
    : AbstractValidator<GetTrustedWorkflowCancellationPreflightQuery>
{
    public GetTrustedWorkflowCancellationPreflightValidator()
    {
        RuleFor(x => x.ServiceClientId).NotEmpty();
        RuleFor(x => x.WorkflowInstanceId).NotEmpty();
        RuleFor(x => x.ApprovalTaskId).NotEmpty();
        RuleFor(x => x.ExpectedObjectType).NotEmpty().MaximumLength(128);
        RuleFor(x => x.ExpectedObjectId).NotEmpty().MaximumLength(256);
        RuleFor(x => x.ExpectedMakerSubjectId).NotEmpty();
    }
}
