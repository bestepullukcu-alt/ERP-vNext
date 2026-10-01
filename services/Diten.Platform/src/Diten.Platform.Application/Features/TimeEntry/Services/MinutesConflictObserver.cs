using Diten.Platform.Application.Contracts;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.TimeEntry.Services;

/// <summary>
/// MOD-0280-FU01 T3 (pack §21.3 N5, D8) — time entry's side of the minutes seam. For each attendee the published minutes
/// record as <c>Absent</c> or <c>Excused</c> who ACCEPTED this meeting's suggestion into their week (any week state), the
/// person — and only the person, never the approver or a manager (D11) — is told once per (meeting, person, attendance).
/// <c>Present</c> sends nothing; an open or dismissed suggestion sends nothing (the read-time flag already hides it).
///
/// <para>Reads only this module's own records (the suggestion decision and its row); the meeting facts come in on the
/// observation. The read-time flag on the week stays the source of truth — this is the e-mail, nothing else.</para>
///
/// <para><b>Never throws into the minutes write</b> (the contract says so): every failure is logged.</para>
/// </summary>
public sealed class MinutesConflictObserver : IMeetingAttendanceObserver
{
    private readonly ITimeSuggestionRepository _suggestions;
    private readonly ITimeEntryRepository _entries;
    private readonly ITimeEntryNotifier _notifier;
    private readonly ILogger<MinutesConflictObserver> _logger;

    public MinutesConflictObserver(
        ITimeSuggestionRepository suggestions,
        ITimeEntryRepository entries,
        ITimeEntryNotifier notifier,
        ILogger<MinutesConflictObserver> logger)
    {
        _suggestions = suggestions;
        _entries = entries;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task OnMinutesAttendanceRecordedAsync(MeetingAttendanceObservation observation, CancellationToken ct = default)
    {
        foreach (var record in observation.Attendance.Where(a => a.Status is AttendanceStatus.Absent or AttendanceStatus.Excused))
        {
            try
            {
                var accepted = (await _suggestions.ListForMeetingsAsync(record.AttendeeUserId, [observation.MeetingId], ct))
                    .FirstOrDefault(s => s.State == TimeSuggestionState.Accepted);
                if (accepted is null)
                {
                    continue;
                }

                // The row the person accepted must still be there: one they removed from their draft is no conflict.
                if (accepted.AcceptedEntryId is { } entryId && await _entries.GetByIdAsync(entryId, ct) is null)
                {
                    continue;
                }

                await _notifier.MinutesConflictAsync(
                    record.AttendeeUserId, observation.MeetingId, observation.MeetingTitle, accepted.LocalDate, record.Status, ct);
            }
            catch (Exception ex)
            {
                // One person's failure never costs the others their notice, and never reaches the minutes.
                _logger.LogWarning(ex, "time-entry.minutes-conflict failed MinutesVersion={Version} UserId={UserId}.",
                    observation.MinutesVersionNumber, record.AttendeeUserId);
            }
        }
    }
}
