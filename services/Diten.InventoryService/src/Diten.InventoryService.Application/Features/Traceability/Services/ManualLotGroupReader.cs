using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Traceability.Repositories;

namespace Diten.InventoryService.Application.Features.Traceability.Services;

/// <summary>The migratable typed groups and what was left out (CT K4-4).</summary>
/// <param name="Groups">Groups of LOT-CONTROLLED SKUs, in matcher order.</param>
/// <param name="ExcludedItemIds">Items none of whose typed groups migrate: not lot-controlled, or unknown to PRODUCT-MASTER.</param>
/// <param name="NeedsSerials">Per included SKU (id + level): whether a new lot of it is Lot + Serial (tracking mode of a new lot).</param>
public sealed record ManualLotGroupSnapshot(
    IReadOnlyList<TypedLotGroupStock> Groups,
    IReadOnlySet<Guid> ExcludedItemIds,
    IReadOnlyDictionary<(Guid SkuId, SkuLevel Level), bool> NeedsSerials);

/// <summary>
/// WP-0174-P4 — the typed groups the migration works on (listManualLotGroups and the commit read the SAME set): every typed
/// group of the tenant whose SKU PRODUCT-MASTER calls lot-controlled. A typed text on a SKU that is not lot-controlled
/// is not a lot (pack §12 <c>LOT_NOT_ALLOWED</c>): it gets no group, no link, no re-key (CT K4-4). PRODUCT-MASTER
/// unreachable → <see cref="ProductMasterUnavailableException"/> (the contract's 503), nothing read further.
/// <para>WP-0173-PM1 (PRODUCT-MASTER v1.1, OD-1 / OD-5): the lot flag is read per SKU from <c>getSku</c>, no longer from the
/// product. An UNKNOWN lot flag stops the read with the same 503 — a group is never left out because its flag is unknown
/// (that would be "unknown = not lot-controlled"). The tracking mode of a new lot over-requires under doubt
/// (<see cref="ProductMasterStockRules.LotNeedsSerials"/>); at GSKU it follows the product's serial flag (OD-10).</para>
/// </summary>
public sealed class ManualLotGroupReader
{
    private readonly IManualLotMigrationRepository _store;
    private readonly IProductMasterClient _products;

    public ManualLotGroupReader(IManualLotMigrationRepository store, IProductMasterClient products)
    {
        _store = store;
        _products = products;
    }

    public async Task<ManualLotGroupSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        var all = await _store.ListTypedGroupsAsync(cancellationToken);
        var needsSerials = new Dictionary<(Guid, SkuLevel), bool>();
        var excludedSkus = new HashSet<(Guid, SkuLevel)>();
        foreach (var (skuId, level, itemId) in all.Select(g => (g.Group.SkuId, g.Group.SkuLevel, g.Group.ItemId)).Distinct())
        {
            var sku = await _products.GetSkuAsync(skuId, level, cancellationToken);
            if (sku is null || !Guid.TryParse(sku.ItemId, out var linked) || linked != itemId)
            {
                excludedSkus.Add((skuId, level));
                continue;
            }

            if (sku.LotControlled is not { } lotControlled)
            {
                throw new ProductMasterUnavailableException(
                    $"PRODUCT-MASTER does not state whether SKU {skuId:D} ({WireNames.ToWire(level)}) is lot-controlled.");
            }

            if (!lotControlled)
            {
                excludedSkus.Add((skuId, level));
                continue;
            }

            var productSerial = level == SkuLevel.Gsku ? (await _products.GetProductAsync(itemId, cancellationToken))?.SerialControlled : null;
            needsSerials[(skuId, level)] = ProductMasterStockRules.LotNeedsSerials(level, sku.SerialControlled, productSerial);
        }

        var groups = all.Where(g => needsSerials.ContainsKey((g.Group.SkuId, g.Group.SkuLevel))).ToList();
        var included = groups.Select(g => g.Group.ItemId).ToHashSet();
        var excluded = all.Select(g => g.Group.ItemId).Where(i => !included.Contains(i)).ToHashSet();
        return new ManualLotGroupSnapshot(groups, excluded, needsSerials);
    }
}
