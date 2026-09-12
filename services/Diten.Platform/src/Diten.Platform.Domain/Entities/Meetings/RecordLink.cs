using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.Meetings;

/// <summary>
/// MOD-0357 pack §3 — the ONE bridge between a meeting and anything else (a MOD-0024 task today; MOD-0007's
/// decisions later), in ONE collection. Many-to-many, bidirectional by query: a row is read by
/// <see cref="SourceRecordId"/> from one side and <see cref="TargetRecordId"/> from the other, never as a
/// single-direction embedded list on either aggregate.
///
/// <para><b>Owner and writer.</b> MOD-0357 is the first owner and — in this slice — the only writer
/// (`IRecordLinkService`); MOD-0024 reads it (batched, for `relatedRecords`) and never writes a row. If a
/// second module needs to write links later, this collection is promoted to a shared contract, not duplicated
/// (ADR-003 §"Sonuçlar").</para>
///
/// <para><b>Never an implicit blocker (K1).</b> No action, gate or state anywhere reads a `RecordLink` to decide
/// whether something else may proceed — the one documented exception is the task TYPE's own
/// `reviewMeetingPolicy.required` flag, which is MOD-0024's rule reading this link, not a rule this entity
/// enforces itself.</para>
/// </summary>
public sealed class RecordLink : TenantScopedEntity
{
    /// <summary>
    /// The module owning the "from" record — an open string, not an enum, so a future module (MOD-0007) joins
    /// without a schema change. `"tasks"` and `"meetings"` are the two values this slice actually writes/reads;
    /// see <see cref="RecordLinkModuleCodes"/>.
    /// </summary>
    public required string SourceModuleCode { get; set; }

    /// <summary>The record id on the "from" side — a `Meeting.Id` or a `TaskItem.Id`, depending on
    /// <see cref="SourceModuleCode"/>. Never a foreign key at the C# type level: the far side is resolved by
    /// module code, not by a typed reference, which is what lets a third module join with no migration.</summary>
    public required Guid SourceRecordId { get; set; }

    /// <summary>The module owning the "to" record. See <see cref="SourceModuleCode"/>.</summary>
    public required string TargetModuleCode { get; set; }

    /// <summary>The record id on the "to" side.</summary>
    public required Guid TargetRecordId { get; set; }

    /// <summary>
    /// What kind of relationship this is — `"preparation" | "agenda" | "bornFromMeeting" | "reviewMeeting"`
    /// (pack §3). A string, not an enum, for the same reason the module codes are: a link type is data a
    /// module declares about its own relationship, not a closed set this collection's owner controls alone.
    /// See <see cref="RecordLinkTypes"/> for the values this slice actually writes.
    /// </summary>
    public required string LinkType { get; set; }

    /// <summary>
    /// Who created this link — the real user id, distinct from the inherited <c>CreatedBy</c> (a display NAME
    /// string, per <c>BaseEntity</c>'s own convention). Kept as its own field because a query ("every link I
    /// created") needs an id to filter on, not a name to compare.
    /// </summary>
    public required Guid CreatedByUserId { get; set; }

    /// <summary>
    /// Soft-delete timestamp, mirroring <see cref="Diten.Platform.Domain.Entities.Tasks.TaskDependency"/> — the
    /// closest existing entity of this exact shape (a typed edge between two records). <c>BaseEntity.IsDeleted</c>
    /// alone drives the repository's execution filter; this field is kept for the same reason
    /// <c>TaskDependency</c> keeps it: an auditable "when."
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>The module codes this slice actually writes or reads. Not a closed enum — see <see cref="RecordLink.SourceModuleCode"/>.</summary>
public static class RecordLinkModuleCodes
{
    public const string Tasks = "tasks";
    public const string Meetings = "meetings";
}

/// <summary>The link types the pack (§3) names. Not a closed enum — see <see cref="RecordLink.LinkType"/>.</summary>
public static class RecordLinkTypes
{
    /// <summary>A task prepared BEFORE the meeting it is linked to.</summary>
    public const string Preparation = "preparation";

    /// <summary>An agenda line IS this link — the agenda item that carries a `RecordLinkId`.</summary>
    public const string Agenda = "agenda";

    /// <summary>A task raised DURING the meeting or FROM its minutes (an action from a decision).</summary>
    public const string BornFromMeeting = "bornFromMeeting";

    /// <summary>The receiving side of MOD-0024's `scheduleReviewMeeting` — a meeting opened to satisfy a task
    /// type's `reviewMeetingPolicy.required`.</summary>
    public const string ReviewMeeting = "reviewMeeting";
}
