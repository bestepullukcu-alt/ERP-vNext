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

    // Override GetAllAsync to apply default sort by Name
    public override async Task<IReadOnlyList<ItemCategory>> GetAllAsync(CancellationToken ct = default)
    {
        return await Collection.Find(TenantFilter).SortBy(x => x.Name).ToListAsync(ct);
    }

    public async Task<int> BulkDeleteAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return 0;

        var filter = Builders<ItemCategory>.Filter.And(
            TenantFilter,
            Builders<ItemCategory>.Filter.In(x => x.Id, idList));

        var update = Builders<ItemCategory>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow);

        var result = await Collection.UpdateManyAsync(filter, update, cancellationToken: ct);
        return (int)result.ModifiedCount;
    }

    public async Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var filter = Builders<ItemCategory>.Filter.And(
            TenantFilter,
            Builders<ItemCategory>.Filter.Eq(x => x.Code, code));

        if (excludeId.HasValue)
        {
            filter &= Builders<ItemCategory>.Filter.Ne(x => x.Id, excludeId.Value);
        }

        return await Collection.Find(filter).AnyAsync(ct);
    }

    public async Task<bool> WouldCreateCycleAsync(Guid categoryId, Guid? parentCategoryId, CancellationToken ct = default)
    {
        if (!parentCategoryId.HasValue) return false;

        var currentParentId = parentCategoryId;
        while (currentParentId.HasValue)
        {
            if (currentParentId.Value == categoryId) return true;
            var parent = await GetByIdAsync(currentParentId.Value, ct);
            currentParentId = parent?.ParentCategoryId;
        }

        return false;
    }
}
