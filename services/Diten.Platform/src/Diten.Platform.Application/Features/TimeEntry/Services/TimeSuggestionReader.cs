using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.TimeEntry.Services;

/// <summary>What the minutes say NOW about a suggestion (D8) — derived on every read, never stored.</summary>
public static class TimeSuggestionMinutesStatus
{
    public const string None = "none";

    /// <summary>The minutes record the person Present.</summary>
    public const string Confirmed = "confirmed";

    /// <summary>Absent/Excused on a suggestion the person has NOT accepted: it is not offered any more.</summary>
    public const string Withdrawn = "withdrawn";

    /// <summary>Absent/Excused on a suggestion the person accepted: the row stays theirs, flagged to them only.</summary>
    public const string Conflict = "conflict";
}

/// <summary>One suggestion as a read derives it.</summary>
public sealed record DerivedTimeSuggestion(
    Guid Id,
    TimeEntryAcceptedMeeting Invitation,
    DateOnly LocalDate,
    int ProposedMinutes,
    TimeSuggestion? Decision,
    string MinutesStatus)
{
    public TimeSuggestionState State => Decision?.State ?? TimeSuggestionState.Open;

    /// <summary>A suggestion nobody accepted and the minutes contradict is not offered.</summary>
    public bool Offered => !(State == TimeSuggestionState.Open && MinutesStatus == TimeSuggestionMinutesStatus.Withdrawn);

    public TimeSuggestionDto ToDto() => new(
        Id, Invitation.MeetingId, Invitation.Title, LocalDate, Decision?.ProposedMinutes ?? ProposedMinutes, State.ToString(),
        MinutesStatus, Decision?.AcceptedEntryId);
}

public interface ITimeSuggestionReader
{
    /// <summary>The person's suggestions for the week in <paramref name="context"/>, offered or not. Reads only.</summary>
    Task<IReadOnlyList<DerivedTimeSuggestion>> ListForWeekAsync(TimesheetWeekContext context, CancellationToken ct = default);
}

/// <summary>
/// MOD-0280-FU01 D8 — meeting suggestions, derived. A suggestion exists for every meeting the person ACCEPTED that is not
/// cancelled, has ENDED, and started on a local day of the week up to today; its id is deterministic per (meeting,
/// person) until the person decides, and the stored decision carries the same id after. Declined or pending invitations
/// produce nothing (the meeting port does not return them).
///
/// <para><b>No row is written by reading</b> — the pack's "created lazily when the week is read" is met by the
/// deterministic id: the suggestion is addressable from the first read, and the row appears only when the person acts.</para>
/// </summary>
public sealed class TimeSuggestionReader : ITimeSuggestionReader
{
    private readonly ITimeEntryMeetingGateway _meetings;
    private readonly ITimeSuggestionRepository _decisions;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _clock;

    public TimeSuggestionReader(
        ITimeEntryMeetingGateway meetings, ITimeSuggestionRepository decisions, ITenantContext tenantContext, TimeProvider clock)
    {
        _meetings = meetings;
        _decisions = decisions;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<IReadOnlyList<DerivedTimeSuggestion>> ListForWeekAsync(TimesheetWeekContext context, CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow();
        var from = TimerRules.LocalMidnightAfter(context.Monday.AddDays(-1), context.Zone);
        var to = TimerRules.LocalMidnightAfter(context.Sunday, context.Zone);

        var meetings = (await _meetings.AcceptedMeetingsAsync(context.UserId, from, to, ct))
            .Where(m => !m.IsCancelled && m.EndAt <= now)
            .Select(m => (Invitation: m, LocalDate: WeekCalendar.LocalDateOf(m.StartAt, context.Zone),
                Minutes: TimerRules.SuggestedMinutes(m.StartAt, m.EndAt)))
            .Where(x => x.LocalDate <= context.LocalToday && x.Minutes > 0)
            .ToList();
        if (meetings.Count == 0)
        {
            return [];
        }

        var decisions = (await _decisions.ListForMeetingsAsync(context.UserId, meetings.Select(x => x.Invitation.MeetingId).ToList(), ct))
            .ToDictionary(d => d.MeetingId);

        return meetings
            .OrderBy(x => x.Invitation.StartAt.UtcTicks)
            .Select(x =>
            {
                decisions.TryGetValue(x.Invitation.MeetingId, out var decision);
                var accepted = decision?.State == TimeSuggestionState.Accepted;
                var status = x.Invitation.Attendance switch
                {
                    TimeEntryMeetingAttendance.Present => TimeSuggestionMinutesStatus.Confirmed,
                    TimeEntryMeetingAttendance.Absent or TimeEntryMeetingAttendance.Excused => accepted
                        ? TimeSuggestionMinutesStatus.Conflict
                        : TimeSuggestionMinutesStatus.Withdrawn,
                    _ => TimeSuggestionMinutesStatus.None
                };
                return new DerivedTimeSuggestion(
                    decision?.Id ?? IdOf(x.Invitation.MeetingId, context.UserId), x.Invitation, x.LocalDate, x.Minutes, decision, status);
            })
            .ToList();
    }

    /// <summary>The suggestion's id before a decision exists — the same for every read, and the decision row's id after.</summary>
    public Guid IdOf(Guid meetingId, Guid userId)
        => TimesheetFinalizer.DeterministicId($"time-entry.suggestion|{_tenantContext.TenantId:N}|{meetingId:N}|{userId:N}");
}
