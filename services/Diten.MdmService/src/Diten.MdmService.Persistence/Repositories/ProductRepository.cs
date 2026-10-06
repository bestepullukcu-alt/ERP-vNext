using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class ProductRepository : RepositoryBase<Product>, IProductRepository
{
    private readonly IMongoClient _client;
    private readonly ExternalReferenceReservations _reservations;
    private readonly BrandProductStoreOptions _storeOptions;
    private readonly ILogger<ProductRepository>? _logger;

    public ProductRepository(
        IMongoDatabase database,
        ITenantContext tenantContext,
        BrandProductStoreOptions? storeOptions = null,
        ILogger<ProductRepository>? logger = null)
        : base(database, tenantContext, "mdm_products")
    {
        _client = database.Client;
        _reservations = new ExternalReferenceReservations(database, TenantId);
        _storeOptions = storeOptions ?? new BrandProductStoreOptions();
        _logger = logger;
        IndexOnce.Ensure(database, "mdm_products", EnsureIndexes);
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

    public Task<BrandWriteOutcome> CreateWithReservationsAsync(Product product, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        product.TenantId = TenantId;
        product.CreatedAt = now;
        product.UpdatedAt = now;
        product.IsDeleted = false;
        return MasterDataAtomicWrite.RunAsync(_client, _storeOptions, _logger, async session =>
        {
            try
            {
                if (session is null)
                    await Collection.InsertOneAsync(product, cancellationToken: cancellationToken);
                else
                    await Collection.InsertOneAsync(session, product, cancellationToken: cancellationToken);
            }
            catch (Exception exception) when (MasterDataAtomicWrite.IsDuplicateKey(exception))
            {
                return BrandWriteOutcome.DuplicateCode;
            }

            return await _reservations.TryReserveAsync(session, ExternalReferenceReservation.RecordTypeProduct,
                product.Id, product.ProductCode, product.ExternalReferences ?? [], cancellationToken)
                ? BrandWriteOutcome.Written
                : BrandWriteOutcome.ReferenceInUse;
        }, cancellationToken);
    }

    public Task<BrandWriteOutcome> UpdateWithReservationsAsync(
        Product product, IReadOnlyList<BrandProductExternalReference> added, IReadOnlyList<BrandProductExternalReference> removed,
        CancellationToken cancellationToken = default)
    {
        product.TenantId = TenantId;
        product.UpdatedAt = DateTimeOffset.UtcNow;
        product.Version++;
        var filter = TenantFilter & Builders<Product>.Filter.Eq(x => x.Id, product.Id);
        return MasterDataAtomicWrite.RunAsync(_client, _storeOptions, _logger, async session =>
        {
            var result = session is null
                ? await Collection.ReplaceOneAsync(filter, product, cancellationToken: cancellationToken)
                : await Collection.ReplaceOneAsync(session, filter, product, cancellationToken: cancellationToken);
            if (result.MatchedCount != 1)
            {
                return BrandWriteOutcome.Stale;
            }

            await _reservations.ReleaseAsync(session, ExternalReferenceReservation.RecordTypeProduct, product.Id, removed, cancellationToken);
            return await _reservations.TryReserveAsync(session, ExternalReferenceReservation.RecordTypeProduct,
                product.Id, product.ProductCode, added, cancellationToken)
                ? BrandWriteOutcome.Written
                : BrandWriteOutcome.ReferenceInUse;
        }, cancellationToken);
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
