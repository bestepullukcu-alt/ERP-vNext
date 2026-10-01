using Diten.Platform.Application.Features.Workflow.Queries;
using FluentValidation;

namespace Diten.Platform.Application.Features.Workflow.Validators;

public sealed class GetTrustedWorkflowTerminalDecisionEvidenceValidator
    : AbstractValidator<GetTrustedWorkflowTerminalDecisionEvidenceQuery>
{
    public GetTrustedWorkflowTerminalDecisionEvidenceValidator()
    {
        RuleFor(x => x.WorkflowInstanceId).NotEmpty();
        RuleFor(x => x.TrustedConsumerClientId).NotEmpty();
        RuleFor(x => x.ExpectedObjectType)
            .NotEmpty()
            .MaximumLength(128)
            .Must(BeExactBoundedFact);
        RuleFor(x => x.ExpectedObjectId)
            .NotEmpty()
            .MaximumLength(256)
            .Must(BeExactBoundedFact);
        RuleFor(x => x.CorrelationId).NotEmpty().MaximumLength(128);
    }

    private static bool BeExactBoundedFact(string? value) =>
        value is not null && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
