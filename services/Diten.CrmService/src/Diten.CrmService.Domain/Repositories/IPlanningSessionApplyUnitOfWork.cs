using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// MOD-0155 FU05 apply/re-plan unit of work (D-APPLY-ATOMICITY = C, LOCKED). Writing the FU01
/// <see cref="PlannedVisit"/> atoms and flipping the <see cref="PlanningSession"/> to <c>committed</c> must be
/// ALL-OR-NOTHING: a mid-apply failure must leave NO half-plan and must NOT flip the session. This spans two
/// collections (planned_visits + planning_sessions), so it needs a real multi-document transaction on a replica set —
/// guarded by a capability probe + a compensation fallback so dev STANDALONE Mongo (which has no transactions) still
/// works without a 500 (the CRM standalone-Mongo transaction-fallback rule).
/// <para>It reuses FU01's own <c>planned_visits</c> collection and aggregate — it does NOT duplicate FU01 storage and
/// does NOT change the aggregate shape; the atoms are fully-formed by the engine (Slot/Content/Availability/Selection
/// already filled) and simply persisted here.</para>
/// </summary>
public interface IPlanningSessionApplyUnitOfWork
{
    /// <summary>Atomically insert <paramref name="atoms"/> into <c>planned_visits</c> and replace
    /// <paramref name="session"/> (already flipped to <c>committed</c> with its <c>CommittedPlannedVisitIds</c> set) at
    /// its <paramref name="expectedVersion"/>. Returns false on a session concurrency mismatch (nothing is written).</summary>
    Task<bool> ApplyAsync(
        PlanningSession session, int expectedVersion, IReadOnlyList<PlannedVisit> atoms, CancellationToken cancellationToken);

    /// <summary>Atomically replace the affected <paramref name="atoms"/> IN PLACE (re-plan, D-REPLAN = A) — the session
    /// is not reopened. All-or-nothing over the subset; no new revision is created.</summary>
    Task ReplanAsync(IReadOnlyList<PlannedVisit> atoms, CancellationToken cancellationToken);
}
