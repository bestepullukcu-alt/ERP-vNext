using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// MOD-0155 FU02 VisitReport store — ONE collection (<c>visit_reports</c>), tenant scoped, soft-delete aware, and with
/// <b>no delete method</b>: a report is the immutable compliance record of an executed visit, so it is never deleted;
/// corrections are append-only amendments. Every write is a single-document operation guarded by the optimistic
/// <see cref="EntityBase.Version"/> token — the report submit / amend touch only the <see cref="VisitReport"/> aggregate,
/// so no multi-document transaction is needed (the D-EXECUTION-STATUS = A plan reflection is a documented no-op — FU01
/// exposes no "executed" transition, F-EXECUTED-MARKER — so there is no second aggregate to write).
/// <para><see cref="VisitReport.ExecutedAt"/> is a lone DateTimeOffset and is never a server-side sort key (parallel
/// arrays); ordering happens in memory. 1:1 with the plan atom is enforced in the handler via
/// <see cref="GetByPlannedVisitIdAsync"/>, not by a partial index with a <c>$ne</c> filter (which crash-loops startup).</para>
/// </summary>
public interface IVisitReportRepository
{
    Task<VisitReport?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>The single report for a plan atom (1:1), or null. The report-for-a-visit lookup + the 1:1 guard.</summary>
    Task<VisitReport?> GetByPlannedVisitIdAsync(Guid tenantId, Guid plannedVisitId, CancellationToken cancellationToken);

    /// <summary>Every non-deleted report of the tenant. Filtering/ordering happen in memory to avoid sorting the
    /// DateTimeOffset fields at the server (parallel-arrays).</summary>
    Task<IReadOnlyList<VisitReport>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>Every report whose plan atom is in the given id set — the calendar-join bulk read (one read for the whole
    /// window, never one per visit).</summary>
    Task<IReadOnlyList<VisitReport>> ListByPlannedVisitIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> plannedVisitIds, CancellationToken cancellationToken);

    Task InsertAsync(VisitReport entity, CancellationToken cancellationToken);

    /// <summary>Optimistic replace: matches on (Id, TenantId, Version == expectedVersion) and bumps the token. Returns
    /// false on a concurrency mismatch so the handler can answer 409 instead of overwriting silently.</summary>
    Task<bool> ReplaceAsync(VisitReport entity, int expectedVersion, CancellationToken cancellationToken);
}
