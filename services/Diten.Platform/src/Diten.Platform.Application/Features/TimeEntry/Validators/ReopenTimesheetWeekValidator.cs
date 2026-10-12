using Diten.Platform.Application.Features.TimeEntry.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.TimeEntry.Validators;

/// <summary>MOD-0280-FU01 A9 — reopening a week older than the edit window always says why.</summary>
public sealed class ReopenTimesheetWeekValidator : AbstractValidator<ReopenTimesheetWeekCommand>
{
    public ReopenTimesheetWeekValidator()
    {
        RuleFor(x => x.Request.UserId).NotEmpty().WithErrorCode(TimeEntryReasonCodes.PersonNotFound);
        RuleFor(x => x.Request.ExpectedVersion).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.Reason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= TimeEntryLimits.ReasonMaxLength)
            .WithErrorCode(TimeEntryReasonCodes.ReopenReasonRequired)
            .WithMessage("A reopen needs a reason (at most 1000 characters).");
    }
}
