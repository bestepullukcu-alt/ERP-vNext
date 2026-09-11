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
