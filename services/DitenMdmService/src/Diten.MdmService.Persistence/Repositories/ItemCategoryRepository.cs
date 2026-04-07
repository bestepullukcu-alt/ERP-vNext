using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class ItemCategoryRepository : RepositoryBase<ItemCategory>, IItemCategoryRepository
{
    public ItemCategoryRepository(IMongoDatabase database, ITenantContext tenantContext)
        : base(database, tenantContext, "item_categories")
    {
        var indexKeys = Builders<ItemCategory>.IndexKeys
            .Ascending(x => x.TenantId)
            .Ascending(x => x.Code)
            .Ascending(x => x.IsDeleted);
        Collection.Indexes.CreateOne(new CreateIndexModel<ItemCategory>(indexKeys, new CreateIndexOptions { Unique = true }));
    }

    public async Task<ItemCategory> CreateAsync(ItemCategory entity, CancellationToken cancellationToken = default)
    {
        return await InsertAsync(entity, cancellationToken);
    }

    public async Task<bool> UpdateAsync(ItemCategory entity, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ItemCategory>.Filter.And(
            TenantFilter,
            Builders<ItemCategory>.Filter.Eq(x => x.Id, entity.Id));

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.TenantId = TenantContext.TenantId;
        var result = await Collection.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<ItemCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await FindByIdAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<ItemCategory>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await Collection.Find(TenantFilter).SortBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var update = Builders<ItemCategory>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow);
        await Collection.UpdateOneAsync(
            Builders<ItemCategory>.Filter.And(TenantFilter, Builders<ItemCategory>.Filter.Eq(x => x.Id, id)),
            update,
            cancellationToken: cancellationToken);
    }

    public async Task<int> BulkDeleteAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
        {
            return 0;
        }

        var filter = Builders<ItemCategory>.Filter.And(
            TenantFilter,
            Builders<ItemCategory>.Filter.In(x => x.Id, idList));

        var update = Builders<ItemCategory>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow);

        var result = await Collection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
        return (int)result.ModifiedCount;
    }

    public async Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ItemCategory>.Filter.And(
            TenantFilter,
            Builders<ItemCategory>.Filter.Eq(x => x.Code, code));

        if (excludeId.HasValue)
        {
            filter &= Builders<ItemCategory>.Filter.Ne(x => x.Id, excludeId.Value);
        }

        return await Collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<bool> WouldCreateCycleAsync(Guid categoryId, Guid? parentCategoryId, CancellationToken cancellationToken = default)
    {
        if (!parentCategoryId.HasValue)
        {
            return false;
        }

        var currentParentId = parentCategoryId;

        while (currentParentId.HasValue)
        {
            if (currentParentId.Value == categoryId)
            {
                return true;
            }

            var parent = await GetByIdAsync(currentParentId.Value, cancellationToken);
            currentParentId = parent?.ParentCategoryId;
        }

        return false;
    }
}
