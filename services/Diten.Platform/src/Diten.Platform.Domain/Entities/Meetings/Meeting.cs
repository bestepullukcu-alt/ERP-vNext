using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.Meetings;

namespace Diten.Platform.Domain.Entities.Meetings;

/// <summary>
/// MOD-0357 S2 — the meeting aggregate root (pack §3/§4). Attendees and agenda items are their OWN collections
/// (<see cref="MeetingAttendee"/>, <see cref="AgendaItem"/>), not embedded arrays — the same reasoning
/// <c>TaskComment</c> documents for its own collection: a full-replace update of this document must never be
/// able to lose either list by omission.
/// </summary>
public sealed class Meeting : TenantScopedEntity
{
    public required string Title { get; set; }

    /// <summary>FK → <see cref="MeetingType"/>, tenant-scoped, must resolve and be active.</summary>
    public required Guid MeetingTypeId { get; set; }

    public required DateTimeOffset StartAt { get; set; }

    /// <summary>Must be strictly after <see cref="StartAt"/> — enforced in the handler, not by validator, so the
    /// failure carries the <c>MEETING_END_BEFORE_START</c> reason code (FluentValidation failures in this
    /// codebase always surface as a plain 400 with no code otherwise).</summary>
    public required DateTimeOffset EndAt { get; set; }

    /// <summary>Free text — a physical place or a link; no format enforced (pack §2, Calendar's job).</summary>
    public string? Location { get; set; }

    /// <summary>This record's "requester" (pack §4) — defaults to the creating user; reassignable while
    /// <see cref="Lifecycle"/> is <see cref="MeetingLifecycle.Scheduled"/> via <c>ReassignMeetingOrganizerCommand</c>
    /// (D4). The historical organizer is never overwritten silently.</summary>
    public required Guid OrganizerUserId { get; set; }

    public string? Description { get; set; }

    /// <summary>FK → another <see cref="Meeting"/> in the same tenant; null for a first-of-its-kind meeting
    /// (K6). Carry-forward of open agenda items from the prior meeting is S7 — not built here.</summary>
    public Guid? FollowUpOfMeetingId { get; set; }

    public MeetingLifecycle Lifecycle { get; set; } = MeetingLifecycle.Scheduled;

    /// <summary>Server-computed on create via <see cref="Diten.Platform.Application.Features.Meetings.Services.IMeetingIdempotencyKeyResolver"/>
    /// (K11) — never accepted from the client. Unique per tenant (see the manifest's own index).</summary>
    public required string IdempotencyKey { get; set; }

    /// <summary>Reason text captured by <c>CancelMeetingCommand</c> — required whenever <see cref="Lifecycle"/>
    /// is <see cref="MeetingLifecycle.Cancelled"/>.</summary>
    public string? CancellationReason { get; set; }

    /// <summary>Set by <c>ReassignMeetingOrganizerCommand</c> — the organizer BEFORE the most recent reassignment,
    /// kept purely as an audit trail (D4: "the historical organizer stays in the record").</summary>
    public Guid? PreviousOrganizerUserId { get; set; }
}
