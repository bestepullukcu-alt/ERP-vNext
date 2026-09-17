using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

public interface ITerritoryNodeRepository
{
    Task<TerritoryNode?> GetByIdAsync(Guid tenantId, Guid modelId, Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsByCodeAsync(Guid tenantId, Guid modelId, string territoryCode, Guid? excludeId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TerritoryNode>> ListByModelAsync(Guid tenantId, Guid modelId, CancellationToken cancellationToken);

    /// <summary>Bulk reverse lookup by node id ACROSS models within one tenant (WP-SEG-DETAILS8). A consumer that holds
    /// only node ids (a segment criterion stores the node id, not its parent model) resolves them to {name, modelId} in
    /// ONE read. Tenant-scoped; deleted nodes are excluded; an empty id set returns an empty list without a query.</summary>
    Task<IReadOnlyList<TerritoryNode>> ListByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Walks the parent chain from <paramref name="candidateParentId"/> to detect whether re-parenting
    /// <paramref name="nodeId"/> under it would create a cycle (candidate is the node itself or one of its descendants).</summary>
    Task<bool> WouldCreateCycleAsync(Guid tenantId, Guid modelId, Guid nodeId, Guid candidateParentId, CancellationToken cancellationToken);

    Task InsertAsync(TerritoryNode node, CancellationToken cancellationToken);

    Task UpdateAsync(TerritoryNode node, CancellationToken cancellationToken);
}
