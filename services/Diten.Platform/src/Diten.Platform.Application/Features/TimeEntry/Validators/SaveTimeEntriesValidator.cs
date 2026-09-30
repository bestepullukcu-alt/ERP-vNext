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

            // 15-minute steps, 15…960 (A3) — for the person's own rows. A captured (timer/meeting) row is checked by the
            // handler, and only when it actually changed (v3 G3): one sent back untouched may carry a 17-hour timer day.
            row.RuleFor(r => r.DurationMinutes)
                .Must(m => m >= TimeEntryLimits.StepMinutes
                           && m <= TimeEntryLimits.MaxRowMinutes
                           && m % TimeEntryLimits.StepMinutes == 0)
                .WithErrorCode(TimeEntryReasonCodes.StepInvalid)
                .WithMessage("Durations are whole 15-minute steps between 15 and 960 minutes.")
                .When(r => !IsCaptured(r.Source));

            // v2 F1 — every row names its source: a timer row sent back without one must not become a second, Manual
            // copy of the same minutes.
            row.RuleFor(r => r.Source)
                .NotEmpty()
                .WithErrorCode(TimeEntryReasonCodes.SourceRequired)
                .WithMessage("Each row names its source (see TimeEntrySource).");
            // v3 G5 — exactly one of the declared NAMES. Enum.TryParse would also take "Timer,Timer", "Manual,Meeting" (a flags
            // combination) or a number; none of those is a source.
            row.RuleFor(r => r.Source)
                .Must(source => SourceNames.Contains(source!.Trim()))
                .WithErrorCode(TimeEntryReasonCodes.SourceInvalid)
                .WithMessage("A row's source is one of TimeEntrySource's names.")
                .When(r => !string.IsNullOrWhiteSpace(r.Source));

            row.RuleFor(r => r.Note)
                .MaximumLength(TimeEntryLimits.NoteMaxLength)
                .WithErrorCode(TimeEntryReasonCodes.NoteTooLong)
                .When(r => r.Note is not null);
        }).When(x => x.Request?.Entries is not null);

        // One row per cell and KIND: the person's own (Manual/Plan share a cell), the timer's, and one per meeting.
        RuleFor(x => x.Request.Entries)
            .Must(rows => rows!
                .GroupBy(r => (r.LocalDate, r.TaskItemId, Category: r.CategoryCode?.Trim().ToUpperInvariant(), Kind: KindOf(r)))
                .All(g => g.Count() == 1))
            .WithErrorCode(TimeEntryReasonCodes.DuplicateRow)
            .WithMessage("A day holds one row per task or category.")
            .When(x => x.Request?.Entries is not null);
    }

    private static string KindOf(TimeEntryRowRequest row) => row.Source?.Trim().ToUpperInvariant() switch
    {
        "TIMER" => "timer",
        "MEETING" => "meeting:" + row.SourceRef,
        _ => "typed"
    };

    /// <summary>The declared source names, case-insensitive — the ONLY accepted spellings.</summary>
    private static readonly HashSet<string> SourceNames =
        new(Enum.GetNames<Domain.Enums.TimeEntry.TimeEntrySource>(), StringComparer.OrdinalIgnoreCase);

    private static bool IsCaptured(string? source)
        => string.Equals(source?.Trim(), nameof(Domain.Enums.TimeEntry.TimeEntrySource.Timer), StringComparison.OrdinalIgnoreCase)
           || string.Equals(source?.Trim(), CapturedMeetingName, StringComparison.OrdinalIgnoreCase);

    /// <summary>The meeting source's name, spelled through the enum (the module guard forbids the bare word).</summary>
    private static readonly string CapturedMeetingName = Domain.Enums.TimeEntry.TimeEntrySource.Meeting.ToString();
}
