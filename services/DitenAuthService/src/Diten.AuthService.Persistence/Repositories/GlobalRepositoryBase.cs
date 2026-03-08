using Diten.AuthService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Repositories;

public abstract class GlobalRepositoryBase<TEntity> where TEntity : GlobalEntityBase
{
    protected readonly IMongoCollection<TEntity> Collection;

    protected GlobalRepositoryBase(IMongoDatabase database, string collectionName)
    {
        Collection = database.GetCollection<TEntity>(collectionName);
    }

    protected FilterDefinition<TEntity> IsDeletedFilter => 
        Builders<TEntity>.Filter.Eq(e => e.IsDeleted, false);

    protected async Task<TEntity?> FindByIdAsync(Guid id, CancellationToken ct)
    {
        var filter = Builders<TEntity>.Filter.And(
            IsDeletedFilter,
            Builders<TEntity>.Filter.Eq(e => e.Id, id));

        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    protected async Task<IReadOnlyList<TEntity>> FindAllAsync(CancellationToken ct)
    {
        return await Collection.Find(IsDeletedFilter).ToListAsync(ct);
    }

    protected async Task<TEntity> InsertOneAsync(TEntity entity, CancellationToken ct)
    {
        await Collection.InsertOneAsync(entity, cancellationToken: ct);
        return entity;
    }

    protected async Task ReplaceOneAsync(TEntity entity, CancellationToken ct)
    {
        var filter = Builders<TEntity>.Filter.And(
            IsDeletedFilter,
            Builders<TEntity>.Filter.Eq(e => e.Id, entity.Id));

        await Collection.ReplaceOneAsync(filter, entity, cancellationToken: ct);
    }
}
