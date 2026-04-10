using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class SkuRepository : RepositoryBase<Sku>, ISkuRepository
{
    public SkuRepository(IMongoDatabase database, ITenantContext tenantContext)
        : base(database, tenantContext, "skus")
    {
        var indexKeys = Builders<Sku>.IndexKeys
            .Ascending(x => x.TenantId)
            .Ascending(x => x.Code)
            .Ascending(x => x.IsDeleted);
        Collection.Indexes.CreateOne(new CreateIndexModel<Sku>(indexKeys, new CreateIndexOptions { Unique = true }));
    }

    public async Task<Sku> CreateAsync(Sku entity, CancellationToken cancellationToken = default)
    {
        return await InsertAsync(entity, cancellationToken);
    }

    public async Task<bool> UpdateAsync(Sku entity, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Sku>.Filter.And(
            TenantFilter,
            Builders<Sku>.Filter.Eq(x => x.Id, entity.Id));

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.TenantId = TenantContext.TenantId;
        var result = await Collection.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<Sku?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await FindByIdAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<Sku>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await Collection.Find(TenantFilter).SortBy(x => x.Code).ToListAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Sku>.Filter.And(
            TenantFilter,
            Builders<Sku>.Filter.Eq(x => x.Id, id));

        var update = Builders<Sku>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow);

        await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task<int> BulkDeleteAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
        {
            return 0;
        }

        var filter = Builders<Sku>.Filter.And(
            TenantFilter,
            Builders<Sku>.Filter.In(x => x.Id, idList));

        var update = Builders<Sku>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow);

        var result = await Collection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
        return (int)result.ModifiedCount;
    }

    public async Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Sku>.Filter.And(
            TenantFilter,
            Builders<Sku>.Filter.Eq(x => x.Code, code));

        if (excludeId.HasValue)
        {
            filter &= Builders<Sku>.Filter.Ne(x => x.Id, excludeId.Value);
        }

        return await Collection.Find(filter).AnyAsync(cancellationToken);
    }
}
