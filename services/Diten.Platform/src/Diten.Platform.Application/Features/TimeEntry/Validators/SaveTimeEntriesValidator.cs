using Diten.Platform.Application.Features.TimeEntry.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.TimeEntry.Validators;

/// <summary>
/// MOD-0280-FU01 §12 — the row-shape rules, each with the pack's own reason code. The rules that need the week or the
/// calendar (future date, day totals, window, category state) live in the handler.
/// </summary>
public sealed class SaveTimeEntriesValidator : AbstractValidator<SaveTimeEntriesCommand>
{
    public SaveTimeEntriesValidator()
    {
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.ExpectedVersion).GreaterThanOrEqualTo(0).When(x => x.Request is not null);
        RuleFor(x => x.Request.Entries).NotNull().When(x => x.Request is not null);

        RuleForEach(x => x.Request.Entries).ChildRules(row =>
        {
            // Exactly one of task / category (D4).
            row.RuleFor(r => r.TaskItemId)
                .Must((r, _) => (r.TaskItemId is { } task && task != Guid.Empty) ^ !string.IsNullOrWhiteSpace(r.CategoryCode))
                .WithErrorCode(TimeEntryReasonCodes.TargetInvalid)
                .WithMessage("Each row names exactly one task or one category.");

            // 15-minute steps, 15…960 (A3).
            row.RuleFor(r => r.DurationMinutes)
                .Must(m => m >= TimeEntryLimits.StepMinutes
                           && m <= TimeEntryLimits.MaxRowMinutes
                           && m % TimeEntryLimits.StepMinutes == 0)
                .WithErrorCode(TimeEntryReasonCodes.StepInvalid)
                .WithMessage("Durations are whole 15-minute steps between 15 and 960 minutes.");

            // T1b (D9) — the person writes Manual rows and accepts Plan ones; the timer and meeting paths are their own.
            row.RuleFor(r => r.Source)
                .Must(source => source is null
                                || string.Equals(source, "Manual", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(source, "Plan", StringComparison.OrdinalIgnoreCase))
                .WithErrorCode(TimeEntryReasonCodes.SourceInvalid)
                .WithMessage("A saved row is Manual or Plan.");

            row.RuleFor(r => r.Note)
                .MaximumLength(TimeEntryLimits.NoteMaxLength)
                .WithErrorCode(TimeEntryReasonCodes.NoteTooLong)
                .When(r => r.Note is not null);
        }).When(x => x.Request?.Entries is not null);

        RuleFor(x => x.Request.Entries)
            .Must(rows => rows!
                .GroupBy(r => (r.LocalDate, r.TaskItemId, Category: r.CategoryCode?.Trim().ToUpperInvariant()))
                .All(g => g.Count() == 1))
            .WithErrorCode(TimeEntryReasonCodes.DuplicateRow)
            .WithMessage("A day holds one row per task or category.")
            .When(x => x.Request?.Entries is not null);
    }
}
