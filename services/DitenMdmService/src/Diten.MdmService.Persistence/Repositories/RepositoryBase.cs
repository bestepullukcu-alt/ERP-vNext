using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

/// <summary>
/// Tenant-enforcing generic repository implementation.
/// All queries automatically filter by TenantId and IsDeleted=false.
/// </summary>
public class RepositoryBase<TEntity> : IRepository<TEntity> where TEntity : EntityBase
{
    protected readonly IMongoCollection<TEntity> Collection;
    protected readonly ITenantContext TenantContext;

    public RepositoryBase(IMongoDatabase database, ITenantContext tenantContext, string collectionName)
    {
        Collection = database.GetCollection<TEntity>(collectionName);
        TenantContext = tenantContext;
    }

    protected FilterDefinition<TEntity> TenantFilter =>
        Builders<TEntity>.Filter.And(
            Builders<TEntity>.Filter.Eq(e => e.TenantId, TenantContext.TenantId),
            Builders<TEntity>.Filter.Eq(e => e.IsDeleted, false)
        );

    public virtual async Task<TEntity> CreateAsync(TEntity entity, CancellationToken ct = default)
    {
        entity.TenantId = TenantContext.TenantId;
        await Collection.InsertOneAsync(entity, cancellationToken: ct);
        return entity;
    }

    public virtual async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var filter = Builders<TEntity>.Filter.And(
            TenantFilter,
            Builders<TEntity>.Filter.Eq(e => e.Id, id));
        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public virtual async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default)
    {
        return await Collection.Find(TenantFilter).ToListAsync(ct);
    }

    public virtual async Task<bool> UpdateAsync(TEntity entity, CancellationToken ct = default)
    {
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.TenantId = TenantContext.TenantId;

        var filter = Builders<TEntity>.Filter.And(
            TenantFilter,
            Builders<TEntity>.Filter.Eq(e => e.Id, entity.Id));
        var result = await Collection.ReplaceOneAsync(filter, entity, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }

    public virtual async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var filter = Builders<TEntity>.Filter.And(
            TenantFilter,
            Builders<TEntity>.Filter.Eq(e => e.Id, id));

        var update = Builders<TEntity>.Update
            .Set(e => e.IsDeleted, true)
            .Set(e => e.DeletedAt, DateTimeOffset.UtcNow);

        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    public virtual async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        var filter = Builders<TEntity>.Filter.And(
            TenantFilter,
            Builders<TEntity>.Filter.Eq(e => e.Id, id));
        return await Collection.Find(filter).AnyAsync(ct);
    }

    public virtual async Task<int> BulkDeleteAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return 0;

        var filter = Builders<TEntity>.Filter.And(
            TenantFilter,
            Builders<TEntity>.Filter.In(x => x.Id, idList));

        var update = Builders<TEntity>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow);

        var result = await Collection.UpdateManyAsync(filter, update, cancellationToken: ct);
        return (int)result.ModifiedCount;
    }

    public virtual async Task<long> CountAsync(CancellationToken ct = default)
    {
        return await Collection.CountDocumentsAsync(TenantFilter, cancellationToken: ct);
    }

    public virtual async Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var filter = Builders<TEntity>.Filter.And(
            TenantFilter,
            Builders<TEntity>.Filter.Eq("Code", code));

        if (excludeId.HasValue)
        {
            filter &= Builders<TEntity>.Filter.Ne(x => x.Id, excludeId.Value);
        }

        return await Collection.Find(filter).AnyAsync(ct);
    }
}
