using Diten.CrmService.Application.Features.StrategyTemplate.Commands;
using Diten.CrmService.Domain.Entities;
using FluentValidation;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Validators;

/// <summary>Shape checks for the update path. The binding lists stay nullable here on purpose: null means "leave this
/// binding alone", and only a supplied list is size-checked.</summary>
public sealed class UpdateStrategyTemplateValidator : AbstractValidator<UpdateStrategyTemplateCommand>
{
    public UpdateStrategyTemplateValidator()
    {
        RuleFor(x => x.TemplateId).NotEmpty();

        RuleFor(x => x.TemplateName)
            .NotEmpty()
            .MaximumLength(StrategyTemplateLimits.MaxTemplateNameLength);

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
