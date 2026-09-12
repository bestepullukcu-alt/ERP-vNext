using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.Meetings;

namespace Diten.Platform.Domain.Entities.Meetings;

/// <summary>
/// MOD-0357 S11 (pack §19) — a recurring cadence rule (weekly quality review, monthly management review). The
/// rule itself never IS a meeting: a background sweep reads it and produces the NEXT single instance ahead of
/// time (<see cref="LeadTimeDays"/>), the same shape MOD-0024's own <c>TaskRecurrenceRule</c> takes for tasks —
/// see <c>TaskRecurrenceRule</c>'s own doc comment ("the background sweep has no 'itself'").
/// </summary>
public sealed class MeetingSeries : TenantScopedEntity
{
    /// <summary>Trim, max 200, tenant-unique (unique index + pre-check — same convention as <see cref="MeetingType.Name"/>).</summary>
    public required string Name { get; set; }

    public required Guid MeetingTypeId { get; set; }

    public MeetingSeriesFrequency Frequency { get; set; } = MeetingSeriesFrequency.Weekly;

    /// <summary>Every Nth period, N &gt;= 1. "Every 2 weeks" is <see cref="Frequency"/> Weekly + Interval 2.</summary>
    public int Interval { get; set; } = 1;

    /// <summary>The first instance's own start, and the anchor every later occurrence is computed FROM — never
    /// by stepping from the previous one, for the same month-end drift reason
    /// <c>TaskRecurrenceSchedule</c>'s own doc comment states (31 Jan → 28 Feb → 31 Mar, not → 28 Mar).</summary>
    public required DateTimeOffset StartsAt { get; set; }

    /// <summary>Null means the series has no end. An occurrence at or after this instant is never generated.</summary>
    public DateTimeOffset? EndsAt { get; set; }

    public int DurationMinutes { get; set; } = 60;

    public string? Location { get; set; }

    /// <summary>
    /// NOT nullable — KS3. The sweep that generates each instance runs with no user context
    /// (<c>ICurrentUserContext</c> answers <c>Guid.Empty</c>, no HTTP request behind it); the organizer of every
    /// generated meeting comes from THIS field, never from whoever happens to be "current" when a sweep runs on
    /// nobody's behalf. The exact failure <c>TaskRecurrenceRule.AssignmentTarget</c>'s own doc comment records
    /// for tasks (a background sweep has no "self") — required here so a series can never repeat it.
    /// </summary>
    public required Guid OrganizerUserId { get; set; }

    public List<Guid> AttendeeUserIds { get; set; } = [];

    /// <summary>How many days before an occurrence's own start the sweep may generate it, 1..90, default 14 —
    /// deliberately SHORT and adjustable (K6): a longer window means the carry-forward snapshot (still-open
    /// actions from the PREVIOUS instance) is taken further from the new meeting's own date, and is more likely
    /// to be stale by the time the meeting actually happens.</summary>
    public int LeadTimeDays { get; set; } = 14;

    /// <summary>
    /// True (default): each generated instance after the first is scheduled via
    /// <c>ScheduleFollowUpMeetingCommand</c>, chained to the previous instance exactly like a manually-scheduled
    /// continuation (K6) — same carry-forward, same cross-link. False: every instance (including the first) is
    /// an independent <c>CreateMeetingCommand</c> call with no <c>FollowUpOfMeetingId</c> at all. The FIRST
    /// instance is always a plain create either way — there is nothing to chain it to yet.
    /// </summary>
    public bool ChainAsFollowUp { get; set; } = true;

    /// <summary>The most recently generated instance, for display only — never read to compute the next
    /// occurrence (see <c>MeetingSeriesSchedule</c>, which always recomputes from <see cref="StartsAt"/>).</summary>
    public Guid? LastGeneratedMeetingId { get; set; }

    /// <summary>The START of the most recently generated occurrence (not the wall-clock time the sweep ran) —
    /// <c>MeetingSeriesSchedule.NextDueOccurrence</c> reads this to find the occurrence that comes AFTER it.</summary>
    public DateTimeOffset? LastGeneratedAt { get; set; }

    /// <summary>
    /// KS2 — the claim. Stamped with the due occurrence's own deterministic id under an expected-version write
    /// BEFORE the meeting is created, the identical mechanism <c>TaskRecurrenceRule.LastProcessInstanceId</c>
    /// uses: two overlapping sweeps computing the same occurrence both attempt this write, exactly one wins.
    /// </summary>
    public string? LastProcessInstanceId { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DeletedAt { get; set; }
}
