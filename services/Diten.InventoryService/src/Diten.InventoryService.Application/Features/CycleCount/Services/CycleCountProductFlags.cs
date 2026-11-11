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

    private static Response<T> Incomplete<T>(Guid skuId)
        => CycleCountSupport.Fail<T>(InventoryErrorCodes.ProductMasterDataIncomplete, "PRODUCT-MASTER does not state the lot / serial flag this SKU needs.", 422,
            new Dictionary<string, object?> { ["skuId"] = skuId.ToString("D") });
}
