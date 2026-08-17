using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public abstract class RepositoryBase<TEntity> : IRepository<TEntity>
    where TEntity : EntityBase
{
    protected readonly IMongoCollection<TEntity> Collection;
    protected readonly Guid TenantId;

    protected RepositoryBase(IMongoDatabase database, ITenantContext tenantContext, string collectionName)
    {
        Collection = database.GetCollection<TEntity>(collectionName);
        TenantId = tenantContext.TenantId;
    }

    public virtual async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<TEntity>.Filter.And(
            TenantFilter,
            Builders<TEntity>.Filter.Eq(x => x.Id, id));

        return await Collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public virtual async Task<TEntity> CreateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        entity.TenantId = TenantId;
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.IsDeleted = false;

        await Collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
        return entity;
    }

    public virtual async Task<bool> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        entity.TenantId = TenantId;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.Version++;

        var filter = Builders<TEntity>.Filter.And(
            TenantFilter,
            Builders<TEntity>.Filter.Eq(x => x.Id, entity.Id));

        var result = await Collection.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public virtual async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<TEntity>.Filter.And(
            TenantFilter,
            Builders<TEntity>.Filter.Eq(x => x.Id, id));

        var update = Builders<TEntity>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    protected FilterDefinition<TEntity> TenantFilter => Builders<TEntity>.Filter.And(
        Builders<TEntity>.Filter.Eq(x => x.TenantId, TenantId),
        Builders<TEntity>.Filter.Eq(x => x.IsDeleted, false));
}
