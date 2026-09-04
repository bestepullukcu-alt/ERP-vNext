using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// MOD-0155 FU01 PlannedVisit master — one collection, tenant scoped, soft-delete aware. There is deliberately
/// <b>no delete method</b> (§8.2): a plan is cancelled and/or archived, and history stays readable. Every write is a
/// single-document operation guarded by the optimistic <see cref="EntityBase.Version"/> token, so no multi-document
/// transaction and no compensation is needed (§8.4).
/// </summary>
public interface IPlannedVisitRepository
{
    Task<PlannedVisit?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>Every non-deleted plan of the tenant (archived included — history must stay readable). Filtering and
    /// ordering happen in memory to avoid sorting the DateOnly/DateTimeOffset fields at the server (parallel-arrays).</summary>
    Task<IReadOnlyList<PlannedVisit>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>Every row carrying this VisitCode, so code uniqueness is decided in the handler rather than through a
    /// partial index with a <c>$ne</c> filter (which crash-loops the service at startup).</summary>
    Task<IReadOnlyList<PlannedVisit>> ListByCodeAsync(Guid tenantId, string visitCode, CancellationToken cancellationToken);

    /// <summary>Every plan of one resource on one day — the overlap guard's (V22) pre-check.</summary>
    Task<IReadOnlyList<PlannedVisit>> ListByResourceAndDateAsync(
        Guid tenantId, string resourceId, DateOnly plannedDate, CancellationToken cancellationToken);

    /// <summary>Every plan against one target on one day — the same-day-same-type guard's (V24) pre-check.</summary>
    Task<IReadOnlyList<PlannedVisit>> ListByTargetAndDateAsync(
        Guid tenantId, Guid targetId, DateOnly plannedDate, CancellationToken cancellationToken);

    Task InsertAsync(PlannedVisit entity, CancellationToken cancellationToken);

    /// <summary>Optimistic replace: matches on (Id, TenantId, Version == expectedVersion) and bumps the token. Returns
    /// false on a concurrency mismatch so the handler can answer 409 instead of overwriting silently.</summary>
    Task<bool> ReplaceAsync(PlannedVisit entity, int expectedVersion, CancellationToken cancellationToken);
}
