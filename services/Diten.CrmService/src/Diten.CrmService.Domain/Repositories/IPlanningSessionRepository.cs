using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// MOD-0155 FU05 PlanningSession staging store — one collection (<c>planning_sessions</c>), tenant scoped,
/// soft-delete aware. A session is archived, never hard-deleted, so its provenance stays readable. Single-document
/// writes are guarded by the optimistic <see cref="EntityBase.Version"/> token; the only multi-document write is the
/// atomic apply, which lives in <see cref="IPlanningSessionApplyUnitOfWork"/> (it spans planning_sessions +
/// planned_visits and needs the transaction + standalone fallback, D-APPLY-ATOMICITY = C).
/// </summary>
public interface IPlanningSessionRepository
{
    Task<PlanningSession?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>Every non-deleted session of the tenant (archived included — history stays readable). Ordering happens
    /// in memory so the DateTimeOffset audit fields are never a server-side sort key (parallel-arrays 500).</summary>
    Task<IReadOnlyList<PlanningSession>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>Sessions for one rep in one period (the session-for-rep-in-period lookup).</summary>
    Task<IReadOnlyList<PlanningSession>> ListByPeriodAndResourceAsync(
        Guid tenantId, Guid cyclePeriodId, string resourceId, CancellationToken cancellationToken);

    Task InsertAsync(PlanningSession entity, CancellationToken cancellationToken);

    /// <summary>Optimistic replace: matches on (Id, TenantId, Version == expectedVersion) and bumps the token. Returns
    /// false on a concurrency mismatch so the handler can answer 409 instead of overwriting silently.</summary>
    Task<bool> ReplaceAsync(PlanningSession entity, int expectedVersion, CancellationToken cancellationToken);
}
