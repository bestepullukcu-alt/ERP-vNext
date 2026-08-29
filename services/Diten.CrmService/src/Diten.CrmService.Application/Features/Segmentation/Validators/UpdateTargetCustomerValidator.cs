using Diten.CrmService.Application.Features.Segmentation.Commands;
using Diten.CrmService.Domain.Entities;
using FluentValidation;

namespace Diten.CrmService.Application.Features.Segmentation.Validators;

/// <summary>Shape checks for updating a hand-written membership row. SubjectType and SubjectId are absent from the
/// command because they are immutable: a row always answers for the subject it was created for.</summary>
public sealed class UpdateTargetCustomerValidator : AbstractValidator<UpdateTargetCustomerCommand>
{
    public UpdateTargetCustomerValidator()
    {
        RuleFor(x => x.SegmentId).NotEmpty();
        RuleFor(x => x.TargetCustomerId).NotEmpty();
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
