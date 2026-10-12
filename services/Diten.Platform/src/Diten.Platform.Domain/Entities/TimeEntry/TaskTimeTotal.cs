using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.TimeEntry;

/// <summary>
/// MOD-0280-FU01 (pack §4.4, D7) — approved minutes per task: the single source of a task's spent time.
///
/// <para><b>One writer, and it RECOMPUTES.</b> Only the approval finalizer writes this collection, and it writes the
/// sum of the in-force approved entries for the task — never an increment. A replayed finalization therefore writes
/// the same number, which is the whole idempotency argument (T-14).</para>
/// </summary>
public sealed class TaskTimeTotal : TenantScopedEntity
{
    public required Guid TaskItemId { get; set; }

    public int ApprovedMinutes { get; set; }

    public DateTimeOffset RecomputedAtUtc { get; set; }

    public Guid? LastFinalizedWeekId { get; set; }
}
