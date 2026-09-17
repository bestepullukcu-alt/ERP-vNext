namespace Diten.Platform.Domain.Enums.Meetings;

/// <summary>MOD-0357 S2 — a meeting's own lifecycle (pack §4). <c>Completed</c> is set only when its minutes
/// publish (S6); this slice never sets it — a meeting created here stays <see cref="Scheduled"/> until
/// <c>CancelMeeting</c> moves it to <see cref="Cancelled"/>.</summary>
public enum MeetingLifecycle
{
    Scheduled = 0,
    Cancelled = 1,
    Completed = 2
}

/// <summary>An attendee's response to the invitation (pack §4 K5). No third "maybe" state in this slice.</summary>
public enum InvitationResponse
{
    Pending = 0,
    Accepted = 1,
    Declined = 2
}

/// <summary>Whether an attendee actually attended — written only at minutes time (S6); null until then.</summary>
public enum AttendanceStatus
{
    Present = 0,
    Absent = 1,
    Excused = 2
}

/// <summary>A minutes version's own state (pack §4 K4). <see cref="Published"/> is a one-way door: the only
/// path forward from it is a NEW version row (a correction), never a change to this one.</summary>
public enum MinutesStatus
{
    Draft = 0,
    Published = 1
}

/// <summary>
/// MOD-0357 S11 — how often a <c>MeetingSeries</c> repeats. A DELIBERATE, separate enum from MOD-0024's own
/// <c>TaskRecurrenceFrequency</c>, not a reuse: the pack allows Meetings exactly one contact point with
/// <c>Features/Tasks</c> (the <c>RecordLink</c> bridge), and reaching into Tasks' own recurrence enum would be a
/// second, silent one. No <c>Daily</c> value — a daily "management review" is not a cadence this module's own
/// vocabulary (weekly quality review, monthly/quarterly/yearly management review) ever names.
/// </summary>
public enum MeetingSeriesFrequency
{
    Weekly = 0,
    Monthly = 1,
    Quarterly = 2,
    Yearly = 3
}
