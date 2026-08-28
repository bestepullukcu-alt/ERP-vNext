namespace Diten.Platform.Application.Features.DocumentManagementDowntime.Services;

/// <summary>
/// MOD-0029-FU20 — the two working-day rules of the downtime process (GMG-QMS-SOP-0001 §11.3): the 3-working-day
/// reconciliation deadline for a temporary controlled issue, and the 2-working-day downtime escalation threshold.
///
/// Working days are Monday–Friday only. A holiday calendar is deliberately NOT implemented — that is a
/// tenant-configurable concern for a later FU, and this calculator is the seam it would replace.
///
/// NOTE: MOD-0029-FU14 has an equivalent AddWorkingDays for its own 10-working-day rule. The duplication is
/// deliberate for now — each feature owns its schedule rules rather than depending on another feature's service
/// namespace. Extracting one shared working-day helper is recorded as a follow-up.
/// </summary>
public static class DowntimeScheduleCalculator
{
    /// <summary>SOP §11.3 — a temporary controlled issue must be reconciled within 3 working days.</summary>
    public const int ReconciliationWorkingDays = 3;

    /// <summary>SOP §11.3 — downtime beyond 2 working days requires GQD + IT/CSV escalation and a BCP assessment.</summary>
    public const int EscalationThresholdWorkingDays = 2;

    /// <summary>
    /// Adds working days (Mon–Fri) to a date. Day 0 is the start date itself, so the result is the date
    /// <paramref name="workingDays"/> business days later, skipping weekends.
    /// </summary>
    public static DateTimeOffset AddWorkingDays(DateTimeOffset from, int workingDays)
    {
        var result = from;
        var remaining = workingDays;
        while (remaining > 0)
        {
            result = result.AddDays(1);
            if (result.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                remaining--;
            }
        }

        return result;
    }

    /// <summary>
    /// Whole working days elapsed between two instants (weekends excluded). Used for the downtime duration and
    /// therefore for the 2-working-day escalation threshold.
    /// </summary>
    public static int CountWorkingDays(DateTimeOffset from, DateTimeOffset to)
    {
        if (to <= from)
        {
            return 0;
        }

        var days = 0;
        var cursor = from;
        while (cursor.Date < to.Date)
        {
            cursor = cursor.AddDays(1);
            if (cursor.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                days++;
            }
        }

        return days;
    }

    /// <summary>
    /// SOP §11.3 — the reconciliation deadline. The clock starts from whichever happens LATER: the moment the copy
    /// was issued, or the moment the normal repository came back. Reconciliation into the normal system is
    /// impossible while that system is still down, so a restore that follows the issue resets the window.
    /// </summary>
    public static DateTimeOffset ReconciliationDueDate(DateTimeOffset issuedAt, DateTimeOffset? restoredAt)
    {
        var start = restoredAt is { } restored && restored > issuedAt ? restored : issuedAt;
        return AddWorkingDays(start, ReconciliationWorkingDays);
    }

    /// <summary>True once the outage has run beyond the SOP's 2-working-day escalation threshold.</summary>
    public static bool ExceedsEscalationThreshold(int durationWorkingDays) =>
        durationWorkingDays > EscalationThresholdWorkingDays;
}
