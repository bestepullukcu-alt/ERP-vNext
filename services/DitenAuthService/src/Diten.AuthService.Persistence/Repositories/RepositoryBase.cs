using Diten.AuthService.Application.Common;
using Diten.AuthService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Repositories;

public abstract class RepositoryBase<TEntity> where TEntity : EntityBase
{
    protected readonly IMongoCollection<TEntity> Collection;
    protected readonly ITenantContext TenantContext;

    protected RepositoryBase(IMongoDatabase database, ITenantContext tenantContext, string collectionName)
    {
        Collection = database.GetCollection<TEntity>(collectionName);
        TenantContext = tenantContext;
    }

    protected FilterDefinition<TEntity> TenantFilter => 
        Builders<TEntity>.Filter.And(
            Builders<TEntity>.Filter.Eq(e => e.TenantId, TenantContext.TenantId),
            Builders<TEntity>.Filter.Eq(e => e.IsDeleted, false)
        );

    protected async Task<TEntity?> FindByIdAsync(Guid id, CancellationToken ct)
    {
        var filter = Builders<TEntity>.Filter.And(
            TenantFilter,
            Builders<TEntity>.Filter.Eq(e => e.Id, id));

        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    protected async Task<IReadOnlyList<TEntity>> FindAllAsync(CancellationToken ct)
    {
        return await Collection.Find(TenantFilter).ToListAsync(ct);
    }

    protected async Task<TEntity> InsertOneAsync(TEntity entity, CancellationToken ct)
    {
        await Collection.InsertOneAsync(entity, cancellationToken: ct);
        return entity;
    }

    protected async Task<TEntity> ReplaceOneAsync(TEntity entity, CancellationToken ct)
    {
        var filter = Builders<TEntity>.Filter.And(
            TenantFilter,
            Builders<TEntity>.Filter.Eq(e => e.Id, entity.Id));

        await Collection.ReplaceOneAsync(filter, entity, cancellationToken: ct);
        return entity;
    }

    protected async Task UpdateOneAsync(FilterDefinition<TEntity> filter, UpdateDefinition<TEntity> update, CancellationToken ct)
    {
        var combinedFilter = Builders<TEntity>.Filter.And(TenantFilter, filter);
        await Collection.UpdateOneAsync(combinedFilter, update, cancellationToken: ct);
    }
}
