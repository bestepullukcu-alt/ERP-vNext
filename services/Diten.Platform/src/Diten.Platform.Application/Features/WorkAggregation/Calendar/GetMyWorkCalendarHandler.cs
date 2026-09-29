using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkingHours;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.WorkAggregation.Calendar;

/// <summary>
/// WP-TASK-CALENDAR-ENGINE-01 (E) — see <see cref="GetMyWorkCalendarQuery"/>.
///
/// <para><b>Scope.</b> Tasks come from <see cref="ITaskItemRepository.ListByAssigneeAsync"/> for the CALLER (the
/// repository adds the tenant); meetings from <see cref="ICalendarMeetingReader"/> for the caller. There is no
/// parameter naming another person, so there is nothing to widen.</para>
/// </summary>
public sealed class GetMyWorkCalendarHandler : IRequestHandler<GetMyWorkCalendarQuery, Response<WorkCalendarDto>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskLifecycleService _lifecycle;
    private readonly IWorkingHoursProvider _workingHours;
    private readonly ICalendarMeetingReader _meetings;
    private readonly IMeetingAttendeeRepository _attendees;
    private readonly IMeetingRepository _meetingRecords;
    private readonly ICurrentUserContext _currentUser;

    public GetMyWorkCalendarHandler(
        ITaskItemRepository tasks,
        ITaskLifecycleService lifecycle,
        IWorkingHoursProvider workingHours,
        ICalendarMeetingReader meetings,
        IMeetingAttendeeRepository attendees,
        IMeetingRepository meetingRecords,
        ICurrentUserContext currentUser)
    {
        _tasks = tasks;
        _lifecycle = lifecycle;
        _workingHours = workingHours;
        _meetings = meetings;
        _attendees = attendees;
        _meetingRecords = meetingRecords;
        _currentUser = currentUser;
    }

    public async Task<Response<WorkCalendarDto>> Handle(GetMyWorkCalendarQuery query, CancellationToken ct)
    {
        if (query.From is not { } from || query.To is not { } to
            || to < from || to.DayNumber - from.DayNumber + 1 > WorkCalendarLimits.MaxDays
            // CT acceptance: the edges of the calendar would overflow the day arithmetic below (to + 1 day) into a 500.
            || from < DateOnly.MinValue.AddDays(2) || to > DateOnly.MaxValue.AddDays(-2))
        {
            return Response<WorkCalendarDto>.Fail(
                $"'from' and 'to' are required (YYYY-MM-DD), 'to' may not precede 'from', and the range may span at most {WorkCalendarLimits.MaxDays} days.",
                400, WorkCalendarReasonCodes.RangeInvalid, query.CorrelationId);
        }

        var me = _currentUser.UserId;
        var hours = await _workingHours.GetWorkingWindowsAsync(me, from, to, ct);
        var zone = hours.TimeZone;
        var today = hours.LocalDateOf(DateTimeOffset.UtcNow);

        // ── The caller's own tasks ───────────────────────────────────────────────────────────────────────────
        var held = (await _tasks.ListByAssigneeAsync(me, ct))
            .Where(t => !_lifecycle.IsTerminal(t))
            .ToList();

        var blocks = held
            .Where(t => t.PlannedStartAt is not null && t.PlannedDurationMinutes is not null)
            .Select(t => (t.Id, Start: t.PlannedStartAt!.Value, End: EndOf(t)!.Value))
            .ToList();

        var tasks = held
            .Where(t => t.PlannedDate is not null)
            .Select(t => (Task: t, Day: PlanDay(t)))
            .Where(x => x.Day >= from && x.Day <= to)
            .OrderBy(x => x.Day).ThenBy(x => x.Task.PlannedStartAt ?? DateTimeOffset.MaxValue)
            .Select(x => new WorkCalendarTaskDto(
                x.Task.Id.ToString(),
                x.Task.Title,
                x.Task.Priority.ToString(),
                x.Task.Lifecycle.ToString(),
                x.Day,
                x.Task.PlannedStartAt,
                x.Task.PlannedDurationMinutes,
                EndOf(x.Task),
                TaskPlanBlockRules.RemainingMinutes(x.Task.EstimateHours, x.Task.PlannedDurationMinutes),
                x.Task.DueAt,
                Conflict: x.Task.PlannedStartAt is { } start
                          && blocks.Any(b => b.Id != x.Task.Id
                                             && TaskPlanBlockRules.Overlaps(start, EndOf(x.Task)!.Value, b.Start, b.End))))
            .ToList();

        var notStarted = held.Where(t => t.Lifecycle is TaskLifecycle.Open or TaskLifecycle.Planned).ToList();
        var unplannedCount = notStarted.Count(t => t.PlannedDate is null);
        var planPassedCount = notStarted.Count(t => t.PlannedDate is not null && PlanDay(t) < today);

        // ── The caller's own meetings ────────────────────────────────────────────────────────────────────────
        var meetings = (await _meetings.ListMineAsync(
                me, LocalMidnightUtc(from, zone), LocalMidnightUtc(to.AddDays(1), zone), ct))
            .Select(m => new WorkCalendarMeetingDto(
                m.MeetingId.ToString(), m.Title, m.StartAt.ToUniversalTime(), m.EndAt.ToUniversalTime(),
                m.Response == InvitationResponse.Accepted ? "accepted" : "pending"))
            .ToList();

        var pendingInviteCount = await CountPendingInvitesAsync(me, ct);

        var days = hours.Days
            .Select(d => new WorkCalendarDayDto(
                d.Date, d.DayKind, d.HolidayName,
                d.Windows.Select(w => new WorkCalendarWindowDto(w.StartAt, w.EndAt)).ToList(),
                d.ResolvedFrom, d.CalendarUnresolved))
            .ToList();

        return Response<WorkCalendarDto>.Success(
            new WorkCalendarDto(
                hours.TimeZoneId, from, to, tasks, meetings, days,
                unplannedCount, planPassedCount, pendingInviteCount),
            200,
            query.CorrelationId);
    }

    /// <summary>
    /// Invitations still waiting for the caller's answer — the SAME rule the Task Center's invitation rows use
    /// (<c>MeetingWorkItemProvider</c>): pending, the meeting still scheduled, and not already past.
    /// </summary>
    private async Task<int> CountPendingInvitesAsync(Guid me, CancellationToken ct)
    {
        var pending = await _attendees.ListPendingByUserIdAsync(me, ct);
        if (pending.Count == 0)
        {
            return 0;
        }

        var today = DateTimeOffset.UtcNow.Date;
        var meetings = (await _meetingRecords.ListByIdsAsync(pending.Select(a => a.MeetingId).Distinct().ToList(), ct))
            .ToDictionary(m => m.Id);
        return pending.Count(a => meetings.TryGetValue(a.MeetingId, out var m)
                                  && m.Lifecycle == MeetingLifecycle.Scheduled
                                  && m.StartAt.UtcDateTime.Date >= today);
    }

    /// <summary>
    /// The day a plan is FOR, read as the stored date's own calendar day: a block's plan date is written as local
    /// midnight carrying the local offset, and a day plan is the date the reader chose.
    /// </summary>
    private static DateOnly PlanDay(TaskItem task) => DateOnly.FromDateTime(task.PlannedDate!.Value.DateTime);

    private static DateTimeOffset? EndOf(TaskItem task)
        => task.PlannedStartAt is { } start && task.PlannedDurationMinutes is { } minutes
            ? start.AddMinutes(minutes)
            : null;

    private static DateTimeOffset LocalMidnightUtc(DateOnly day, TimeZoneInfo zone)
    {
        var midnight = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(midnight, zone.GetUtcOffset(midnight)).ToUniversalTime();
    }
}
