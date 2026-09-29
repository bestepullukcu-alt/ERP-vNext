using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.TimeEntry;

namespace Diten.Platform.Domain.Entities.TimeEntry;

/// <summary>
/// MOD-0280-FU01 (pack §4.5, D8) — the person's decision on one meeting suggestion. Unique per (tenant, meeting, user).
///
/// <para><b>Stored only when the person decides.</b> An OPEN suggestion is derived at read time from the person's
/// accepted, ended meetings and carries a deterministic id (meeting + user), so reading a week never writes. The row
/// appears on accept or dismiss, under that same id. "Confirmed by minutes", "withdrawn" and "conflict" are never
/// stored: they are read from the meeting's CURRENT minutes attendance, so a minutes correction is picked up on the next
/// read.</para>
/// </summary>
public sealed class TimeSuggestion : TenantScopedEntity
{
    public required Guid UserId { get; set; }

    /// <summary>MOD-0357 meeting id — a reference only, read through the meeting port.</summary>
    public required Guid MeetingId { get; set; }

    /// <summary>The tenant-local day the meeting started on.</summary>
    public required DateOnly LocalDate { get; set; }

    /// <summary>Scheduled duration, rounded to 15 minutes (A2), as it stood when the person decided.</summary>
    public int ProposedMinutes { get; set; }

    public TimeSuggestionState State { get; set; } = TimeSuggestionState.Open;

    /// <summary>The draft row the accepted minutes went into.</summary>
    public Guid? AcceptedEntryId { get; set; }

    public DateTimeOffset? DecidedAtUtc { get; set; }
}
