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

        // WP-CL-BE-3 — optional display context (display only; the link must stay inside the app).
        When(x => x.Request.DisplayContext is not null, () =>
        {
            RuleFor(x => x.Request.DisplayContext!.Title).MaximumLength(WorkflowDisplayContext.MaxTitle);
            RuleFor(x => x.Request.DisplayContext!.Subtitle).MaximumLength(WorkflowDisplayContext.MaxSubtitle);
            RuleFor(x => x.Request.DisplayContext!.SourceModule).MaximumLength(WorkflowDisplayContext.MaxSourceModule);
            RuleFor(x => x.Request.DisplayContext!.DeepLinkUrl)
                .MaximumLength(WorkflowDisplayContext.MaxDeepLinkUrl)
                .Must(url => string.IsNullOrWhiteSpace(url) || WorkflowDisplayContext.IsRelativePath(url.Trim()))
                .WithMessage("DisplayContext.DeepLinkUrl must be an app-relative path starting with '/' "
                    + "(absolute, protocol-relative and scheme links are not allowed).");
            RuleFor(x => x.Request.DisplayContext!.Chips)
                .Must(chips => chips is null || chips.Count <= WorkflowDisplayContext.MaxChips)
                .WithMessage($"DisplayContext.Chips can hold at most {WorkflowDisplayContext.MaxChips} labels.");
            RuleForEach(x => x.Request.DisplayContext!.Chips)
                .NotEmpty()
                .MaximumLength(WorkflowDisplayContext.MaxChip)
                .When(x => x.Request.DisplayContext!.Chips is not null);
        });
    }
}
