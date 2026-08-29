using Diten.CrmService.Application.Features.StrategyTemplate.Commands;
using Diten.CrmService.Domain.Entities;
using FluentValidation;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Validators;

/// <summary>
/// Cheap shape checks that fail before a handler ever runs. The DEEP rules — the percentage arithmetic, the frequency
/// intent shape, the binding proofs and the cross-service reference proof — live in the handler and in
/// <see cref="StrategyTemplateValidation"/>, because they need the template, the referenced aggregates and (for MDM) a
/// dependency call. Duplicating them here would create two sources of truth that drift.
/// </summary>
public sealed class CreateStrategyTemplateValidator : AbstractValidator<CreateStrategyTemplateCommand>
{
    public CreateStrategyTemplateValidator()
    {
        RuleFor(x => x.TemplateCode)
            .NotEmpty()
            .MaximumLength(StrategyTemplateLimits.MaxTemplateCodeLength);

        RuleFor(x => x.TemplateName)
            .NotEmpty()
            .MaximumLength(StrategyTemplateLimits.MaxTemplateNameLength);

        RuleFor(x => x.SubjectType).NotEmpty();

        RuleFor(x => x.Description!)
            .MaximumLength(StrategyTemplateLimits.MaxDescriptionLength)
            .When(x => x.Description is not null);

        RuleFor(x => x.Notes!)
            .MaximumLength(StrategyTemplateLimits.MaxNotesLength)
            .When(x => x.Notes is not null);

        RuleFor(x => x.SegmentBindings!)
            .Must(b => b.Count <= StrategyTemplateLimits.MaxSegmentBindings)
            .WithMessage($"A template may bind at most {StrategyTemplateLimits.MaxSegmentBindings} segments.")
            .When(x => x.SegmentBindings is not null);

        RuleFor(x => x.ProductLines!)
            .Must(l => l.Count <= StrategyTemplateLimits.MaxProductLines)
            .WithMessage($"A template may carry at most {StrategyTemplateLimits.MaxProductLines} product lines.")
            .When(x => x.ProductLines is not null);

        RuleFor(x => x.ContentBindings!)
            .Must(c => c.Count <= StrategyTemplateLimits.MaxContentBindings)
            .WithMessage($"A template may bind at most {StrategyTemplateLimits.MaxContentBindings} content rows.")
            .When(x => x.ContentBindings is not null);
    }
}
