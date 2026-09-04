using Diten.CrmService.Application.Features.PlannedVisit.Commands;
using Diten.CrmService.Domain.Entities;
using FluentValidation;

namespace Diten.CrmService.Application.Features.PlannedVisit.Validators;

/// <summary>Cheap shape checks for an edit. <c>VisitCode</c> is not here — it is never renamed. The deep rules live in
/// <see cref="PlannedVisitValidation"/> and the handler.</summary>
public sealed class UpdatePlannedVisitValidator : AbstractValidator<UpdatePlannedVisitCommand>
{
    public UpdatePlannedVisitValidator()
    {
        RuleFor(x => x.PlannedVisitId).NotEmpty();
        RuleFor(x => x.TargetType).NotEmpty();
        RuleFor(x => x.TargetId).NotEmpty();
        RuleFor(x => x.ResourceId).NotEmpty().MaximumLength(PlannedVisitLimits.MaxResourceIdLength);
        RuleFor(x => x.ResourceType).NotEmpty();
        RuleFor(x => x.VisitPurpose).NotEmpty();
        RuleFor(x => x.VisitType).NotEmpty();
        RuleFor(x => x.Objective!).MaximumLength(PlannedVisitLimits.MaxObjectiveLength).When(x => x.Objective is not null);
        RuleFor(x => x.Notes!).MaximumLength(PlannedVisitLimits.MaxNotesLength).When(x => x.Notes is not null);
    }
}
