using Diten.Platform.Application.Features.Workflow.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.Workflow.Validators;

public sealed class CreateSlaEscalationRuleValidator : AbstractValidator<CreateSlaEscalationRuleCommand>
{
    public CreateSlaEscalationRuleValidator()
    {
        RuleFor(x => x.Request.TemplateId).NotEmpty();
        RuleFor(x => x.Request.StageCode).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Request.StepCode).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Request.DueInMinutes).GreaterThan(0);
        RuleFor(x => x.Request.EscalateAfterMinutes)
            .GreaterThanOrEqualTo(x => x.Request.DueInMinutes);
        RuleFor(x => x.Request.TimeoutAfterMinutes)
            .Must((request, value) => !value.HasValue || value.Value >= request.Request.EscalateAfterMinutes)
            .WithMessage("TimeoutAfterMinutes must be null or greater than or equal to EscalateAfterMinutes.");
        RuleFor(x => x.Request.EscalationPrincipalIds).NotEmpty();
        RuleForEach(x => x.Request.EscalationPrincipalIds).NotEmpty().MaximumLength(256);
    }
}
