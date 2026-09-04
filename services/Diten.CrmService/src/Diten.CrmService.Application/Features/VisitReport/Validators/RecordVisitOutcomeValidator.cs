using Diten.CrmService.Application.Features.VisitReport.Commands;
using FluentValidation;

namespace Diten.CrmService.Application.Features.VisitReport.Validators;

/// <summary>
/// Cheap shape checks that fail before the handler runs. The DEEP rules — the fail-closed vocabulary, the reason-code
/// rule, the orphan/1:1 guards, the edit window — live in <see cref="VisitReportValidation"/> and the handlers, because
/// they need other rows. Duplicating them here would create two sources of truth that drift.
/// </summary>
public sealed class RecordVisitOutcomeValidator : AbstractValidator<RecordVisitOutcomeCommand>
{
    public RecordVisitOutcomeValidator()
    {
        RuleFor(x => x.PlannedVisitId).NotEmpty();
        RuleFor(x => x.ExecutionOutcome).NotEmpty();
    }
}
