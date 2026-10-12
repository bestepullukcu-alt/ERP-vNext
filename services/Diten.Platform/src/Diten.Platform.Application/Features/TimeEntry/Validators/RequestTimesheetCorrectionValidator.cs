using Diten.Platform.Application.Features.TimeEntry.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.TimeEntry.Validators;

/// <summary>MOD-0280-FU01 D5 — a correction of an approved week always says why.</summary>
public sealed class RequestTimesheetCorrectionValidator : AbstractValidator<RequestTimesheetCorrectionCommand>
{
    public RequestTimesheetCorrectionValidator()
    {
        RuleFor(x => x.Request.Reason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= TimeEntryLimits.ReasonMaxLength)
            .WithErrorCode(TimeEntryReasonCodes.CorrectionReasonRequired)
            .WithMessage("A correction needs a reason (at most 1000 characters).");
    }
}
