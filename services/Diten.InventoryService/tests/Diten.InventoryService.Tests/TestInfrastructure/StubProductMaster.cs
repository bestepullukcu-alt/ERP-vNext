using System.Collections.Concurrent;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// In-process PRODUCT-MASTER for API tests (WP E5: handler tests do not depend on Prism). Behaves like the contract: unknown
/// ids are 404 (null), a sku carries its D-SKU-LINK itemId. State is keyed by fresh per-test ids, so parallel test classes
/// never see each other's items. <see cref="MarkUnavailable"/> makes every call touching that item or sku fail like an
/// unreachable upstream.
/// <para>WP-0173-PM1 — v1.1 shapes: <c>getSku</c> carries the authoritative lot / serial flags and the lifecycle status;
/// <c>validateRefs</c> rows carry <c>lifecycleStatus</c>. So that the tests written before v1.1 keep their meaning, a SKU's flags
/// DEFAULT to its item's (<see cref="AddItem"/>): lot = the item's lot flag; serial = the item's serial flag at Lsku /
/// FinishedGood / Item, null at Gsku ("not applicable", OD-10). <see cref="SetSkuFlags"/> and <see cref="SetLifecycle"/>
/// set anything else, including null (unknown).</para>
/// </summary>
public sealed class StubProductMaster : IProductMasterClient
{
    private readonly ConcurrentDictionary<Guid, ProductReference> _products = new();
    private readonly ConcurrentDictionary<Guid, SkuReference> _skus = new();
    private readonly ConcurrentDictionary<Guid, (bool? Lot, bool? Serial)> _skuFlags = new();
    private readonly ConcurrentDictionary<Guid, string?> _lifecycle = new();
    private readonly ConcurrentDictionary<Guid, bool?> _stockable = new();

    /// <summary>WP-0174-ITEM: the SKU's published <c>stockable</c> (default: not stated = null).</summary>
    public void SetStockable(Guid skuId, bool? stockable) => _stockable[skuId] = stockable;
    private readonly ConcurrentDictionary<Guid, SkuUomInfo> _uoms = new();
    private readonly ConcurrentDictionary<Guid, bool> _down = new();

    public void AddItem(Guid itemId, bool lotControlled = false, bool serialControlled = false, string baseUomId = "EA")
        => _products[itemId] = new ProductReference(itemId.ToString(), "Active", baseUomId, lotControlled, serialControlled);

    public void AddSku(Guid skuId, SkuLevel level, Guid linkedItemId, string baseUomId = "EA", params UomConversionRule[] conversions)
    {
        _skus[skuId] = new SkuReference(skuId.ToString(), WireNames.ToWire(level), linkedItemId.ToString());
        _uoms[skuId] = new SkuUomInfo(baseUomId, conversions);
    }

    /// <summary>A material item stocked as its own SKU (<c>skuId == itemId</c>, level Item; Q-1).</summary>
    public void AddMaterialItem(Guid itemId, bool lotControlled = false, string baseUomId = "EA")
    {
        AddItem(itemId, lotControlled, serialControlled: false, baseUomId);
        AddSku(itemId, SkuLevel.Item, itemId, baseUomId);
    }

    /// <summary>The SKU's own flags, overriding the item-derived default (null = unknown).</summary>
    public void SetSkuFlags(Guid skuId, bool? lotControlled, bool? serialControlled) => _skuFlags[skuId] = (lotControlled, serialControlled);

    /// <summary>The published lifecycle of an item or a SKU (null = not published at all).</summary>
    public void SetLifecycle(Guid id, string? status) => _lifecycle[id] = status;

    public void MarkUnavailable(params Guid[] ids)
    {
        foreach (var id in ids) _down[id] = true;
    }

    public Task<IReadOnlyList<ProductRefValidation>> ValidateRefsAsync(IReadOnlyList<ProductRefQuery> refs, CancellationToken cancellationToken = default)
    {
        ThrowIfDown(refs.Select(r => r.ItemId ?? r.SkuId ?? Guid.Empty));
        IReadOnlyList<ProductRefValidation> results = refs.Select(r =>
            {
                if (r.ItemId.HasValue)
                {
                    var found = _products.ContainsKey(r.ItemId.Value);
                    return new ProductRefValidation(r.ItemId.ToString(), null, null, found, found ? Lifecycle(r.ItemId.Value) : null);
                }

                var level = r.SkuLevel.HasValue ? WireNames.ToWire(r.SkuLevel.Value) : null;
                var exists = r.SkuId.HasValue && _skus.TryGetValue(r.SkuId.Value, out var sku) && string.Equals(sku.SkuLevel, level, StringComparison.Ordinal);
                return new ProductRefValidation(null, r.SkuId.ToString(), level, exists, exists ? Lifecycle(r.SkuId!.Value) : null);
            })
            .ToList();
        return Task.FromResult(results);
    }

    public Task<ProductReference?> GetProductAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        ThrowIfDown([itemId]);
        return Task.FromResult(_products.TryGetValue(itemId, out var p) ? p with { LifecycleStatus = Lifecycle(itemId) } : null);
    }

    public Task<SkuReference?> GetSkuAsync(Guid skuId, SkuLevel level, CancellationToken cancellationToken = default)
    {
        ThrowIfDown([skuId]);
        if (!_skus.TryGetValue(skuId, out var s) || s.SkuLevel != WireNames.ToWire(level))
        {
            return Task.FromResult<SkuReference?>(null);
        }

        var (lot, serial) = Flags(skuId, level, s);
        return Task.FromResult<SkuReference?>(s with
        {
            LifecycleStatus = Lifecycle(skuId), LotControlled = lot, SerialControlled = serial,
            Stockable = _stockable.TryGetValue(skuId, out var stockable) ? stockable : null
        });
    }

    public Task<SkuUomInfo?> GetSkuUomAsync(Guid skuId, CancellationToken cancellationToken = default)
    {
        ThrowIfDown([skuId]);
        return Task.FromResult(_uoms.TryGetValue(skuId, out var u) ? u : null);
    }

    private (bool? Lot, bool? Serial) Flags(Guid skuId, SkuLevel level, SkuReference sku)
    {
        if (_skuFlags.TryGetValue(skuId, out var own)) return own;
        var item = Guid.TryParse(sku.ItemId, out var id) && _products.TryGetValue(id, out var p) ? p : null;
        return (item?.LotControlled, level == SkuLevel.Gsku ? null : item?.SerialControlled);
    }

    /// <summary>"Active" unless set: an item's status also applies to its SKUs unless the SKU has its own.</summary>
    private string? Lifecycle(Guid id)
    {
        if (_lifecycle.TryGetValue(id, out var own)) return own;
        if (_skus.TryGetValue(id, out var sku) && Guid.TryParse(sku.ItemId, out var item) && _lifecycle.TryGetValue(item, out var inherited)) return inherited;
        return "Active";
    }

    private void ThrowIfDown(IEnumerable<Guid> ids)
    {
        if (ids.Any(_down.ContainsKey))
        {
            throw new ProductMasterUnavailableException("stub: PRODUCT-MASTER unreachable");
        }
    }
}
