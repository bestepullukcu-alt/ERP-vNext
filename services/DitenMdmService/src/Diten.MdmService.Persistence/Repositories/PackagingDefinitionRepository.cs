using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class PackagingDefinitionRepository : RepositoryBase<PackagingDefinition>, IPackagingDefinitionRepository
{
    public PackagingDefinitionRepository(IMongoDatabase database, ITenantContext tenantContext)
        : base(database, tenantContext, "packaging_definitions")
    {
        var indexKeys = Builders<PackagingDefinition>.IndexKeys
            .Ascending(x => x.TenantId)
            .Ascending(x => x.Code)
            .Ascending(x => x.IsDeleted);
        Collection.Indexes.CreateOne(new CreateIndexModel<PackagingDefinition>(indexKeys, new CreateIndexOptions { Unique = true }));
    }

    // Override GetAllAsync to apply default sort by Code
    public override async Task<IReadOnlyList<PackagingDefinition>> GetAllAsync(CancellationToken ct = default)
    {
        return await Collection.Find(TenantFilter).SortBy(x => x.Code).ToListAsync(ct);
    }

    public async Task<int> BulkDeleteAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return 0;

        var filter = Builders<PackagingDefinition>.Filter.And(
            TenantFilter,
            Builders<PackagingDefinition>.Filter.In(x => x.Id, idList));

        var update = Builders<PackagingDefinition>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow);

        var result = await Collection.UpdateManyAsync(filter, update, cancellationToken: ct);
        return (int)result.ModifiedCount;
    }

    public async Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var filter = Builders<PackagingDefinition>.Filter.And(
            TenantFilter,
            Builders<PackagingDefinition>.Filter.Eq(x => x.Code, code));

        if (excludeId.HasValue)
        {
            filter &= Builders<PackagingDefinition>.Filter.Ne(x => x.Id, excludeId.Value);
        }

        return await Collection.Find(filter).AnyAsync(ct);
    }
}
