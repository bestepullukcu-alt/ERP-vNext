using Diten.CrmService.Application.Features.Segmentation.Commands;
using Diten.CrmService.Domain.Entities;
using FluentValidation;

namespace Diten.CrmService.Application.Features.Segmentation.Validators;

/// <summary>Shape checks for a hand-written membership row. The rules that need the SEGMENT — subject-type match, the
/// dynamic-segment refusal, uniqueness — are enforced in the handler, where the segment is actually available.</summary>
public sealed class AddTargetCustomerValidator : AbstractValidator<AddTargetCustomerCommand>
{
    public AddTargetCustomerValidator()
    {
        RuleFor(x => x.SegmentId).NotEmpty();
        RuleFor(x => x.SubjectType).NotEmpty();
        RuleFor(x => x.SubjectId).NotEmpty();
        RuleFor(x => x.MembershipMode).NotEmpty();

        RuleFor(x => x.SelectionReason)
            .NotEmpty()
            .WithMessage("A manual membership without a reason is not authorable.")
            .MaximumLength(SegmentLimits.MaxSelectionReasonLength);

        RuleFor(x => x.ReasonCodes)
            .NotEmpty()
            .WithMessage("At least one ReasonCode is required.");

        RuleFor(x => x.Notes!)
            .MaximumLength(SegmentLimits.MaxNotesLength)
            .When(x => x.Notes is not null);
    }
}
