using Diten.Platform.Domain.Entities.Workflow;

namespace Diten.Platform.Domain.Repositories;

/// <summary>
/// BL-549 + BL-539 + BL-540 (1) — the reads the engine's recovery needs and its ordinary repositories do not offer: they
/// join instances to their tasks on the server, so a cycle never loads every open instance of a tenant into memory. All
/// tenant-scoped (the current tenant), all read-only.
/// </summary>
public interface IWorkflowStartRecoveryStore
{
    /// <summary>
    /// Generic (non-trusted) instances that are Active, have NOT moved since <paramref name="movedBeforeUtc"/> and have NO
    /// task at all (any status, deleted ones included) — the starts that crashed after writing their instance: at most
    /// <paramref name="maxItems"/> of them (by id) and how many there are, in ONE pass over the store (FIX1 K3).
    /// FIX2 item 4 — bounded: at most <paramref name="scanLimit"/> unmoved Active instances are looked at; past that the total
    /// is a lower bound (<see cref="RecoveryCount.Capped"/>).
    /// </summary>
    Task<(IReadOnlyList<WorkflowInstance> Page, RecoveryCount Total)> FindAbandonedGenericStartsAsync(
        DateTimeOffset movedBeforeUtc,
        int maxItems,
        int scanLimit,
        CancellationToken ct = default);

    /// <summary>
    /// FIX2 item 4 — THE COUNT ALONE (the count-only cycle): the same abandoned starts, counted on the server, no page read.
    /// At most <paramref name="scanLimit"/> unmoved Active instances are looked at; past that the count is "at least".
    /// </summary>
    Task<RecoveryCount> CountAbandonedGenericStartsAsync(
        DateTimeOffset movedBeforeUtc,
        int scanLimit,
        CancellationToken ct = default);

    /// <summary>
    /// BL-539 — trusted reservations still Pending short of Completed that have not moved (no checkpoint advanced) since
    /// <paramref name="movedBeforeUtc"/>: the gauge, counted up to <paramref name="limit"/> (FIX2 item 4).
    /// </summary>
    Task<RecoveryCount> CountIncompleteTrustedStartsAsync(DateTimeOffset movedBeforeUtc, int limit, CancellationToken ct = default);

    /// <summary>BL-539 (CT decision) — the same reservations, oldest first, at most <paramref name="maxItems"/>: what the recovery
    /// closes.</summary>
    Task<IReadOnlyList<WorkflowInstance>> ListIncompleteTrustedStartsAsync(
        DateTimeOffset movedBeforeUtc,
        int maxItems,
        CancellationToken ct = default);

    /// <summary>
    /// BL-540 (1) — generic instances still open (Pending, Active, Escalated) under the given object types, oldest first,
    /// at most <paramref name="maxItems"/>. Whether one is an orphan is decided by the caller against the Task Center.
    /// </summary>
    Task<IReadOnlyList<WorkflowInstance>> ListOpenGenericInstancesAsync(
        IReadOnlyCollection<string> objectTypes,
        int maxItems,
        Guid? afterId = null,
        CancellationToken ct = default);
}

/// <summary>FIX2 item 4 — a bounded count: <see cref="Capped"/> = the scan stopped at its limit, so the true number is at
/// least <see cref="Count"/> (reported as "≥ N").</summary>
public readonly record struct RecoveryCount(long Count, bool Capped);
