using Diten.CrmService.Domain.Entities;
using PlannedVisitEntity = Diten.CrmService.Domain.Entities.PlannedVisit;
using VisitReportEntity = Diten.CrmService.Domain.Entities.VisitReport;
using VisitExecutionEntity = Diten.CrmService.Domain.Entities.VisitExecution;

namespace Diten.CrmService.Application.Features.VisitReport;

/// <summary>
/// WP-VW-W1 — the visit's DERIVED work status: one code the calendar (Web W2) and mobile read instead of re-deriving it
/// from plan status + report state + the clock. It is <b>never stored</b> (no field, no migration, no index): it is
/// computed at read time from the plan atom, its report (if any) and "now". Pure — the clock is passed in.
/// <para><b>Order is priority</b>; the first rule that holds wins:
/// cancelled &gt; not_done &gt; rescheduled &gt; reported &gt; expired &gt; report_missing &gt; missed &gt; in_progress &gt;
/// today &gt; planned.
/// A DRAFT report whose outcome is <c>missed</c> / <c>rescheduled</c> is not a result yet: the visit stays on its day
/// rule (<c>missed</c> once the day is past, <c>expired</c> after the deadline) until the report is submitted.</para>
/// <para>WP-VW-W3 (BE-a) — <c>in_progress</c>: the visit's execution is started and not finished. It sits just before
/// <c>today</c>; once the execution is finished the earlier rules apply again (<c>today</c> until the report …).</para>
/// </summary>
public static class VisitWorkStatus
{
    public const string Cancelled = "cancelled";
    public const string NotDone = "not_done";
    public const string Rescheduled = "rescheduled";
    public const string Reported = "reported";
    public const string Expired = "expired";
    public const string ReportMissing = "report_missing";
    public const string Missed = "missed";
    public const string InProgress = "in_progress";
    public const string Today = "today";
    public const string Planned = "planned";

    /// <summary>Every code, in priority order (published on the contract as <c>workStatuses</c>).</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        Cancelled, NotDone, Rescheduled, Reported, Expired, ReportMissing, Missed, InProgress, Today, Planned
    };

    public static bool IsKnown(string? value)
        => value is not null && All.Contains(value.Trim().ToLowerInvariant(), StringComparer.Ordinal);

    public static string Derive(
        PlannedVisitEntity plan, VisitReportEntity? report, DateTimeOffset now, VisitExecutionEntity? execution = null)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.IsCancelled())
        {
            return Cancelled;
        }

        if (report is not null && report.IsFinalised())
        {
            return report.ExecutionOutcome switch
            {
                VisitExecutionOutcome.Missed => NotDone,
                VisitExecutionOutcome.Rescheduled => Rescheduled,
                _ => Reported
            };
        }

        if (VisitReportDeadline.IsPassed(plan.PlannedDate, now))
        {
            return Expired;
        }

        if (report is not null && report.IsCompleted())
        {
            return ReportMissing;
        }

        // WP-VW-W3 (CT, E2) — the execution outranks the calendar day: a FINISHED visit happened and waits for its report
        // (report_missing, never "missed" the next day); a STARTED one is in progress until it is finished or the 48 h
        // window closes (expired above), whatever the day.
        if (execution is not null && execution.IsFinished())
        {
            return ReportMissing;
        }

        if (execution is not null && execution.IsStarted())
        {
            return InProgress;
        }

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        if (plan.PlannedDate < today)
        {
            return Missed;
        }

        return plan.PlannedDate == today ? Today : Planned;
    }

    /// <summary>The "lock + notify the manager" flag: the deadline passed with no submitted report. Read-time only;
    /// W5's e-mail takes this flag as its source.</summary>
    public static bool NeedsManagerAttention(string workStatus)
        => string.Equals(workStatus, Expired, StringComparison.Ordinal);
}
