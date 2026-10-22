using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Application.Features.Traceability.Ports;

/// <summary>
/// PRODUCT-MASTER-BUNDLE v1 <c>getSku</c> as MOD-0174 needs it: the SKU's D-SKU-LINK item and its GTIN (K-B). Model A
/// only, HTTP only, fail-closed: unreachable / undeclared answer → <c>ProductMasterUnavailableException</c>; unknown SKU
/// (404) → null. <see cref="TraceSku.Gtin"/> is null while MDM returns <c>gtin: null</c> (MOD-0290 D-1) — the code reads
/// it as if it were always present, so nothing changes when MDM starts sending it.
/// The ledger's <c>IProductMasterClient</c> is not widened for this (its record carries no GTIN and is shared code).
/// </summary>
public interface ITraceSkuDirectory
{
    Task<TraceSku?> GetSkuAsync(Guid skuId, SkuLevel level, CancellationToken cancellationToken = default);
}

/// <remarks>
/// WP-0173-PM1: <c>LotControlled</c> / <c>SerialControlled</c> are the SKU-level flags of PRODUCT-MASTER v1.1 (null = unknown).
/// WP-0174-ITEM: <c>Stockable</c> (null = not stated; only an explicit false refuses a new lot).
/// WP-0176-F1 (PRODUCT-MASTER 1.1 <c>SkuRef</c> <c>:729-732</c>, days): <c>ShelfLifeDays</c>, <c>RetestDays</c>,
/// <c>MinRemainingShelfLifeDays</c> — absent or null = null ("Tanımsız"), NEVER 0 (KF-C: a null minimum is not applied).
/// </remarks>
public sealed record TraceSku(Guid SkuId, SkuLevel Level, Guid ItemId, string? Gtin, bool? LotControlled = null, bool? SerialControlled = null,
    bool? Stockable = null, int? ShelfLifeDays = null, int? RetestDays = null, int? MinRemainingShelfLifeDays = null);
