namespace Diten.InventoryService.Application.Common.Upstream;

/// <summary>
/// The fields of PRODUCT-MASTER <c>SkuRef</c> (getSku) that MOD-0173 needs; <see cref="ItemId"/> is the D-SKU-LINK.
/// WP-0173-PM1 (v1.1, OD-1 / R-04): <c>getSku</c> is the AUTHORITATIVE read of the stock behaviour — <see cref="LotControlled"/>,
/// <see cref="SerialControlled"/> (null = unknown, or at GSKU "not applicable", OD-10) and <see cref="LifecycleStatus"/> (an
/// open enum: carried as text, decided by <c>ProductMasterStockRules</c>). WP-0174-ITEM: <see cref="Stockable"/> (null = not stated).
/// WP-0176-F1 (v1.1 <c>SkuRef</c>, days): <see cref="ShelfLifeDays"/>, <see cref="RetestDays"/>, <see cref="MinRemainingShelfLifeDays"/> —
/// absent or null = null ("Tanımsız"), never 0 (KF-C: a null minimum is not applied).
/// WP-MVP1-FIX-LIVE2 (additive): <see cref="CanonicalCode"/> — <c>SkuRef.canonicalCode</c>, read for display only (evidence packs);
/// absent = null, never invented.
/// </summary>
public sealed record SkuReference(
    string SkuId,
    string? SkuLevel,
    string? ItemId,
    string? LifecycleStatus = null,
    bool? LotControlled = null,
    bool? SerialControlled = null,
    bool? Stockable = null,
    int? ShelfLifeDays = null,
    int? RetestDays = null,
    int? MinRemainingShelfLifeDays = null,
    string? CanonicalCode = null);
