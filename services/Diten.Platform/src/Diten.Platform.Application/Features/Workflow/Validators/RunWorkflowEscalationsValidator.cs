using Diten.Platform.Application.Features.Workflow.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.Workflow.Validators;

public sealed class RunWorkflowEscalationsValidator : AbstractValidator<RunWorkflowEscalationsCommand>
{
    public RunWorkflowEscalationsValidator()
    {
        RuleFor(x => x.Request.MaxItems)
            .GreaterThan(0)
            .LessThanOrEqualTo(500)
            .When(x => x.Request.MaxItems.HasValue);

        RuleFor(x => x.Request.IdempotencyKey)
            .MaximumLength(128)
            .When(x => x.Request.IdempotencyKey is not null);
    }
}
