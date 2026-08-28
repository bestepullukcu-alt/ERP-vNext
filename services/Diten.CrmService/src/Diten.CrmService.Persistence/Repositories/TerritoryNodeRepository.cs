using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

public sealed class TerritoryNodeRepository : ITerritoryNodeRepository
{
    private const int MaxHierarchyWalk = 100;
    private readonly IMongoCollection<TerritoryNode> _collection;

    public TerritoryNodeRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<TerritoryNode>("territory_nodes");
    }

    private static FilterDefinition<TerritoryNode> ActiveModel(Guid tenantId, Guid modelId)
        => Builders<TerritoryNode>.Filter.Where(n => n.TenantId == tenantId && n.ModelId == modelId && !n.IsDeleted);

    public async Task<TerritoryNode?> GetByIdAsync(Guid tenantId, Guid modelId, Guid id, CancellationToken cancellationToken)
    {
        var filter = ActiveModel(tenantId, modelId) & Builders<TerritoryNode>.Filter.Eq(n => n.Id, id);
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ExistsByCodeAsync(Guid tenantId, Guid modelId, string territoryCode, Guid? excludeId, CancellationToken cancellationToken)
    {
        var filter = ActiveModel(tenantId, modelId) & Builders<TerritoryNode>.Filter.Eq(n => n.TerritoryCode, territoryCode);
        if (excludeId is { } id)
        {
            filter &= Builders<TerritoryNode>.Filter.Ne(n => n.Id, id);
        }

        return await _collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TerritoryNode>> ListByModelAsync(Guid tenantId, Guid modelId, CancellationToken cancellationToken)
        => await _collection.Find(ActiveModel(tenantId, modelId))
            .SortBy(n => n.SortOrder).ThenBy(n => n.TerritoryCode)
            .ToListAsync(cancellationToken);

    public async Task<bool> WouldCreateCycleAsync(Guid tenantId, Guid modelId, Guid nodeId, Guid candidateParentId, CancellationToken cancellationToken)
    {
        var current = (Guid?)candidateParentId;
        var steps = 0;
        while (current is { } cursor && steps++ < MaxHierarchyWalk)
        {
            if (cursor == nodeId)
            {
                return true;
            }

            var node = await GetByIdAsync(tenantId, modelId, cursor, cancellationToken);
            current = node?.ParentTerritoryId;
        }

        return false;
    }

    public async Task InsertAsync(TerritoryNode node, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(node, cancellationToken: cancellationToken);

    public async Task UpdateAsync(TerritoryNode node, CancellationToken cancellationToken)
    {
        var filter = Builders<TerritoryNode>.Filter.Where(n => n.Id == node.Id && n.TenantId == node.TenantId);
        await _collection.ReplaceOneAsync(filter, node, cancellationToken: cancellationToken);
    }
}
