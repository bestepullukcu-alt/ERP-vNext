using Diten.CrmService.Application.Features.Segmentation.Commands;
using Diten.CrmService.Domain.Entities;
using FluentValidation;

namespace Diten.CrmService.Application.Features.Segmentation.Validators;

/// <summary>
/// Cheap shape checks that fail before a handler ever runs. The DEEP rules — catalog conformance, operator arity,
/// tree depth and cycles, the freeze guard, cross-service value proof — live in the handler and in
/// <see cref="SegmentValidation"/>, because they need the segment, the catalog and (for class X) a dependency call.
/// Duplicating them here would create two sources of truth that drift.
/// </summary>
public sealed class CreateSegmentValidator : AbstractValidator<CreateSegmentCommand>
{
    public CreateSegmentValidator()
    {
        RuleFor(x => x.SegmentCode)
            .NotEmpty()
            .MaximumLength(SegmentLimits.MaxSegmentCodeLength);

        RuleFor(x => x.SegmentName)
            .NotEmpty()
            .MaximumLength(SegmentLimits.MaxSegmentNameLength);

        RuleFor(x => x.SegmentType).NotEmpty();
        RuleFor(x => x.SubjectType).NotEmpty();
        RuleFor(x => x.MatchMode).NotEmpty();

        RuleFor(x => x.Description!)
            .MaximumLength(SegmentLimits.MaxDescriptionLength)
            .When(x => x.Description is not null);

        RuleFor(x => x.Notes!)
            .MaximumLength(SegmentLimits.MaxNotesLength)
            .When(x => x.Notes is not null);

        RuleFor(x => x.Criteria!)
            .Must(c => c.Count <= SegmentLimits.MaxCriteriaNodes)
            .WithMessage($"A criteria tree may hold at most {SegmentLimits.MaxCriteriaNodes} nodes.")
            .When(x => x.Criteria is not null);
    }
}
