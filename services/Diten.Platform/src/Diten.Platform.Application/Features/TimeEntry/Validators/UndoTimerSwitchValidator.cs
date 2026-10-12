using Diten.Platform.Application.Features.TimeEntry.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.TimeEntry.Validators;

/// <summary>MOD-0280-FU01 §19.1 — an undo names the switch it undoes.</summary>
public sealed class UndoTimerSwitchValidator : AbstractValidator<UndoTimerSwitchCommand>
{
    public UndoTimerSwitchValidator()
    {
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.SwitchToken)
            .NotEmpty()
            .WithErrorCode(TimeEntryReasonCodes.TimerUndoExpired)
            .When(x => x.Request is not null);
    }
}
