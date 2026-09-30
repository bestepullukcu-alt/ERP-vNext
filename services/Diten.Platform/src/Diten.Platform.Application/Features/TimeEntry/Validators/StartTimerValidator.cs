using Diten.Platform.Application.Features.TimeEntry.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.TimeEntry.Validators;

/// <summary>MOD-0280-FU01 §4.1 — a timer runs on exactly one task or one category.</summary>
public sealed class StartTimerValidator : AbstractValidator<StartTimerCommand>
{
    public StartTimerValidator()
    {
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.TaskItemId)
            .Must((command, _) => (command.Request.TaskItemId is { } task && task != Guid.Empty)
                                  ^ !string.IsNullOrWhiteSpace(command.Request.CategoryCode))
            .WithErrorCode(TimeEntryReasonCodes.TargetInvalid)
            .WithMessage("A timer runs on exactly one task or one category.")
            .When(x => x.Request is not null);
    }
}
