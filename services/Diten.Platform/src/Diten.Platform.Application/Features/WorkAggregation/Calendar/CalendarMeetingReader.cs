using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.WorkAggregation.Calendar;

/// <summary>One meeting on the caller's calendar, with the caller's own answer to it.</summary>
public sealed record CalendarMeeting(
    Guid MeetingId,
    string Title,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    InvitationResponse Response,
    MeetingLifecycle Lifecycle);

/// <summary>
/// WP-TASK-CALENDAR-ENGINE-01 — "my meetings in this window", READ-ONLY over MOD-0357's data.
///
/// <para>One reader for both consumers — the plan-overlap warning and the calendar feed — so they cannot disagree
/// about which meetings are on somebody's calendar: the ones they ACCEPTED or have NOT ANSWERED yet, never a
/// declined one, never a cancelled meeting. Scoped to the tenant by the repositories and to the user by the
/// attendee query; nothing here widens either.</para>
/// </summary>
public interface ICalendarMeetingReader
{
    /// <summary>Meetings of <paramref name="userId"/> overlapping [<paramref name="fromUtc"/>, <paramref name="toUtc"/>).</summary>
    Task<IReadOnlyList<CalendarMeeting>> ListMineAsync(
        Guid userId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default);
}

public sealed class CalendarMeetingReader : ICalendarMeetingReader
{
    private readonly IMeetingAttendeeRepository _attendees;
    private readonly IMeetingRepository _meetings;

    public CalendarMeetingReader(IMeetingAttendeeRepository attendees, IMeetingRepository meetings)
    {
        _attendees = attendees;
        _meetings = meetings;
    }

    public async Task<IReadOnlyList<CalendarMeeting>> ListMineAsync(
        Guid userId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default)
    {
        // Declined rows are excluded AT THE QUERY (ListNotDeclinedByUserIdAsync) — one place, so a test on the feed
        // proves the rule rather than a second copy of it.
        var mine = (await _attendees.ListNotDeclinedByUserIdAsync(userId, ct))
            .GroupBy(a => a.MeetingId)
            .ToDictionary(g => g.Key, g => g.First().InvitationResponse);
        if (mine.Count == 0)
        {
            return [];
        }

        // The window is applied in memory: StartAt/EndAt are BSON [ticks, offset] arrays (BL-030), so a server-side
        // range query on them is not reliable.
        return (await _meetings.ListByIdsAsync(mine.Keys.ToList(), ct))
            .Where(m => m.Lifecycle != MeetingLifecycle.Cancelled
                        && m.StartAt < toUtc
                        && m.EndAt > fromUtc)
            .OrderBy(m => m.StartAt)
            .Select(m => new CalendarMeeting(m.Id, m.Title, m.StartAt, m.EndAt, mine[m.Id], m.Lifecycle))
            .ToList();
    }
}
