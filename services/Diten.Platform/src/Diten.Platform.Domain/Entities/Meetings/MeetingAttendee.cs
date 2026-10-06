using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.Meetings;

namespace Diten.Platform.Domain.Entities.Meetings;

/// <summary>
/// MOD-0357 S2 — one row per invited person (pack §3/§4). Its own collection: an embedded list on
/// <see cref="Meeting"/> would make the unique "one row per (meeting, user)" guarantee a document-level
/// invariant this codebase has no primitive for; a compound unique index gives it for free instead.
/// </summary>
public sealed class MeetingAttendee : TenantScopedEntity
{
    public required Guid MeetingId { get; set; }

    /// <summary>Same tenant-scoped user reference every other module resolves through — no second directory
    /// seam (D2: any ACTIVE tenant user, whatever their company/unit).</summary>
    public required Guid UserId { get; set; }

    public InvitationResponse InvitationResponse { get; set; } = InvitationResponse.Pending;

    /// <summary>Null until minutes are drafted (S6) — never set at invitation time.</summary>
    public AttendanceStatus? AttendanceStatus { get; set; }

    /// <summary>
    /// BL-406 — ADDITIVE ONLY, null on every row written before this WP and on every row whose meeting mail
    /// still has retries left or was delivered. Set once a <c>NotificationDispatch</c> attributed to this
    /// (MeetingId, UserId) pair (via <c>NotificationDispatch.CausationId</c> + <c>MeetingAttendeeUserId</c>)
    /// permanently fails — see <c>MarkNotificationDispatchFailedHandler</c>. Drives the "mail undelivered" badge
    /// on the meeting detail screen; no other writer of this field exists.
    /// </summary>
    public DateTimeOffset? MailUndeliveredAt { get; set; }
}
