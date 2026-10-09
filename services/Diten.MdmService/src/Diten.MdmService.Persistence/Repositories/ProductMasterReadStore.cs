using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

// MOD-0290-FU04 S4: tenant-bounded batch reads for the product-master contract. The live rule of each kind is its own
// repository's: items and Finished Goods IsDeleted ≠ true (a legacy document without the field is live), GSKUs and
// LSKUs (and revisions) IsDeleted = false.
public sealed class ProductMasterReadStore : IProductMasterReadStore
{
    private readonly IMongoCollection<Item> _items;
    private readonly IMongoCollection<Gsku> _gskus;
    private readonly IMongoCollection<Lsku> _lskus;
    private readonly IMongoCollection<FinishedGood> _finishedGoods;
    private readonly IMongoCollection<ProductDefinitionRevision> _revisions;
    private readonly Guid _tenantId;

    public ProductMasterReadStore(IMongoDatabase database, ITenantContext tenantContext)
    {
        _items = database.GetCollection<Item>(ItemRepository.CollectionName);
        _gskus = database.GetCollection<Gsku>("mdm_gskus");
        _lskus = database.GetCollection<Lsku>("mdm_lskus");
        _finishedGoods = database.GetCollection<FinishedGood>("mdm_finished_goods");
        _revisions = database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions");
        _tenantId = tenantContext.TenantId;
    }

    private FilterDefinition<Item> LiveItems =>
        Builders<Item>.Filter.Eq(x => x.TenantId, _tenantId) & Builders<Item>.Filter.Ne(x => x.IsDeleted, true);

    private FilterDefinition<Gsku> LiveGskus =>
        Builders<Gsku>.Filter.Eq(x => x.TenantId, _tenantId) & Builders<Gsku>.Filter.Eq(x => x.IsDeleted, false);

    private FilterDefinition<Lsku> LiveLskus =>
        Builders<Lsku>.Filter.Eq(x => x.TenantId, _tenantId) & Builders<Lsku>.Filter.Eq(x => x.IsDeleted, false);

    private FilterDefinition<FinishedGood> LiveFinishedGoods =>
        Builders<FinishedGood>.Filter.Eq(x => x.TenantId, _tenantId)
        & Builders<FinishedGood>.Filter.Ne(x => x.IsDeleted, true);

    public async Task<IReadOnlyList<Item>> GetItemsByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        ids.Count == 0
            ? []
            : await _items.Find(LiveItems & Builders<Item>.Filter.In(x => x.Id, ids)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Lsku>> GetLskusByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        ids.Count == 0
            ? []
            : await _lskus.Find(LiveLskus & Builders<Lsku>.Filter.In(x => x.Id, ids)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FinishedGood>> GetFinishedGoodsByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        ids.Count == 0
            ? []
            : await _finishedGoods.Find(LiveFinishedGoods & Builders<FinishedGood>.Filter.In(x => x.Id, ids))
                .ToListAsync(cancellationToken);

    public async Task<ProductMasterStockableCandidates> GetStockableCandidatesAsync(
        CancellationToken cancellationToken = default)
    {
        var items = await _items.Find(LiveItems & Builders<Item>.Filter.Eq(x => x.Stockable, true))
            .ToListAsync(cancellationToken);
        var gskus = await _gskus.Find(LiveGskus & Builders<Gsku>.Filter.Eq(x => x.Stockable, true))
            .ToListAsync(cancellationToken);
        var gskuIds = gskus.Select(x => x.Id).ToArray();
        var lskus = gskuIds.Length == 0
            ? []
            : await _lskus.Find(LiveLskus & Builders<Lsku>.Filter.In(x => x.GskuId, gskuIds)).ToListAsync(cancellationToken);
        var finishedGoods = await _finishedGoods
            .Find(LiveFinishedGoods & Builders<FinishedGood>.Filter.Eq(x => x.Stockable, true))
            .ToListAsync(cancellationToken);
        return new(items, gskus, lskus, finishedGoods);
    }

    public async Task<ProductMasterProductSkus> GetLiveSkusOfGlobalProductAsync(
        Guid globalProductId, CancellationToken cancellationToken = default)
    {
        var revisionIds = await _revisions
            .Find(Builders<ProductDefinitionRevision>.Filter.Eq(x => x.TenantId, _tenantId)
                  & Builders<ProductDefinitionRevision>.Filter.Eq(x => x.IsDeleted, false)
                  & Builders<ProductDefinitionRevision>.Filter.Eq(x => x.GlobalProductId, globalProductId))
            .Project(x => x.Id)
            .ToListAsync(cancellationToken);
        if (revisionIds.Count == 0) return new([], [], []);
        var gskus = await _gskus.Find(LiveGskus
                & Builders<Gsku>.Filter.In(x => x.ProductDefinitionRevisionId, revisionIds)
                & Builders<Gsku>.Filter.Ne(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.Retired))
            .ToListAsync(cancellationToken);
        // The LSKUs and Finished Goods of every GSKU of the product (a retired GSKU's live children still count).
        var allGskuIds = await _gskus.Find(LiveGskus & Builders<Gsku>.Filter.In(x => x.ProductDefinitionRevisionId, revisionIds))
            .Project(x => x.Id)
            .ToListAsync(cancellationToken);
        var lskus = await _lskus.Find(LiveLskus
                & Builders<Lsku>.Filter.In(x => x.GskuId, allGskuIds)
                & Builders<Lsku>.Filter.Ne(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.Retired))
            .ToListAsync(cancellationToken);
        var finishedGoods = await _finishedGoods.Find(LiveFinishedGoods
                & Builders<FinishedGood>.Filter.In(x => x.GskuId, allGskuIds)
                & Builders<FinishedGood>.Filter.Ne(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.Retired))
            .ToListAsync(cancellationToken);
        return new(gskus, lskus, finishedGoods);
    }
}
