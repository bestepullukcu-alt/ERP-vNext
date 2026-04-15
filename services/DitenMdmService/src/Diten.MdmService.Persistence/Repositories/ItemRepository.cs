using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class ItemRepository : RepositoryBase<Item>, IItemRepository
{
    private readonly IMongoCollection<ItemAttributeValue> _attributeValues;
    private readonly IMongoCollection<ItemVariant> _variants;

    public ItemRepository(IMongoDatabase database, ITenantContext tenantContext)
        : base(database, tenantContext, "items")
    {
        _attributeValues = database.GetCollection<ItemAttributeValue>("item_attribute_values");
        _variants = database.GetCollection<ItemVariant>("item_variants");

        var indexKeys = Builders<Item>.IndexKeys
            .Ascending(x => x.TenantId)
            .Ascending(x => x.Code)
            .Ascending(x => x.IsDeleted);
        Collection.Indexes.CreateOne(new CreateIndexModel<Item>(indexKeys, new CreateIndexOptions { Unique = true }));
    }

    // Override GetAllAsync to apply default sort by Code
    public override async Task<IReadOnlyList<Item>> GetAllAsync(CancellationToken ct = default)
    {
        return await Collection.Find(TenantFilter).SortBy(x => x.Code).ToListAsync(ct);
    }

    // Override DeleteAsync to cascade soft-delete to child collections
    public override async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await base.DeleteAsync(id, ct);
        await SoftDeleteManyAsync(
            _attributeValues,
            Builders<ItemAttributeValue>.Filter.And(TenantFilterFor<ItemAttributeValue>(), Builders<ItemAttributeValue>.Filter.Eq(x => x.ItemId, id)),
            ct);
        await SoftDeleteManyAsync(
            _variants,
            Builders<ItemVariant>.Filter.And(TenantFilterFor<ItemVariant>(), Builders<ItemVariant>.Filter.Eq(x => x.ItemId, id)),
            ct);
    }

    public async Task<int> BulkDeleteAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return 0;

        var filter = Builders<Item>.Filter.And(
            TenantFilter,
            Builders<Item>.Filter.In(x => x.Id, idList));
        var itemResult = await SoftDeleteManyAsync(Collection, filter, ct);

        var attributeFilter = Builders<ItemAttributeValue>.Filter.And(
            TenantFilterFor<ItemAttributeValue>(),
            Builders<ItemAttributeValue>.Filter.In(x => x.ItemId, idList));
        await SoftDeleteManyAsync(_attributeValues, attributeFilter, ct);

        var variantFilter = Builders<ItemVariant>.Filter.And(
            TenantFilterFor<ItemVariant>(),
            Builders<ItemVariant>.Filter.In(x => x.ItemId, idList));
        await SoftDeleteManyAsync(_variants, variantFilter, ct);

        return (int)itemResult.ModifiedCount;
    }

    public async Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var filter = Builders<Item>.Filter.And(
            TenantFilter,
            Builders<Item>.Filter.Eq(x => x.Code, code));

        if (excludeId.HasValue)
        {
            filter &= Builders<Item>.Filter.Ne(x => x.Id, excludeId.Value);
        }

        return await Collection.Find(filter).AnyAsync(ct);
    }

    public async Task ReplaceAttributeValuesAsync(Guid itemId, IEnumerable<ItemAttributeValue> values, CancellationToken ct = default)
    {
        var deleteFilter = Builders<ItemAttributeValue>.Filter.And(
            TenantFilterFor<ItemAttributeValue>(),
            Builders<ItemAttributeValue>.Filter.Eq(x => x.ItemId, itemId));
        await _attributeValues.DeleteManyAsync(deleteFilter, ct);

        var valueList = values.ToList();
        if (valueList.Count == 0) return;

        foreach (var value in valueList)
        {
            value.Id = value.Id == Guid.Empty ? Guid.NewGuid() : value.Id;
            value.ItemId = itemId;
            value.TenantId = TenantContext.TenantId;
            value.IsDeleted = false;
        }

        await _attributeValues.InsertManyAsync(valueList, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<ItemAttributeValue>> GetAttributeValuesAsync(Guid itemId, CancellationToken ct = default)
    {
        var filter = Builders<ItemAttributeValue>.Filter.And(
            TenantFilterFor<ItemAttributeValue>(),
            Builders<ItemAttributeValue>.Filter.Eq(x => x.ItemId, itemId));
        return await _attributeValues.Find(filter).ToListAsync(ct);
    }

    public async Task ReplaceVariantsAsync(Guid itemId, IEnumerable<ItemVariant> variants, CancellationToken ct = default)
    {
        var deleteFilter = Builders<ItemVariant>.Filter.And(
            TenantFilterFor<ItemVariant>(),
            Builders<ItemVariant>.Filter.Eq(x => x.ItemId, itemId));
        await _variants.DeleteManyAsync(deleteFilter, ct);

        var variantList = variants.ToList();
        if (variantList.Count == 0) return;

        foreach (var variant in variantList)
        {
            variant.Id = variant.Id == Guid.Empty ? Guid.NewGuid() : variant.Id;
            variant.ItemId = itemId;
            variant.TenantId = TenantContext.TenantId;
            variant.IsDeleted = false;
        }

        await _variants.InsertManyAsync(variantList, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<ItemVariant>> GetVariantsAsync(Guid itemId, CancellationToken ct = default)
    {
        var filter = Builders<ItemVariant>.Filter.And(
            TenantFilterFor<ItemVariant>(),
            Builders<ItemVariant>.Filter.Eq(x => x.ItemId, itemId));
        return await _variants.Find(filter).SortBy(x => x.Code).ToListAsync(ct);
    }

    private FilterDefinition<TEntity> TenantFilterFor<TEntity>() where TEntity : EntityBase
    {
        return Builders<TEntity>.Filter.And(
            Builders<TEntity>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<TEntity>.Filter.Eq(x => x.IsDeleted, false));
    }

    private async Task<UpdateResult> SoftDeleteManyAsync<TEntity>(
        IMongoCollection<TEntity> collection,
        FilterDefinition<TEntity> filter,
        CancellationToken ct)
        where TEntity : EntityBase
    {
        var update = Builders<TEntity>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow);
        return await collection.UpdateManyAsync(filter, update, cancellationToken: ct);
    }
}
