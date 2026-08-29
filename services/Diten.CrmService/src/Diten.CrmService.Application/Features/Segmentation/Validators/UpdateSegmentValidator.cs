using Diten.CrmService.Application.Features.Segmentation.Commands;
using Diten.CrmService.Domain.Entities;
using FluentValidation;

namespace Diten.CrmService.Application.Features.Segmentation.Validators;

/// <summary>Shape checks for the update path. Note what is NOT validated here because it cannot be changed at all:
/// SegmentCode and SubjectType are absent from the command by design.</summary>
public sealed class UpdateSegmentValidator : AbstractValidator<UpdateSegmentCommand>
{
    public UpdateSegmentValidator()
    {
        RuleFor(x => x.SegmentId).NotEmpty();

        RuleFor(x => x.SegmentName)
            .NotEmpty()
            .MaximumLength(SegmentLimits.MaxSegmentNameLength);

        RuleFor(x => x.SegmentType).NotEmpty();
        RuleFor(x => x.SegmentStatus).NotEmpty();
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
