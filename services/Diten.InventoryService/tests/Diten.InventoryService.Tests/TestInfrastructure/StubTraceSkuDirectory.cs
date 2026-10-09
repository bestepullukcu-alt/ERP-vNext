using System.Collections.Concurrent;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Application.Features.Traceability.Ports;
using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// In-process PRODUCT-MASTER <c>getSku</c> for the MOD-0174 API tests: a SKU answers its D-SKU-LINK item and its GTIN
/// (null = MDM's current <c>gtin: null</c>, MOD-0290 D-1). Unknown → null (404). <see cref="MarkUnavailable"/> makes a
/// SKU fail like an unreachable upstream. Keyed by fresh per-test ids, so parallel tests never meet.
/// <para>WP-0173-PM1 (v1.1): the SKU also answers its lot / serial flags. Unless <see cref="SetFlags"/> says otherwise they
/// default to the item's flags in <see cref="Products"/> (serial null at Gsku, OD-10) — the meaning the tests had before.</para>
/// </summary>
public sealed class StubTraceSkuDirectory : ITraceSkuDirectory
{
    private readonly ConcurrentDictionary<(Guid, SkuLevel), TraceSku> _skus = new();
    private readonly ConcurrentDictionary<Guid, (bool? Lot, bool? Serial)> _flags = new();
    private readonly ConcurrentDictionary<Guid, bool> _down = new();
    private readonly ConcurrentDictionary<Guid, bool?> _stockable = new();

    /// <summary>WP-0174-ITEM: the SKU's published <c>stockable</c> (default: not stated = null).</summary>
    public void SetStockable(Guid skuId, bool? stockable) => _stockable[skuId] = stockable;

    /// <summary>The item flags the default SKU flags come from (the factory's <see cref="StubProductMaster"/>).</summary>
    public IProductMasterClient? Products { get; set; }

    public void Add(Guid skuId, SkuLevel level, Guid itemId, string? gtin) => _skus[(skuId, level)] = new TraceSku(skuId, level, itemId, gtin);

    /// <summary>The SKU's own flags (null = unknown), overriding the item-derived default.</summary>
    public void SetFlags(Guid skuId, bool? lotControlled, bool? serialControlled) => _flags[skuId] = (lotControlled, serialControlled);

    public void MarkUnavailable(Guid skuId) => _down[skuId] = true;

    public async Task<TraceSku?> GetSkuAsync(Guid skuId, SkuLevel level, CancellationToken cancellationToken = default)
    {
        if (_down.ContainsKey(skuId)) throw new ProductMasterUnavailableException("stub: PRODUCT-MASTER unreachable");
        if (!_skus.TryGetValue((skuId, level), out var sku)) return null;
        sku = sku with { Stockable = _stockable.TryGetValue(skuId, out var stockable) ? stockable : null };
        if (_flags.TryGetValue(skuId, out var own)) return sku with { LotControlled = own.Lot, SerialControlled = own.Serial };

        var item = Products is null ? null : await Products.GetProductAsync(sku.ItemId, cancellationToken);
        return sku with { LotControlled = item?.LotControlled, SerialControlled = level == SkuLevel.Gsku ? null : item?.SerialControlled };
    }
}
