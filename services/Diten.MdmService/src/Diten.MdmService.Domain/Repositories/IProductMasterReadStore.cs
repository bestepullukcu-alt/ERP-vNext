using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

// MOD-0290-FU04 S4: the batch reads behind the product-master-bundle v1.1 contract (bulk read, validate, stockable
// search). Read-only and tenant-bounded; each record kind keeps its own repository's "live" rule.
public interface IProductMasterReadStore
{
    Task<IReadOnlyList<Item>> GetItemsByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Lsku>> GetLskusByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinishedGood>> GetFinishedGoodsByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    // The rows that may be stockable: items and GSKUs / Finished Goods that store Stockable = true, and the LSKUs of
    // those GSKUs (an LSKU never stores it, O-3). Whether a row IS stockable (completeness) is the reader's rule.
    Task<ProductMasterStockableCandidates> GetStockableCandidatesAsync(CancellationToken cancellationToken = default);

    // OD-1: the Global Product's non-retired SKUs (every GSKU under its revisions, their LSKUs and Finished Goods), for
    // the deprecated v1.0 lot / serial fields of getProduct.
    Task<ProductMasterProductSkus> GetLiveSkusOfGlobalProductAsync(Guid globalProductId, CancellationToken cancellationToken = default);
}

public sealed record ProductMasterProductSkus(
    IReadOnlyList<Gsku> Gskus,
    IReadOnlyList<Lsku> Lskus,
    IReadOnlyList<FinishedGood> FinishedGoods);

public sealed record ProductMasterStockableCandidates(
    IReadOnlyList<Item> Items,
    IReadOnlyList<Gsku> Gskus,
    IReadOnlyList<Lsku> Lskus,
    IReadOnlyList<FinishedGood> FinishedGoods);
