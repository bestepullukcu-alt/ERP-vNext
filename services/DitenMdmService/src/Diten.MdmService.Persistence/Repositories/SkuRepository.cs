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

    // Override GetAllAsync to apply default sort by Code
    public override async Task<IReadOnlyList<Sku>> GetAllAsync(CancellationToken ct = default)
    {
        return await Collection.Find(TenantFilter).SortBy(x => x.Code).ToListAsync(ct);
    }

    public async Task<int> BulkDeleteAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return 0;

        var filter = Builders<Sku>.Filter.And(
            TenantFilter,
            Builders<Sku>.Filter.In(x => x.Id, idList));

        var update = Builders<Sku>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow);

        var result = await Collection.UpdateManyAsync(filter, update, cancellationToken: ct);
        return (int)result.ModifiedCount;
    }

    public async Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var filter = Builders<Sku>.Filter.And(
            TenantFilter,
            Builders<Sku>.Filter.Eq(x => x.Code, code));

        if (excludeId.HasValue)
        {
            filter &= Builders<Sku>.Filter.Ne(x => x.Id, excludeId.Value);
        }

        return await Collection.Find(filter).AnyAsync(ct);
    }
}
