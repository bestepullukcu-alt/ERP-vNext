using Diten.CrmService.Application.Features.PlannedVisit.Commands;
using Diten.CrmService.Domain.Entities;
using FluentValidation;

namespace Diten.CrmService.Application.Features.PlannedVisit.Validators;

/// <summary>
/// Cheap shape checks that fail before the handler runs. The DEEP rules — the vocabulary sets, the time-window relation,
/// target existence, the journey/stage validity, code uniqueness and the legacy overlap / same-day guards — live in
/// <see cref="PlannedVisitValidation"/> and the handlers/probes, because they need other rows or other modules.
/// Duplicating them here would create two sources of truth that drift.
/// </summary>
public sealed class CreatePlannedVisitValidator : AbstractValidator<CreatePlannedVisitCommand>
{
    public CreatePlannedVisitValidator()
    {
        RuleFor(x => x.VisitCode).NotEmpty().MaximumLength(PlannedVisitLimits.MaxVisitCodeLength);
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
