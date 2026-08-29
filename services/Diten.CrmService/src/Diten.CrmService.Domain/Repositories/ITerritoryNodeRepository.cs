using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

public interface ITerritoryNodeRepository
{
    Task<TerritoryNode?> GetByIdAsync(Guid tenantId, Guid modelId, Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsByCodeAsync(Guid tenantId, Guid modelId, string territoryCode, Guid? excludeId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TerritoryNode>> ListByModelAsync(Guid tenantId, Guid modelId, CancellationToken cancellationToken);

    /// <summary>Walks the parent chain from <paramref name="candidateParentId"/> to detect whether re-parenting
    /// <paramref name="nodeId"/> under it would create a cycle (candidate is the node itself or one of its descendants).</summary>
    Task<bool> WouldCreateCycleAsync(Guid tenantId, Guid modelId, Guid nodeId, Guid candidateParentId, CancellationToken cancellationToken);

    Task InsertAsync(TerritoryNode node, CancellationToken cancellationToken);

    Task UpdateAsync(TerritoryNode node, CancellationToken cancellationToken);
}
