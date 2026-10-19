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

    /// <summary>WP-VW-W3L — the history of ONE scope (<paramref name="scope"/> = tenant | legal-entity | business-unit;
    /// <paramref name="scopeRef"/> null = every ref of that type). The default reads the whole history and filters; the
    /// Mongo store filters in the database.</summary>
    async Task<(IReadOnlyList<VisitEvidencePolicyChange> Items, long Total)> ListAsync(
        Guid tenantId, string scope, string? scopeRef, int skip, int take, CancellationToken cancellationToken)
    {
        var (all, _) = await ListAsync(tenantId, 0, int.MaxValue, cancellationToken);
        var rows = all.Where(c => VisitEvidencePolicyChangeFilter.Matches(c, scope, scopeRef)).ToList();
        return (rows.Skip(skip).Take(take).ToList(), rows.Count);
    }
}

public static class VisitEvidencePolicyChangeFilter
{
    /// <summary>A history row belongs to a scope: the same type and (when given) the same ref, case-insensitively.</summary>
    public static bool Matches(VisitEvidencePolicyChange c, string scope, string? scopeRef)
        => string.Equals(c.Scope, scope, StringComparison.Ordinal)
           && (scopeRef is null || string.Equals(c.ScopeRef, scopeRef, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// WP-VW-W3L (W3-BE-d, K-L2 / K-L6) — the evidence policy LAYERS above the tenant (<c>visit_evidence_policy_layers</c>):
/// keyed (TenantId, ScopeType, ScopeRef), one ACTIVE layer per key (database-unique), versioned replace, archive instead
/// of delete. The tenant layer stays in <see cref="IVisitEvidencePolicyRepository"/>.
/// </summary>
public interface IVisitEvidencePolicyLayerRepository
{
    /// <summary>The tenant's ACTIVE layers (archived ones are history only).</summary>
    Task<IReadOnlyList<VisitEvidencePolicyLayer>> ListActiveAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>The active layer of one scope (the ref compared case-insensitively), or null.</summary>
    Task<VisitEvidencePolicyLayer?> GetActiveAsync(Guid tenantId, string scopeType, string scopeRef, CancellationToken cancellationToken);

    /// <summary>False when an active layer of the same key already exists (a concurrent create lost).</summary>
    Task<bool> InsertAsync(VisitEvidencePolicyLayer entity, CancellationToken cancellationToken);

    /// <summary>Optimistic replace: false when the stored version is no longer <paramref name="expectedVersion"/>.</summary>
    Task<bool> ReplaceAsync(VisitEvidencePolicyLayer entity, int expectedVersion, CancellationToken cancellationToken);
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
