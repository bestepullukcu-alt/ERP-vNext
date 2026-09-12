using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.Meetings;

/// <summary>
/// MOD-0357 S2 — one ordered agenda line (pack §3/§4). Its own collection for the same reason
/// <see cref="MeetingAttendee"/> is: a full-replace update of <see cref="Meeting"/> must never risk losing the
/// list by omission.
/// </summary>
public sealed class AgendaItem : TenantScopedEntity
{
    public required Guid MeetingId { get; set; }

    public required string Text { get; set; }

    /// <summary>Server-assigned. A continuation meeting's carried-over open actions are inserted at the FRONT
    /// (K6, S7) — not built here; every line in this slice is appended in the order it was added.</summary>
    public required int SortOrder { get; set; }

    /// <summary>Set when this line IS a prepared-in-advance task or a carried-over open action — i.e. the far
    /// end of a <see cref="RecordLink"/> — never for a plain typed line. This slice writes no <see cref="RecordLink"/>
    /// from an agenda item (that is S4, "meeting → task"); the field exists so S4 has somewhere to put it without
    /// a schema change.</summary>
    public Guid? RecordLinkId { get; set; }
}
