using Diten.CrmService.Domain.Entities;
using PlannedVisitEntity = Diten.CrmService.Domain.Entities.PlannedVisit;
using VisitReportEntity = Diten.CrmService.Domain.Entities.VisitReport;

namespace Diten.CrmService.Application.Features.VisitReport;

/// <summary>
/// WP-VW-W1 — the visit's DERIVED work status: one code the calendar (Web W2) and mobile read instead of re-deriving it
/// from plan status + report state + the clock. It is <b>never stored</b> (no field, no migration, no index): it is
/// computed at read time from the plan atom, its report (if any) and "now". Pure — the clock is passed in.
/// <para><b>Order is priority</b>; the first rule that holds wins:
/// cancelled &gt; not_done &gt; rescheduled &gt; reported &gt; expired &gt; report_missing &gt; missed &gt; today &gt; planned.
/// A DRAFT report whose outcome is <c>missed</c> / <c>rescheduled</c> is not a result yet: the visit stays on its day
/// rule (<c>missed</c> once the day is past, <c>expired</c> after the deadline) until the report is submitted.</para>
/// <para><c>in_progress</c> is deliberately NOT here: it arrives with W3's start / finish record and will sit just
/// before <c>today</c>.</para>
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
    // W3: in_progress goes here (before today).
    public const string Today = "today";
    public const string Planned = "planned";

    /// <summary>Every code, in priority order (published on the contract as <c>workStatuses</c>).</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        Cancelled, NotDone, Rescheduled, Reported, Expired, ReportMissing, Missed, Today, Planned
    };

    public static bool IsKnown(string? value)
        => value is not null && All.Contains(value.Trim().ToLowerInvariant(), StringComparer.Ordinal);

    public static string Derive(PlannedVisitEntity plan, VisitReportEntity? report, DateTimeOffset now)
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
