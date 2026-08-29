using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// MOD-0167 FU04 StrategyTemplate master (all four binding lists embedded — one collection, one optimistic token).
/// Tenant scoped and soft-delete aware. There is deliberately <b>no delete method</b>: closing a template is the soft
/// archive lifecycle, so a past play stays explainable. Every write is a single-document operation, so no
/// multi-document transaction and no compensation is needed.
/// </summary>
public interface IStrategyTemplateRepository
{
    Task<StrategyTemplate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>Every non-deleted template of the tenant (archived included — history must stay readable).</summary>
    Task<IReadOnlyList<StrategyTemplate>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>All versions sharing a lineage, so a new-version clone can compute the next TemplateVersion and the
    /// activate path can supersede its predecessor.</summary>
    Task<IReadOnlyList<StrategyTemplate>> ListByLineageAsync(
        Guid tenantId, Guid versionLineageId, CancellationToken cancellationToken);

    /// <summary>Every row carrying this code (any version, archived included), so code uniqueness is decided in the
    /// handler rather than through a partial index with a <c>$ne</c> filter (which crash-loops the service).</summary>
    Task<IReadOnlyList<StrategyTemplate>> ListByCodeAsync(
        Guid tenantId, string templateCode, CancellationToken cancellationToken);

    Task InsertAsync(StrategyTemplate entity, CancellationToken cancellationToken);

    /// <summary>Optimistic replace: matches on (Id, TenantId, Version == expectedVersion) and bumps the token. Returns
    /// false on a concurrency mismatch so the handler can answer 409 instead of overwriting silently.</summary>
    Task<bool> ReplaceAsync(StrategyTemplate entity, int expectedVersion, CancellationToken cancellationToken);
}
