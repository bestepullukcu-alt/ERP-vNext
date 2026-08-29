using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

public sealed class TerritoryModelRepository : ITerritoryModelRepository
{
    private readonly IMongoCollection<TerritoryModel> _collection;

    public TerritoryModelRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<TerritoryModel>("territory_models");
    }

    private static FilterDefinition<TerritoryModel> ActiveTenant(Guid tenantId)
        => Builders<TerritoryModel>.Filter.Where(m => m.TenantId == tenantId && !m.IsDeleted);

    public async Task<TerritoryModel?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        var filter = ActiveTenant(tenantId) & Builders<TerritoryModel>.Filter.Eq(m => m.Id, id);
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ExistsByCodeAsync(Guid tenantId, string modelCode, Guid? excludeId, CancellationToken cancellationToken)
    {
        var filter = ActiveTenant(tenantId) & Builders<TerritoryModel>.Filter.Eq(m => m.ModelCode, modelCode);
        if (excludeId is { } id)
        {
            filter &= Builders<TerritoryModel>.Filter.Ne(m => m.Id, id);
        }

        return await _collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<TerritoryModel> Items, long Total)> ListAsync(
        Guid tenantId, string? search, string? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var filter = ActiveTenant(tenantId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var regex = Builders<TerritoryModel>.Filter.Regex(m => m.Name, new MongoDB.Bson.BsonRegularExpression(term, "i"))
                        | Builders<TerritoryModel>.Filter.Regex(m => m.ModelCode, new MongoDB.Bson.BsonRegularExpression(term, "i"));
            filter &= regex;
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filter &= Builders<TerritoryModel>.Filter.Eq(m => m.Status, status.Trim());
        }

        var total = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _collection.Find(filter)
            .SortBy(m => m.ModelCode)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<IReadOnlyList<TerritoryModel>> ListActiveAsync(
        Guid tenantId, Guid excludeId, CancellationToken cancellationToken)
    {
        var filter = ActiveTenant(tenantId)
                     & Builders<TerritoryModel>.Filter.Ne(m => m.Id, excludeId)
                     & Builders<TerritoryModel>.Filter.Eq(m => m.Status, "active");
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TerritoryModel>> ListByIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids is null || ids.Count == 0) return [];
        var filter = ActiveTenant(tenantId) & Builders<TerritoryModel>.Filter.In(m => m.Id, ids);
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task InsertAsync(TerritoryModel model, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(model, cancellationToken: cancellationToken);

    public async Task UpdateAsync(TerritoryModel model, CancellationToken cancellationToken)
    {
        var filter = Builders<TerritoryModel>.Filter.Where(m => m.Id == model.Id && m.TenantId == model.TenantId);
        await _collection.ReplaceOneAsync(filter, model, cancellationToken: cancellationToken);
    }
}
