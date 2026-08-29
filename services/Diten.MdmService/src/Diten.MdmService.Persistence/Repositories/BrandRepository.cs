using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class BrandRepository : RepositoryBase<Brand>, IBrandRepository
{
    public BrandRepository(IMongoDatabase database, ITenantContext tenantContext)
        : base(database, tenantContext, "mdm_brands")
    {
        EnsureIndexes();
    }

    public async Task<bool> ExistsByCodeAsync(string brandCode, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        // TenantFilter already scopes to the tenant. Archived rows are intentionally NOT excluded: BrandCode is
        // permanently reserved, so an archived code can never be reused (FU01 §3).
        var filter = Builders<Brand>.Filter.And(
            TenantFilter,
            Builders<Brand>.Filter.Eq(x => x.BrandCode, brandCode));

        if (excludeId.HasValue)
        {
            filter &= Builders<Brand>.Filter.Ne(x => x.Id, excludeId.Value);
        }

        return await Collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Brand>> GetAllAsync(CancellationToken cancellationToken = default)
        => await Collection.Find(TenantFilter)
            .SortBy(x => x.BrandName)
            .ToListAsync(cancellationToken);

    private void EnsureIndexes()
    {
        // Plain unique index — NO partial filter. A partial filter would have to express "not archived", and
        // MongoDB rejects `$ne`/`$not` inside partialFilterExpression, which crash-loops the service at startup.
        // Permanently reserving the code sidesteps the whole problem.
        var codeIndex = new CreateIndexModel<Brand>(
            Builders<Brand>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.BrandCode),
            new CreateIndexOptions { Unique = true, Name = "ux_mdm_brands_tenant_code" });

        // Listing/filtering support. EffectiveFrom and EffectiveTo are DateTimeOffset (stored as BSON arrays)
        // and are deliberately NOT indexed together — two parallel arrays in one index/sort raise
        // "cannot sort with keys that are parallel arrays".
        var listIndex = new CreateIndexModel<Brand>(
            Builders<Brand>.IndexKeys
                .Ascending(x => x.TenantId)
                .Ascending(x => x.IsArchived)
                .Ascending(x => x.BrandStatus),
            new CreateIndexOptions { Name = "ix_mdm_brands_tenant_archived_status" });

        Collection.Indexes.CreateMany([codeIndex, listIndex]);
    }
}
