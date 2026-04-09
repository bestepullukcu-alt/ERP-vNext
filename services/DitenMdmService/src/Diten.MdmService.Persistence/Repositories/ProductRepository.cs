using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class ProductRepository : RepositoryBase<Product>, IProductRepository
{
    public ProductRepository(IMongoDatabase database, ITenantContext tenantContext)
        : base(database, tenantContext, "products")
    {
        var indexKeys = Builders<Product>.IndexKeys
            .Ascending(x => x.TenantId)
            .Ascending(x => x.Code)
            .Ascending(x => x.IsDeleted);
        Collection.Indexes.CreateOne(new CreateIndexModel<Product>(indexKeys, new CreateIndexOptions { Unique = true }));
    }

    public async Task EnsureSeedDataAsync(CancellationToken cancellationToken = default)
    {
        // Not using TenantFilter here because we need to check even deleted records.
        // Also checking by Id globally because Id is a unique index for the entire collection.
        var seedProducts = ProductSeedData.BuildProducts();
        var seedIds = seedProducts.Select(p => p.Id).ToList();
        var seedCodes = seedProducts.Select(p => p.Code).ToList();

        var existingEntities = await Collection.Find(
            Builders<Product>.Filter.Or(
                Builders<Product>.Filter.In(x => x.Id, seedIds),
                Builders<Product>.Filter.Eq(x => x.TenantId, TenantContext.TenantId) // Only check codes for current tenant
            ))
            .Project(x => new { x.Id, x.Code })
            .ToListAsync(cancellationToken);

        var existingIdSet = existingEntities.Select(x => x.Id).ToHashSet();
        var existingCodeSet = existingEntities
            .Where(x => x.Code != null)
            .Select(x => x.Code.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingProducts = seedProducts
            .Where(x => !existingIdSet.Contains(x.Id) && !existingCodeSet.Contains(x.Code))
            .ToList();

        if (missingProducts.Count == 0)
        {
            return;
        }

        foreach (var product in missingProducts)
        {
            await InsertAsync(product, cancellationToken);
        }
    }

    public async Task<Product> CreateAsync(Product entity, CancellationToken cancellationToken = default)
    {
        return await InsertAsync(entity, cancellationToken);
    }

    public async Task<bool> UpdateAsync(Product entity, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Product>.Filter.And(
            TenantFilter,
            Builders<Product>.Filter.Eq(x => x.Id, entity.Id));

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.TenantId = TenantContext.TenantId;
        var result = await Collection.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await FindByIdAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await Collection.Find(TenantFilter).SortBy(x => x.Code).ToListAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Product>.Filter.And(
            TenantFilter,
            Builders<Product>.Filter.Eq(x => x.Id, id));

        var update = Builders<Product>.Update
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

        var filter = Builders<Product>.Filter.And(
            TenantFilter,
            Builders<Product>.Filter.In(x => x.Id, idList));

        var update = Builders<Product>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow);

        var result = await Collection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
        return (int)result.ModifiedCount;
    }

    public async Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Product>.Filter.And(
            TenantFilter,
            Builders<Product>.Filter.Eq(x => x.Code, code));

        if (excludeId.HasValue)
        {
            filter &= Builders<Product>.Filter.Ne(x => x.Id, excludeId.Value);
        }

        return await Collection.Find(filter).AnyAsync(cancellationToken);
    }
}
