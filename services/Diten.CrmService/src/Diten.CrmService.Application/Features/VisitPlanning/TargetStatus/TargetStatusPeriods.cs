using System.Globalization;
using Diten.CrmService.Application.Features.CyclePeriod.Read;

namespace Diten.CrmService.Application.Features.VisitPlanning.TargetStatus;

/// <summary>WP-VP-3D — the period of a status read: the plan's own CyclePeriod (the engine's ① read, same calendar-day
/// window), or — without a plan — the tenant-level period in force today (resolved / none, never a guess).</summary>
public static class TargetStatusPeriods
{
    public static ContactStatusPeriod? From(CyclePeriodSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return null;
        }

        var start = DateOnly.FromDateTime(snapshot.StartDate.UtcDateTime);
        var end = DateOnly.FromDateTime(snapshot.EndDate.UtcDateTime);
        return end < start ? null : new ContactStatusPeriod(snapshot.CyclePeriodId, snapshot.CycleCode, start, end);
    }

    public static async Task<ContactStatusPeriod?> ActiveAsync(
        ICyclePeriodReader periods, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var resolution = await periods.ResolveActiveAsync(at, null, null, null, cancellationToken);
        return string.Equals(resolution.Outcome, Domain.Entities.CyclePeriodResolutionOutcomes.Resolved, StringComparison.Ordinal)
            ? From(resolution.Period)
            : null;
    }

    /// <summary>WP-VP-4L (2) — a doctor's status with <c>extraThisWeek</c>: when a plan and a week are given, whether the
    /// rep added an extra visit for the doctor in that week; otherwise the status as read (null = no week asked).</summary>
    public static ContactPeriodStatusDto WithExtraWeek(ContactPeriodStatusDto status, Domain.Entities.PlanningSession? session, string? weekStart)
    {
        if (session is null || string.IsNullOrWhiteSpace(weekStart))
        {
            return status;
        }

        var ws = weekStart.Trim();
        return status with
        {
            ExtraThisWeek = session.WeekExtras.Any(e =>
                string.Equals(e.WeekStart, ws, StringComparison.Ordinal) && (e.ContactId ?? e.TargetId) == status.ContactId)
        };
    }

    /// <summary>WP-VP-4M (1) — the selected week of a status read: absent = none (true, null); a Monday (yyyy-MM-dd) of the
    /// period's weeks (any Monday when no period is known) = that week; anything else = refused (400 invalid_week).</summary>
    public static bool TryParseSelectedWeek(string? weekStart, ContactStatusPeriod? period, out DateOnly? monday)
    {
        monday = null;
        if (string.IsNullOrWhiteSpace(weekStart))
        {
            return true;
        }

        if (period is not null)
        {
            if (!PlanningWeekCalendar.TryParseWeek(weekStart, period.Start, period.End, out var span))
            {
                return false;
            }

            monday = span.Monday;
            return true;
        }

        if (!DateOnly.TryParseExact(weekStart.Trim(), PlanningWeekCalendar.DateFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var day) || day.DayOfWeek != DayOfWeek.Monday)
        {
            return false;
        }

        monday = day;
        return true;
    }

    public static TargetStatusPeriodDto ToDto(ContactStatusPeriod? period)
        => period is null
            ? new TargetStatusPeriodDto(null, null, null, null, null)
            : new TargetStatusPeriodDto(
                period.CyclePeriodId, period.CycleCode,
                period.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                period.End.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), period.WeekCount);
}
