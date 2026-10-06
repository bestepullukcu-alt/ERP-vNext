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

    public async Task<bool> TryCreateAsync(Product product, CancellationToken cancellationToken = default)
    {
        try
        {
            await CreateAsync(product, cancellationToken);
            return true;
        }
        catch (Exception exception) when (BrandRepository.IsDuplicateKey(exception))
        {
            // WP-MDM-BRAND-ACCEPT-01 FIX1 (item 9) — the unique index answered a create that raced past the pre-check.
            return false;
        }
    }

    public async Task<Product?> FindByExternalReferenceAsync(
        string sourceSystem, string externalId, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        var filter = TenantFilter & Builders<Product>.Filter.ElemMatch(x => x.ExternalReferences,
            Builders<BrandProductExternalReference>.Filter.Regex(x => x.SourceSystem, BrandRepository.ExactIgnoringCase(sourceSystem))
            & Builders<BrandProductExternalReference>.Filter.Regex(x => x.ExternalId, BrandRepository.ExactIgnoringCase(externalId)));
        if (excludeId is { } id)
        {
            filter &= Builders<Product>.Filter.Ne(x => x.Id, id);
        }

        return await Collection.Find(filter).SortBy(x => x.ProductCode).Limit(1).FirstOrDefaultAsync(cancellationToken);
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
