using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class ProductRepository : RepositoryBase<Product>, IProductRepository
{
    public ProductRepository(IMongoDatabase database, ITenantContext tenantContext)
        : base(database, tenantContext, "mdm_products")
    {
        EnsureIndexes();
    }

    public async Task<bool> ExistsByCodeAsync(string productCode, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        // Archived rows included on purpose — ProductCode is permanently reserved (FU01 §4).
        var filter = Builders<Product>.Filter.And(
            TenantFilter,
            Builders<Product>.Filter.Eq(x => x.ProductCode, productCode));

        if (excludeId.HasValue)
        {
            filter &= Builders<Product>.Filter.Ne(x => x.Id, excludeId.Value);
        }

        return await Collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
        => await Collection.Find(TenantFilter)
            .SortBy(x => x.ProductName)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Product>> GetByBrandAsync(Guid brandId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Product>.Filter.And(
            TenantFilter,
            Builders<Product>.Filter.Eq(x => x.BrandId, brandId));

        return await Collection.Find(filter).SortBy(x => x.ProductName).ToListAsync(cancellationToken);
    }

    private void EnsureIndexes()
    {
        // Same reasoning as BrandRepository: plain unique index, no partial filter, no `$ne`.
        var codeIndex = new CreateIndexModel<Product>(
            Builders<Product>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ProductCode),
            new CreateIndexOptions { Unique = true, Name = "ux_mdm_products_tenant_code" });

        var brandIndex = new CreateIndexModel<Product>(
            Builders<Product>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.BrandId),
            new CreateIndexOptions { Name = "ix_mdm_products_tenant_brand" });

        // No DateTimeOffset field participates here — see BrandRepository for why they are kept out of indexes.
        var listIndex = new CreateIndexModel<Product>(
            Builders<Product>.IndexKeys
                .Ascending(x => x.TenantId)
                .Ascending(x => x.IsArchived)
                .Ascending(x => x.ProductStatus),
            new CreateIndexOptions { Name = "ix_mdm_products_tenant_archived_status" });

        Collection.Indexes.CreateMany([codeIndex, brandIndex, listIndex]);
    }
}
