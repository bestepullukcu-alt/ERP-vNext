using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Repositories;

// MOD-0290-FU04 S4: the batch reads behind the product-master-bundle v1.1 contract (bulk read, validate, stockable
// search). Read-only and tenant-bounded; each record kind keeps its own repository's "live" rule.
public interface IProductMasterReadStore
{
    Task<IReadOnlyList<Item>> GetItemsByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Lsku>> GetLskusByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinishedGood>> GetFinishedGoodsByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    // S4 FIX1 (R4): the stockable search runs in the store — filters, the product scope, order and the page window are
    // the database's; only the window's rows come back, projected. Every source answers its own first Window rows in
    // canonical-code order and its total; the caller merges the sources and cuts the page.
    Task<ProductMasterStockablePage> SearchStockableAsync(
        ProductMasterStockableSearch search, CancellationToken cancellationToken = default);

    // OD-1: the lot / serial flags of a Global Product's non-retired SKUs (GSKUs; LSKUs and Finished Goods of every live
    // GSKU of the product), projected.
    Task<ProductMasterProductFlags> GetLiveSkuFlagsOfGlobalProductAsync(
        Guid globalProductId, CancellationToken cancellationToken = default);
}

public sealed record ProductMasterProductFlags(IReadOnlyList<bool?> GskuLots, IReadOnlyList<bool?> ChildSerials);

/// <param name="Query">Code prefix OR a part of the name, both case-insensitive; null = no text filter.</param>
/// <param name="ItemStatuses">The item statuses wanted; a PendingActivation item is wanted when its activation kind is
/// (<paramref name="PendingFirstActivation"/> / <paramref name="PendingReactivation"/>) — §16.3.</param>
/// <param name="ProductScope">Null: no product rows (the caller's scope shows none). Unrestricted: every product row
/// of the tenant (preparation). Otherwise the candidates the enforced scope reads.</param>
/// <param name="Window">How many rows each source answers (page × pageSize).</param>
public sealed record ProductMasterStockableSearch(
    string? Query,
    bool Items,
    bool Gskus,
    bool Lskus,
    bool FinishedGoods,
    ItemMaterialType? MaterialType,
    IReadOnlyCollection<ItemLifecycleStatus> ItemStatuses,
    bool PendingFirstActivation,
    bool PendingReactivation,
    IReadOnlyCollection<ProductIdentityLifecycleStatus> ProductStatuses,
    ProductMasterProductScope? ProductScope,
    int Window);

public sealed record ProductMasterProductScope(
    bool Unrestricted,
    IReadOnlyCollection<Guid> CandidateLegalEntityIds,
    DateTimeOffset ServerNowUtc);

public enum ProductMasterRowKind { Item, Gsku, Lsku, FinishedGood }

/// <summary>One projected row: an item (its own fields) or a product SKU (its pack / market and its product's name).</summary>
public sealed record ProductMasterStockableRow(
    ProductMasterRowKind Kind,
    Guid SkuId,
    Guid ItemId,
    string CanonicalCode,
    string? ItemName,
    ItemMaterialType? MaterialType,
    ItemLifecycleStatus? ItemStatus,
    ItemActivationKind? ActivationKind,
    ProductIdentityLifecycleStatus? ProductStatus,
    string? ProductName,
    decimal? PackQuantity,
    string? PackUomCode,
    string? MarketCode,
    string? BaseUomCode,
    bool? LotControlled,
    bool? SerialControlled);

/// <param name="Rows">Each source's first Window rows, in canonical-code order within the source.</param>
/// <param name="Total">Every matching row of every source.</param>
/// <param name="Unreadable">Rows of the window the store matched but could not read (skipped, never answered
/// half-read): their kind and id only — never their content.</param>
public sealed record ProductMasterStockablePage(
    IReadOnlyList<ProductMasterStockableRow> Rows, long Total, IReadOnlyList<ProductMasterUnreadableRow> Unreadable);

// S4 FIX3 (R1): an unreadable row keeps its place in the merged order (its stored code when that much is readable), so
// the page window counts it like any other row and no readable row slides out of every page.
// S4 FIX4: its place is where the store's sort put it — CodeBracket follows BSON's type order (0: missing / null /
// number, 1: text — every readable row, 2: object, array of those, binary, bool, date …), then CanonicalCode inside it.
public sealed record ProductMasterUnreadableRow(
    ProductMasterRowKind Kind, string Id, string? CanonicalCode = null, int CodeBracket = ProductMasterCodeBracket.Text);

/// <summary>S4 FIX4 — the BSON type order of a stored canonical code, as the store's $sort places it.
/// BL-603 — <see cref="First"/> (MinKey and the legacy undefined, before missing / null and every other value; measured on the
/// real store) and <see cref="Last"/> (MaxKey, after every other value).</summary>
public static class ProductMasterCodeBracket
{
    public const int First = -1;
    public const int BeforeText = 0;
    public const int Text = 1;
    public const int AfterText = 2;
    public const int Last = 3;
}
