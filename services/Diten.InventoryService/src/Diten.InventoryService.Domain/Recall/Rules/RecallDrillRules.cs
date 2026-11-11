using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Enums;

namespace Diten.InventoryService.Domain.Recall.Rules;

/// <summary>
/// WP-0177-R5 — the KR-G drill rules (pack §4.11, §4.12, §8.10). Pure: the step order, the server-timed duration and pass / fail, the plan
/// shape, the result note, the frequency warning.
/// </summary>
public static class RecallDrillRules
{
    public const int MaxNameLength = 200;
    public const int MaxCustomDays = 730;
    public const int MinNoteLength = 10;
    public const int MaxNoteLength = 2000;
    public const int AutoLookbackDays = 365;

    /// <summary>The shared INVENTORY code (the Application layer's <c>InventoryErrorCodes.InvalidRequest</c>; the Domain cannot reference it).</summary>
    private const string InvalidRequest = "INVALID_REQUEST";

    public static readonly IReadOnlyList<RecallDrillStep> StepOrder = [RecallDrillStep.Trace, RecallDrillStep.Scope, RecallDrillStep.CustomerList];

    public static bool IsClosed(RecallDrillRunStatus status) => status is RecallDrillRunStatus.Completed or RecallDrillRunStatus.Cancelled;

    /// <summary>The step that comes next, or null when all three are done.</summary>
    public static RecallDrillStep? NextStep(RecallDrillRun run) => run.Steps.Count < StepOrder.Count ? StepOrder[run.Steps.Count] : null;

    /// <summary>409 <c>RECALL_DRILL_STEP_ORDER</c> unless <paramref name="step"/> is the next one.</summary>
    public static RecallViolation? StepOrderRefusal(RecallDrillRun run, RecallDrillStep step)
        => NextStep(run) == step
            ? null
            : new RecallViolation(RecallErrorCodes.DrillStepOrder,
                NextStep(run) is { } next ? $"The next step is {next}, not {step}." : "All three steps are done.", 409);

    /// <summary><c>ceil((EndedAtUtc − StartedAtUtc) / 1 min)</c> — both server times.</summary>
    public static int DurationMinutes(DateTimeOffset startedAtUtc, DateTimeOffset endedAtUtc)
        => (int)Math.Max(0, Math.Ceiling((endedAtUtc - startedAtUtc).TotalMinutes));

    /// <summary><c>DurationMinutes ≤ TargetHours × 60</c> (the target the run started with).</summary>
    public static bool Passed(int durationMinutes, int targetHours) => durationMinutes <= targetHours * 60;

    public static RecallViolation? Plan(string? name, RecallDrillFrequency? frequency, int? customDays, RecallDrillScopeKind? scopeKind, DateOnly? nextDueOn,
        Guid? ownerUserId)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength) return Invalid($"name must be 1-{MaxNameLength} characters.");
        if (frequency is null) return Invalid("frequency must be Yearly1, Yearly2, Quarterly or CustomDays.");
        if (frequency == RecallDrillFrequency.CustomDays && customDays is not (>= 1 and <= MaxCustomDays))
        {
            return Invalid($"customDays (1-{MaxCustomDays}) is required for frequency CustomDays.");
        }

        if (frequency != RecallDrillFrequency.CustomDays && customDays is not null) return Invalid("customDays is only for frequency CustomDays.");
        if (scopeKind is null) return Invalid("scopeKind must be RandomFinishedGoodLot, RawMaterialLot or SelectedLot.");
        if (nextDueOn is null) return Invalid("nextDueOn must be a date (yyyy-MM-dd).");
        return ownerUserId is null || ownerUserId == Guid.Empty ? Invalid("ownerUserId must be a user id.") : null;
    }

    /// <summary>The result note (10–2000) — 400 <c>REASON_REQUIRED</c> otherwise.</summary>
    public static RecallViolation? Note(string? note, string member = "resultNote")
        => note?.Trim() is { Length: >= MinNoteLength and <= MaxNoteLength }
            ? null
            : new RecallViolation(RecallErrorCodes.ReasonRequired, $"{member} must be {MinNoteLength}-{MaxNoteLength} characters.", 400);

    /// <summary>The frequency warning (pack §8.9): days since the last COMPLETED run &gt; <paramref name="warningDays"/>; no completed run = warning.</summary>
    public static (int? DaysSince, bool Warning) Frequency(DateTimeOffset? lastCompletedAtUtc, DateTimeOffset now, int warningDays)
    {
        if (lastCompletedAtUtc is not { } last) return (null, true);
        var days = (int)Math.Floor((now - last).TotalDays);
        return (days, days > warningDays);
    }

    private static RecallViolation Invalid(string message) => new(InvalidRequest, message, 400);
}
