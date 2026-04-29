using Diten.DevEnablementService.Domain.Entities;
using Diten.DevEnablementService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.DevEnablementService.Persistence.Repositories;

public sealed class GoldenReferenceCompactRepository : IGoldenReferenceCompactRepository
{
    private readonly IMongoCollection<GoldenReferenceCompact> _collection;
    private readonly Guid _tenantId;

    public GoldenReferenceCompactRepository(IMongoDatabase database, Application.Common.ITenantContext tenantContext)
    {
        _collection = database.GetCollection<GoldenReferenceCompact>("golden_reference_compact");
        _tenantId = tenantContext.TenantId;
    }

    public async Task<IReadOnlyList<GoldenReferenceCompact>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _collection.Find(TenantFilter()).SortBy(x => x.Code).ToListAsync(cancellationToken);
    }

    public async Task<GoldenReferenceCompact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<GoldenReferenceCompact>.Filter.And(
            TenantFilter(),
            Builders<GoldenReferenceCompact>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<GoldenReferenceCompact> CreateAsync(GoldenReferenceCompact entity, CancellationToken cancellationToken = default)
    {
        entity.TenantId = _tenantId;
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.IsDeleted = false;
        await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
        return entity;
    }

    public async Task<bool> UpdateAsync(GoldenReferenceCompact entity, CancellationToken cancellationToken = default)
    {
        entity.TenantId = _tenantId;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<GoldenReferenceCompact>.Filter.And(
            TenantFilter(),
            Builders<GoldenReferenceCompact>.Filter.Eq(x => x.Id, entity.Id));
        var result = await _collection.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<GoldenReferenceCompact>.Filter.And(
            TenantFilter(),
            Builders<GoldenReferenceCompact>.Filter.Eq(x => x.Id, id));

        var update = Builders<GoldenReferenceCompact>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<int> BulkDeleteAsync(List<Guid> ids, CancellationToken cancellationToken = default)
    {
        var filter = Builders<GoldenReferenceCompact>.Filter.And(
            TenantFilter(),
            Builders<GoldenReferenceCompact>.Filter.In(x => x.Id, ids));

        var update = Builders<GoldenReferenceCompact>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        var result = await _collection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
        return (int)result.ModifiedCount;
    }

    private FilterDefinition<GoldenReferenceCompact> TenantFilter()
    {
        return Builders<GoldenReferenceCompact>.Filter.And(
            Builders<GoldenReferenceCompact>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId),
            Builders<GoldenReferenceCompact>.Filter.Eq(x => x.IsDeleted, false));
    }

}
