using Diten.CrmService.Application.Features.VisitReport.Commands;
using FluentValidation;

namespace Diten.CrmService.Application.Features.VisitReport.Validators;

/// <summary>Cheap shape check: a report must link to a plan atom. The report-content rules (outcome code, samples,
/// actuals, immutability window) live in <see cref="VisitReportValidation"/> and the handler.</summary>
public sealed class SubmitVisitReportValidator : AbstractValidator<SubmitVisitReportCommand>
{
    public SubmitVisitReportValidator() => RuleFor(x => x.PlannedVisitId).NotEmpty();
}
