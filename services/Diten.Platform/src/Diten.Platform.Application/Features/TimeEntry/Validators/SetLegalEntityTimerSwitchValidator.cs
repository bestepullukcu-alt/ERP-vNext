using Diten.Platform.Application.Features.TimeEntry.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.TimeEntry.Validators;

/// <summary>MOD-0280-FU01 D12 / R7 — switching the timer ON for a legal entity records its legal basis.</summary>
public sealed class SetLegalEntityTimerSwitchValidator : AbstractValidator<SetLegalEntityTimerSwitchCommand>
{
    public SetLegalEntityTimerSwitchValidator()
    {
        RuleFor(x => x.LegalEntityId)
            .NotEmpty()
            .WithErrorCode(TimeEntryReasonCodes.LegalEntityRequired);

        RuleFor(x => x.Request.ExpectedVersion).GreaterThanOrEqualTo(0);

        RuleFor(x => x.Request.Reason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= TimeEntryLimits.ReasonMaxLength)
            .WithErrorCode(TimeEntryReasonCodes.TimerSwitchReasonRequired)
            .WithMessage("Switching the timer on needs a reason (at most 1000 characters).")
            .When(x => x.Request.TimerEnabled);
    }
}
