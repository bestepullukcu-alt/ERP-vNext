using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Contracts;

/// <summary>One attendee as a published minutes version records them.</summary>
public sealed record MeetingAttendanceRecordObservation(Guid AttendeeUserId, AttendanceStatus Status);

/// <summary>
/// A minutes version MOD-0357 just published (or published as a correction) and synced onto its attendees: which meeting,
/// its title and start (so the observer needs no read of the meeting), and the attendance the version records.
/// </summary>
public sealed record MeetingAttendanceObservation(
    Guid TenantId,
    Guid MeetingId,
    string MeetingTitle,
    DateTimeOffset MeetingStartAt,
    int MinutesVersionNumber,
    bool IsCorrection,
    IReadOnlyList<MeetingAttendanceRecordObservation> Attendance);

/// <summary>
/// MOD-0280-FU01 T3 (pack §21.3 N5, ADR-004) — how MOD-0357 tells time entry that published minutes recorded who
/// attended. Called by <c>PublishMinutesHandler</c> and <c>CorrectPublishedMinutesHandler</c>, after <c>SyncAsync</c>,
/// in the same process and scope — the <see cref="ITaskTransitionObserver"/> pattern.
///
/// <para><b>Never throws into the caller.</b> The minutes are already published; a notification that could not be sent
/// must never turn that into an error. The read-time flag on the person's week stays the source of truth, so a missed
/// notification loses nothing but the e-mail.</para>
///
/// <para>On extraction (ADR-004) this becomes an outbox event; nothing on the MOD-0357 side changes but the binding.</para>
/// </summary>
public interface IMeetingAttendanceObserver
{
    Task OnMinutesAttendanceRecordedAsync(MeetingAttendanceObservation observation, CancellationToken ct = default);
}

/// <summary>The one line a minutes handler adds: call the observer if there is one, and let nothing it does escape.</summary>
public static class MeetingAttendanceObserverCall
{
    public static async Task NotifySafelyAsync(
        this IMeetingAttendanceObserver? observer, Meeting meeting, MeetingMinutesVersion version, ILogger? logger)
    {
        if (observer is null)
        {
            return;
        }

        try
        {
            // L8 — built INSIDE the try: a malformed version (a null attendance list) is the observer's failure, never
            // the minutes'.
            var observation = new MeetingAttendanceObservation(
                meeting.TenantId,
                meeting.Id,
                meeting.Title,
                meeting.StartAt,
                version.VersionNumber,
                version.CorrectionOfVersionNumber is not null,
                version.Attendance.Select(a => new MeetingAttendanceRecordObservation(a.AttendeeUserId, a.Status)).ToList());

            // Not the request's token: the minutes have committed, and a client that went away must not cancel the rest.
            await observer.OnMinutesAttendanceRecordedAsync(observation, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex,
                "meeting.attendance.observer_failed MeetingId={MeetingId} MinutesVersion={Version}; the minutes stand.",
                meeting?.Id, version?.VersionNumber);
        }
    }
}
