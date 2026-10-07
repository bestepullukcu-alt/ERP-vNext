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
    /// task at all (any status, deleted ones included) — the starts that crashed after writing their instance. Oldest first,
    /// at most <paramref name="maxItems"/>.
    /// </summary>
    Task<IReadOnlyList<WorkflowInstance>> ListAbandonedGenericStartsAsync(
        DateTimeOffset movedBeforeUtc,
        int maxItems,
        CancellationToken ct = default);

    /// <summary>How many generic starts are abandoned as above (the gauge), without the page limit.</summary>
    Task<long> CountAbandonedGenericStartsAsync(DateTimeOffset movedBeforeUtc, CancellationToken ct = default);

    /// <summary>
    /// BL-539 — trusted reservations still Pending short of Completed that have not moved (no checkpoint advanced) since
    /// <paramref name="movedBeforeUtc"/>: the gauge.
    /// </summary>
    Task<long> CountIncompleteTrustedStartsAsync(DateTimeOffset movedBeforeUtc, CancellationToken ct = default);

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
        CancellationToken ct = default);
}
