using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.VisitReport;

/// <summary>
/// WP-VW-W1 — the ONE report-deadline rule. The write rule (record outcome / submit → 409 visit_report_deadline_passed)
/// and the derived work status (<see cref="VisitWorkStatus"/> → <c>expired</c>) both read it from here, so the lock and
/// the badge can never disagree.
/// <para>The deadline is the end of the planned day (UTC, the CRM holds no tenant time zone) plus
/// <see cref="VisitReportLimits.ReportDeadlineHours"/>: a Thursday visit is open until Saturday 23:59:59Z and locked from
/// Sunday 00:00:00Z.</para>
/// </summary>
public static class VisitReportDeadline
{
    /// <summary>The first instant at which the visit is locked (exclusive end of the open window).</summary>
    public static DateTimeOffset LocksAt(DateOnly plannedDate)
        => new DateTimeOffset(plannedDate.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            .AddHours(VisitReportLimits.ReportDeadlineHours);

    /// <summary>The last whole second the visit can still be reported (published as <c>reportDeadline</c>).</summary>
    public static DateTimeOffset For(DateOnly plannedDate) => LocksAt(plannedDate).AddSeconds(-1);

    public static bool IsPassed(DateOnly plannedDate, DateTimeOffset now) => now >= LocksAt(plannedDate);
}
