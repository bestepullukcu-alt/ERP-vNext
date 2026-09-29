using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.TimeEntry.Adapters;

/// <summary>
/// v1 <see cref="ITimeEntryMeetingGateway"/> over MOD-0357's own read repositories (pack §2.2, D8). Nothing in the
/// meetings feature is edited. The attendance read is the attendee row's <c>AttendanceStatus</c> — which the minutes
/// publish writes one-way from the published version and a minutes correction re-writes
/// (<c>PublishMinutesHandler.SyncAsync</c>), so reading it here always gives the minutes' CURRENT word.
/// </summary>
public sealed class MeetingGatewayAdapter : ITimeEntryMeetingGateway
{
    private readonly IMeetingRepository _meetings;
    private readonly IMeetingAttendeeRepository _attendees;

    public MeetingGatewayAdapter(IMeetingRepository meetings, IMeetingAttendeeRepository attendees)
    {
        _meetings = meetings;
        _attendees = attendees;
    }

    public async Task<IReadOnlyList<TimeEntryAcceptedMeeting>> AcceptedMeetingsAsync(
        Guid userId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default)
    {
        // Accepted only: a pending invitation is not a plan to attend and a declined one is a refusal (D8).
        var accepted = (await _attendees.ListNotDeclinedByUserIdAsync(userId, ct))
            .Where(a => a.InvitationResponse == InvitationResponse.Accepted)
            .GroupBy(a => a.MeetingId)
            .ToDictionary(g => g.Key, g => g.First());
        if (accepted.Count == 0)
        {
            return [];
        }

        // The date window in memory: Meeting.StartAt is stored as a [ticks, offset] array (BL-030), the same reason
        // the calendar feed filters here.
        return (await _meetings.ListByIdsAsync(accepted.Keys.ToList(), ct))
            .Where(m => m.StartAt >= fromUtc && m.StartAt < toUtc)
            .Select(m => new TimeEntryAcceptedMeeting(
                m.Id,
                m.Title,
                m.StartAt,
                m.EndAt,
                m.Lifecycle == MeetingLifecycle.Cancelled,
                accepted[m.Id].AttendanceStatus switch
                {
                    AttendanceStatus.Present => TimeEntryMeetingAttendance.Present,
                    AttendanceStatus.Absent => TimeEntryMeetingAttendance.Absent,
                    AttendanceStatus.Excused => TimeEntryMeetingAttendance.Excused,
                    _ => TimeEntryMeetingAttendance.NotRecorded
                }))
            .ToList();
    }
}
