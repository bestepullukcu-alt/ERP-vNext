using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.WorkAggregation.Calendar;

// WP-TASK-CALENDAR-ENGINE-01 (E) — the Task Center's CALENDAR FEED: GET /api/v1/work/calendar?from=&to=.
//
// ONE read for the calendar screen (2b): the caller's own planned work, the caller's own meetings, the working
// windows and day types for the range, the tenant zone, and the three counts the left panel shows. The LISTS behind
// those counts stay on the existing Task Center feed (GET /api/v1/work-items/mine) — this is not a second list API.
//
// Instants are UTC; the screen renders them in the tenant zone it is told. Nothing outside the caller's own work and
// own invitations is ever returned.

/// <summary>The caller's calendar for a local-date range (at most <see cref="WorkCalendarLimits.MaxDays"/> days).</summary>
public sealed record GetMyWorkCalendarQuery(DateOnly? From, DateOnly? To, string CorrelationId)
    : IRequest<Response<WorkCalendarDto>>;

public static class WorkCalendarLimits
{
    /// <summary>Six weeks: the largest month grid a calendar draws.</summary>
    public const int MaxDays = 42;
}

public static class WorkCalendarReasonCodes
{
    /// <summary>Missing <c>from</c>/<c>to</c>, <c>to</c> before <c>from</c>, or longer than 42 days.</summary>
    public const string RangeInvalid = "WORK_CALENDAR_RANGE_INVALID";
}

public sealed record WorkCalendarDto(
    string TimeZoneId,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<WorkCalendarTaskDto> Tasks,
    IReadOnlyList<WorkCalendarMeetingDto> Meetings,
    IReadOnlyList<WorkCalendarDayDto> Days,
    int UnplannedCount,
    int PlanPassedCount,
    int PendingInviteCount);

/// <summary>
/// One planned task the caller holds. A DAY plan has only <see cref="PlannedDate"/>; a BLOCK also has the start, the
/// length and the end. <see cref="Conflict"/> is true when the block overlaps another of the caller's blocks (the plan
/// rule refuses that, so it marks data written around the rule, e.g. before it existed).
/// </summary>
public sealed record WorkCalendarTaskDto(
    string TaskId,
    string Title,
    string Priority,
    string Lifecycle,
    DateOnly PlannedDate,
    DateTimeOffset? PlannedStartAt,
    int? PlannedDurationMinutes,
    DateTimeOffset? PlannedEndAt,
    int? RemainingMinutes,
    DateTimeOffset? DueAt,
    bool Conflict);

/// <summary>A meeting the caller accepted (<c>accepted</c>) or has not answered (<c>pending</c>). Declined never appear.</summary>
public sealed record WorkCalendarMeetingDto(
    string MeetingId,
    string Title,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string Response);

public sealed record WorkCalendarDayDto(
    DateOnly Date,
    string DayKind,
    string? HolidayName,
    IReadOnlyList<WorkCalendarWindowDto> Windows,
    string ResolvedFrom,
    bool CalendarUnresolved);

public sealed record WorkCalendarWindowDto(DateTimeOffset StartAt, DateTimeOffset EndAt);
