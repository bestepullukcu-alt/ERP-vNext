namespace Diten.Platform.Application.Contracts;

/// <summary>A task's recorded time, in minutes. The two figures are never added together (D7).</summary>
/// <param name="ApprovedMinutes">Minutes in approved, in-force timesheet revisions — the task's spent time.</param>
/// <param name="SubmittedMinutes">Minutes in revisions submitted and not yet decided. Shown separately, never counted as spent. A
/// correction contributes only its CHANGE against the approved revision it corrects (v2 F7), so this may be negative.</param>
public sealed record TaskSpentTime(int ApprovedMinutes, int SubmittedMinutes)
{
    public static readonly TaskSpentTime None = new(0, 0);

    /// <summary>The approved figure in hours — what MOD-0024's <c>SpentHours</c> fields carry.</summary>
    public decimal ApprovedHours => ApprovedMinutes / 60m;
}

/// <summary>What ONE reader has on these tasks: their running timer and their own unsubmitted draft minutes.</summary>
/// <param name="RunningTaskItemId">The task the reader's running timer points at, if any.</param>
/// <param name="DraftMinutes">The reader's own minutes per task in their Draft revisions — never anyone else's; for a correction
/// draft, its change against the approved revision (v2 F7).</param>
public sealed record TaskReaderTime(Guid? RunningTaskItemId, IReadOnlyDictionary<Guid, int> DraftMinutes)
{
    public static readonly TaskReaderTime None = new(null, new Dictionary<Guid, int>());
}

/// <summary>
/// MOD-0280-FU01 (pack §3.3, §5.1 item 2, D7) — where MOD-0024 reads a task's spent time from. <c>TaskItem.SpentHours</c>
/// stays on the entity, always 0 and never written; every read site asks this instead: the task detail and its
/// remaining estimate, the Task Center projection, the work report and its export.
///
/// <para>Batch reads only — one call per page or report, never one per task.</para>
///
/// <para>On extraction (ADR-004) this becomes an HTTP read; the read sites do not change.</para>
/// </summary>
public interface ITaskSpentTimeSource
{
    /// <summary>Approved minutes per task (tasks with none are absent). Cheap: one read of the totals.</summary>
    Task<IReadOnlyDictionary<Guid, int>> ApprovedMinutesAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken ct = default);

    /// <summary>Approved AND, separately, submitted-not-decided minutes per task (tasks with neither are absent).</summary>
    Task<IReadOnlyDictionary<Guid, TaskSpentTime>> SpentTimeAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken ct = default);

    /// <summary>The reader's own running timer and draft minutes on these tasks (D11: per reader, never another person's).</summary>
    Task<TaskReaderTime> ReaderTimeAsync(Guid readerUserId, IReadOnlyCollection<Guid> taskIds, CancellationToken ct = default);
}

/// <summary>
/// MOD-0280-FU01 §5.1 item 3 — whether the Task Center projection DECLARES <c>timeTracking</c> (with its
/// <c>timeEntries</c> block and the per-reader <c>timerState</c>).
///
/// <para><b>Off until T2, on purpose (the stop rule).</b> The existing detail card (<c>renderTimesheet</c>) starts drawing
/// the moment the capability is declared, and T1b ships no screen. The WC-1 contract couples the three both ways
/// (<c>fixture-contract.js</c> <c>validateCapabilities</c>): <c>timeEntries</c> without the capability is
/// <c>CAPABILITY_REQUIRED_FOR_DATA</c>, a <c>running</c> timer state without it is <c>TIME_TRACKING_CAPABILITY_REQUIRED</c>.
/// So while this is off the projection emits NONE of the three — today's shape — and the data path behind them is built
/// and tested with it on. T2 turns it on together with the card.</para>
/// </summary>
public sealed class TaskTimeTrackingOptions
{
    public bool DeclareTimeTracking { get; set; }
}
