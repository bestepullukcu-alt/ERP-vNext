using Diten.CrmService.Application.Features.VisitReport.Commands;
using FluentValidation;

namespace Diten.CrmService.Application.Features.VisitReport.Validators;

/// <summary>Cheap shape check: an amendment needs a target report and a reason. The finalised-state guard, the
/// append-only trail and the optional-correction validity live in <see cref="VisitReportValidation"/> and the handler.</summary>
public sealed class AmendVisitReportValidator : AbstractValidator<AmendVisitReportCommand>
{
    public AmendVisitReportValidator()
    {
        RuleFor(x => x.VisitReportId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty();
    }
}
