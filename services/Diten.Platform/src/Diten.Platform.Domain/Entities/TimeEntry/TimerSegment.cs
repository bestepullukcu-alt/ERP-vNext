using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.TimeEntry;

namespace Diten.Platform.Domain.Entities.TimeEntry;

/// <summary>
/// MOD-0280-FU01 (pack §4.1, D2, D3, D4) — one run of a person's timer. Raw working data, never the record itself: a
/// closed segment becomes a DRAFT row the person still has to submit (Z-2).
///
/// <para><b>Storage rules.</b> One running segment per (tenant, user) — a partial unique index over
/// <see cref="IsRunning"/>, so two concurrent starts cannot both land. A transition that started a segment is recorded in
/// <see cref="StartTransitionId"/> under a sparse unique index: the same MOD-0024 transition observed twice starts ONE
/// segment. A segment never crosses the tenant-local midnight of <see cref="LocalDate"/>.</para>
///
/// <para><b>Minimisation, not deletion (D4, R8).</b> When the week is approved, <see cref="StartedAtUtc"/> and
/// <see cref="StoppedAtUtc"/> are cleared and <see cref="MinimisedAtUtc"/> is set by an audited update; the duration,
/// the day, the target and the outside-hours minutes stay. Nothing here is ever deleted.</para>
/// </summary>
public sealed class TimerSegment : TenantScopedEntity
{
    /// <summary>The person the time belongs to — the caller, or the task holder when the hook started it.</summary>
    public required Guid UserId { get; set; }

    /// <summary>MOD-0024 task id — a reference only. Exactly one of this and <see cref="CategoryCode"/>.</summary>
    public Guid? TaskItemId { get; set; }

    /// <summary>An active <see cref="WorkCategory.Code"/>.</summary>
    public string? CategoryCode { get; set; }

    /// <summary>True while the timer runs. Partial unique index {TenantId, UserId} where true (D2).</summary>
    public bool IsRunning { get; set; }

    /// <summary>Server clock only. Null once minimised (D4).</summary>
    public DateTimeOffset? StartedAtUtc { get; set; }

    /// <summary>Null while running; null again once minimised (D4).</summary>
    public DateTimeOffset? StoppedAtUtc { get; set; }

    /// <summary>Written at stop; kept after minimisation.</summary>
    public int DurationSeconds { get; set; }

    /// <summary>The tenant-local day the segment started on (R3). The segment ends on it, at the latest at its midnight.</summary>
    public required DateOnly LocalDate { get; set; }

    /// <summary>The zone the working-hours provider answered at start.</summary>
    public required string TimeZoneId { get; set; }

    /// <summary>ISO week of <see cref="LocalDate"/>.</summary>
    public required string WeekKey { get; set; }

    public TimerStartSource StartSource { get; set; }

    /// <summary>MOD-0024 <c>TaskTransition.Id</c> when the hook started it — sparse unique (idempotency).</summary>
    public Guid? StartTransitionId { get; set; }

    public TimerStopReason? StopReason { get; set; }

    /// <summary>The transition that stopped it, when one did.</summary>
    public Guid? StopTransitionId { get; set; }

    /// <summary>Minutes outside the day's working window(s), computed at stop (D3). Kept after minimisation.</summary>
    public int OutsideWorkingMinutes { get; set; }

    /// <summary>Set on the segment a switch STARTED: the one it stopped, so an undo can restart that target.</summary>
    public Guid? SwitchedFromSegmentId { get; set; }

    /// <summary>The undo token of a switch — valid for <c>TimerRules.UndoWindow</c> after <see cref="StartedAtUtc"/>.</summary>
    public Guid? SwitchToken { get; set; }

    /// <summary>Set once the one morning notification for a midnight close was handed over (D3) — never twice.</summary>
    public DateTimeOffset? AutoCloseNotifiedAtUtc { get; set; }

    /// <summary>Set when the approval finalizer cleared the instants (D4).</summary>
    public DateTimeOffset? MinimisedAtUtc { get; set; }
}
