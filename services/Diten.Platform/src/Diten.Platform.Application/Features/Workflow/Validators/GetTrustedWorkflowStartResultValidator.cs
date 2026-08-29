using Diten.Platform.Application.Features.Workflow.Queries;
using FluentValidation;

namespace Diten.Platform.Application.Features.Workflow.Validators;

public sealed class GetTrustedWorkflowStartResultValidator : AbstractValidator<GetTrustedWorkflowStartResultQuery>
{
    public GetTrustedWorkflowStartResultValidator()
    {
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128).Must(IsExact);
        RuleFor(x => x.ServiceClientId).NotEmpty();
        RuleFor(x => x.ExpectedMakerSubjectId).NotEmpty();
        RuleFor(x => x.ExpectedObjectType).NotEmpty().MaximumLength(128).Must(IsExact);
        RuleFor(x => x.ExpectedObjectId).NotEmpty().MaximumLength(256).Must(IsExact);
        RuleFor(x => x.CorrelationId).NotEmpty().MaximumLength(128);
    }

    private static bool IsExact(string? value) =>
        value is not null
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl);
}
