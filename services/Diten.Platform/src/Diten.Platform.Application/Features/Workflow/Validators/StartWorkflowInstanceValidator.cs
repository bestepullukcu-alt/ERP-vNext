using Diten.Platform.Application.Features.Workflow.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.Workflow.Validators;

public sealed class StartWorkflowInstanceValidator : AbstractValidator<StartWorkflowInstanceCommand>
{
    public StartWorkflowInstanceValidator()
    {
        RuleFor(x => x.Request)
            .Must(x => (x.TemplateId.HasValue && x.TemplateId.Value != Guid.Empty) || !string.IsNullOrWhiteSpace(x.TemplateCode))
            .WithMessage("TemplateId or TemplateCode is required.");

        RuleFor(x => x.Request.ObjectType)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Request.ObjectId)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Request.ObjectRef)
            .MaximumLength(512)
            .When(x => x.Request.ObjectRef is not null);

        RuleFor(x => x.Request.CandidatePrincipalIds)
            .NotEmpty()
            .WithMessage("At least one candidate principal ID is required.");

        RuleForEach(x => x.Request.CandidatePrincipalIds)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.Request.ReasonCode)
            .MaximumLength(128)
            .When(x => x.Request.ReasonCode is not null);

        RuleFor(x => x.Request.IdempotencyKey)
            .MaximumLength(128)
            .When(x => x.Request.IdempotencyKey is not null);
    }
}
