using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.Meetings;

namespace Diten.Platform.Domain.Entities.Meetings;

/// <summary>
/// MOD-0357 S6 (pack §3/§4, K4) — one row per version of a meeting's minutes, APPEND-ONLY. A draft is edited in
/// place (same row, same <see cref="VersionNumber"/>) until it publishes; publishing is the row's own one-way
/// door (<see cref="BaseEntity.Version"/>-conditional, same optimistic-concurrency discipline every other
/// MOD-0024-adjacent write uses). A correction after publish never updates this row — it inserts a NEW one with
/// <see cref="CorrectionOfVersionNumber"/> set; the prior published row is read, never written, again.
///
/// <para>Its own collection, not embedded on <see cref="Meeting"/>: the append-only history is the entire point
/// (K4), and a document that gets REPLACED on every draft edit cannot also promise its own prior versions are
/// immutable — the two requirements would live on the same document.</para>
/// </summary>
public sealed class MeetingMinutesVersion : TenantScopedEntity
{
    public required Guid MeetingId { get; set; }

    /// <summary>1, 2, 3… per meeting — never reused, never renumbered. The tenant-unique key together with
    /// <see cref="MeetingId"/> (see the schema manifest) is what makes "two v1 rows for one meeting" impossible
    /// at the storage level, not merely in application logic.</summary>
    public required int VersionNumber { get; set; }

    public MinutesStatus Status { get; set; } = MinutesStatus.Draft;

    public List<MinutesAttendanceRecord> Attendance { get; set; } = [];

    public List<MinutesDecision> Decisions { get; set; } = [];

    /// <summary>Every <c>RecordLink.Id</c> a decision or agenda item on THIS version produced — read-only
    /// convenience over the per-decision <see cref="MinutesDecision.RecordLinkId"/> fields (pack §3). Never
    /// written to after this row is Published (see <see cref="MinutesDecision.RecordLinkId"/>'s own note on
    /// why a post-publish task never touches the frozen row).</summary>
    public List<Guid> ActionReferences { get; set; } = [];

    public DateTimeOffset? PublishedAtUtc { get; set; }

    public Guid? PublishedByUserId { get; set; }

    /// <summary>Set only on a correction row — the version number of the published row this one corrects.
    /// Null on every v1 and on every ordinary (non-correction) version.</summary>
    public int? CorrectionOfVersionNumber { get; set; }

    /// <summary>Mandatory on a correction row (K4); null otherwise. Validated by the command handler, not by a
    /// data-annotation, so the 400 carries <c>MEETING_MINUTES_CORRECTION_REASON_REQUIRED</c> rather than a
    /// generic ModelState error.</summary>
    public string? CorrectionReason { get; set; }
}

/// <summary>One attendee's actual presence, decided at minutes time — distinct from
/// <see cref="MeetingAttendee.InvitationResponse"/>, which is decided at invitation time and by the attendee
/// themselves. Embedded (not its own collection): this list exists only as part of a specific minutes version
/// and is never queried on its own.</summary>
public sealed class MinutesAttendanceRecord
{
    public required Guid AttendeeUserId { get; set; }

    public required AttendanceStatus Status { get; set; }
}

/// <summary>
/// A single decision, as a plain row — explicitly NOT a task and explicitly not yet a MOD-0007 aggregate (pack
/// §3 "Decisions are not MOD-0007"). <see cref="Code"/> is server-minted ("D-1", "D-2"…) from the row's own
/// position in <see cref="MeetingMinutesVersion.Decisions"/> at save time — never accepted from the client, so
/// two callers racing a reorder cannot mint the same code twice.
/// </summary>
public sealed class MinutesDecision
{
    public required string Code { get; set; }

    public required string Text { get; set; }

    public Guid? DecidedByUserId { get; set; }

    /// <summary>K4 — "a decision row never gains a TaskId-shaped field": this points at the <c>RecordLink</c>
    /// the decision produced, if any, never at the task itself. Written ONLY while the owning version is still
    /// Draft; a decision inside an already-Published version stays exactly as published even when a task is
    /// later created "from" it — that task's <c>RecordLink.CreatedAfterMinutesPublished</c> flag is how the UI
    /// finds it instead (see <c>CreateTaskFromMeetingHandler</c>).</summary>
    public Guid? RecordLinkId { get; set; }
}
