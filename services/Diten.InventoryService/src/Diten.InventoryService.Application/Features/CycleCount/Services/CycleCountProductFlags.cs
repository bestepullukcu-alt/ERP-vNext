using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Application.Features.CycleCounting.Services;

/// <summary>
/// Pack §4.2 <c>SerialControlled</c> ("Release / Add'de PRODUCT-MASTER'dan yazılır", Q-10 = A): the stock behaviour of a SKU with the ledger's
/// own rule (<see cref="ProductMasterStockRules"/>; Lsku / FinishedGood / Item read the SKU flag, Gsku the product's). Read OUTSIDE the
/// release's snapshot transaction — no HTTP call is made while it is open.
/// </summary>
public sealed class CycleCountProductFlags
{
    /// <summary>A SKU's stock behaviour as a found line needs it.</summary>
    public sealed record CycleCountSkuBehaviour(bool LotControlled, bool SerialControlled, string BaseUomId);

    private readonly IProductMasterClient _products;

    public CycleCountProductFlags(IProductMasterClient products) => _products = products;

    /// <summary>The serial flag of each key, or the refusal (503 unreachable, 422 incomplete / not stockable).</summary>
    public async Task<(IReadOnlyDictionary<(Guid ItemId, Guid SkuId, SkuLevel Level), bool>? Flags, Response<T>? Refusal)> SerialFlagsAsync<T>(
        IEnumerable<(Guid ItemId, Guid SkuId, SkuLevel Level)> keys, CancellationToken ct)
    {
        var flags = new Dictionary<(Guid, Guid, SkuLevel), bool>();
        try
        {
            foreach (var key in keys.Distinct())
            {
                var sku = await _products.GetSkuAsync(key.SkuId, key.Level, ct);
                if (sku is null) return (null, Incomplete<T>(key.SkuId));
                var productSerial = key.Level == SkuLevel.Gsku ? (await _products.GetProductAsync(key.ItemId, ct))?.SerialControlled : null;
                var behaviour = ProductMasterStockRules.Behaviour(key.Level, sku.LotControlled, sku.SerialControlled, productSerial);
                if (behaviour.Refusal is StockBehaviourRefusal.LevelNotStockable)
                {
                    return (null, CycleCountSupport.Fail<T>(InventoryErrorCodes.SkuLevelNotStockable, "PRODUCT-MASTER does not allow stock of this SKU at this level.", 422,
                        new Dictionary<string, object?> { ["skuId"] = key.SkuId.ToString("D"), ["skuLevel"] = WireNames.ToWire(key.Level) }));
                }

                if (behaviour.Refusal is not null) return (null, Incomplete<T>(key.SkuId));
                flags[key] = behaviour.SerialControlled;
            }
        }
        catch (ProductMasterUnavailableException)
        {
            return (null, CycleCountSupport.Fail<T>(InventoryErrorCodes.ProductMasterUnavailable, "PRODUCT-MASTER could not be reached; nothing was written.", 503));
        }

        return (flags, null);
    }

    /// <summary>
    /// DL-2 found line (pack §4.2, §8.9, §12): the SKU's lot / serial behaviour and base UoM from PRODUCT-MASTER, with the ledger's own rule.
    /// Unknown SKU → 422 <c>UNKNOWN_ITEM</c>; another item's SKU → 422 <c>SKU_ITEM_MISMATCH</c>; unreachable → 503; incomplete → 422.
    /// </summary>
    public async Task<(CycleCountSkuBehaviour? Behaviour, Response<T>? Refusal)> LineBehaviourAsync<T>(Guid itemId, Guid skuId, SkuLevel level, CancellationToken ct)
    {
        try
        {
            var sku = await _products.GetSkuAsync(skuId, level, ct);
            if (sku is null)
                return (null, CycleCountSupport.Fail<T>(InventoryErrorCodes.UnknownItem, "PRODUCT-MASTER does not know this SKU at this level.", 422,
                    new Dictionary<string, object?> { ["skuId"] = skuId.ToString("D"), ["skuLevel"] = WireNames.ToWire(level) }));
            if (sku.ItemId is { } owner && !string.Equals(owner, itemId.ToString("D"), StringComparison.OrdinalIgnoreCase))
                return (null, CycleCountSupport.Fail<T>(InventoryErrorCodes.SkuItemMismatch, "The SKU does not belong to this item.", 422,
                    new Dictionary<string, object?> { ["skuId"] = skuId.ToString("D"), ["itemId"] = itemId.ToString("D") }));

            var productSerial = level == SkuLevel.Gsku ? (await _products.GetProductAsync(itemId, ct))?.SerialControlled : null;
            var behaviour = ProductMasterStockRules.Behaviour(level, sku.LotControlled, sku.SerialControlled, productSerial);
            if (behaviour.Refusal is StockBehaviourRefusal.LevelNotStockable)
            {
                return (null, CycleCountSupport.Fail<T>(InventoryErrorCodes.SkuLevelNotStockable, "PRODUCT-MASTER does not allow stock of this SKU at this level.", 422,
                    new Dictionary<string, object?> { ["skuId"] = skuId.ToString("D"), ["skuLevel"] = WireNames.ToWire(level) }));
            }

            if (behaviour.Refusal is not null) return (null, Incomplete<T>(skuId));
            var uom = await _products.GetSkuUomAsync(skuId, ct);
            if (uom is null || string.IsNullOrWhiteSpace(uom.BaseUomId)) return (null, Incomplete<T>(skuId));
            return (new CycleCountSkuBehaviour(behaviour.LotControlled, behaviour.SerialControlled, uom.BaseUomId), null);
        }
        catch (ProductMasterUnavailableException)
        {
            return (null, CycleCountSupport.Fail<T>(InventoryErrorCodes.ProductMasterUnavailable, "PRODUCT-MASTER could not be reached; nothing was written.", 503));
        }
    }

    private static Response<T> Incomplete<T>(Guid skuId)
        => CycleCountSupport.Fail<T>(InventoryErrorCodes.ProductMasterDataIncomplete, "PRODUCT-MASTER does not state the lot / serial flag this SKU needs.", 422,
            new Dictionary<string, object?> { ["skuId"] = skuId.ToString("D") });
}
