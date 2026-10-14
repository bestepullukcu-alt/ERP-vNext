using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// WP-VW-W3 (BE-a) — the visit execution store: ONE collection (<c>visit_executions</c>), tenant scoped, one record per
/// planned visit. The 1:1 rule is enforced in the DATABASE (unique <c>(TenantId, PlannedVisitId)</c>, equality-only
/// partial filter): a concurrent second start loses with <see cref="InsertAsync"/> answering <c>false</c>. No delete — a
/// started visit is evidence.
/// </summary>
public interface IVisitExecutionRepository
{
    Task<VisitExecution?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<VisitExecution?> GetByPlannedVisitIdAsync(Guid tenantId, Guid plannedVisitId, CancellationToken cancellationToken);

    /// <summary>The calendar join: one read for the whole window.</summary>
    Task<IReadOnlyList<VisitExecution>> ListByPlannedVisitIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> plannedVisitIds, CancellationToken cancellationToken);

    /// <summary>Inserts the record; <c>false</c> when the plan already has one (the unique index won).</summary>
    Task<bool> InsertAsync(VisitExecution entity, CancellationToken cancellationToken);

    /// <summary>Optimistic replace on (Id, TenantId, Version); bumps the token; <c>false</c> on a mismatch.</summary>
    Task<bool> ReplaceAsync(VisitExecution entity, int expectedVersion, CancellationToken cancellationToken);

    /// <summary>WP-VW-W3E (BE-b, §2.6) — one page of the evidence flags read, newest start first, and the total.</summary>
    Task<(IReadOnlyList<VisitExecution> Items, long Total)> ListForEvidenceFlagsAsync(
        Guid tenantId, VisitEvidenceFlagFilter filter, int skip, int take, CancellationToken cancellationToken)
        => throw new NotSupportedException("The evidence flags read needs the Mongo repository.");

    /// <summary>WP-VW-W3E (BE-b, §2.6) — every start of the filter for the summary (only Id, ResourceId and Flags are read).</summary>
    Task<IReadOnlyList<VisitExecution>> ListForEvidenceSummaryAsync(
        Guid tenantId, VisitEvidenceFlagFilter filter, CancellationToken cancellationToken)
        => throw new NotSupportedException("The evidence flags summary needs the Mongo repository.");
}

/// <summary>WP-VW-W3 (BE-a, K-W2) — the tenant's single evidence policy document.</summary>
public interface IVisitEvidencePolicyRepository
{
    /// <summary>The tenant's document, or null (the defaults apply; nothing is written on a read).</summary>
    Task<VisitEvidencePolicy?> GetAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<bool> InsertAsync(VisitEvidencePolicy entity, CancellationToken cancellationToken);

    Task<bool> ReplaceAsync(VisitEvidencePolicy entity, int expectedVersion, CancellationToken cancellationToken);
}

/// <summary>WP-VW-W3 (W3-BE-c) — the append-only evidence policy history (newest first).</summary>
public interface IVisitEvidencePolicyChangeRepository
{
    Task InsertManyAsync(IReadOnlyCollection<VisitEvidencePolicyChange> rows, CancellationToken cancellationToken);

    Task<(IReadOnlyList<VisitEvidencePolicyChange> Items, long Total)> ListAsync(
        Guid tenantId, int skip, int take, CancellationToken cancellationToken);
}

/// <summary>WP-VW-W3 (W3-BE-c) — the time-boxed evidence exceptions. Tenant scoped; no delete (cancel / end early).</summary>
public interface IVisitEvidenceExceptionRepository
{
    Task<VisitEvidenceException?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<VisitEvidenceException>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<VisitEvidenceException>> ListByResourceAsync(Guid tenantId, string resourceId, CancellationToken cancellationToken);

    Task InsertAsync(VisitEvidenceException entity, CancellationToken cancellationToken);

    /// <summary>Optimistic replace: succeeds only on <paramref name="expectedVersion"/>, then bumps the version.</summary>
    Task<bool> ReplaceAsync(VisitEvidenceException entity, int expectedVersion, CancellationToken cancellationToken);
}
