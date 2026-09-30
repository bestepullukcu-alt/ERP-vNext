using System.Collections.Concurrent;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// In-process PRODUCT-MASTER for API tests (WP E5: handler tests do not depend on Prism). Behaves like the frozen
/// contract: unknown ids are 404 (null), a sku carries its D-SKU-LINK itemId. State is keyed by fresh per-test ids, so
/// parallel test classes never see each other's items. <see cref="MarkUnavailable"/> makes every call touching that
/// item or sku fail like an unreachable upstream.
/// </summary>
public sealed class StubProductMaster : IProductMasterClient
{
    private readonly ConcurrentDictionary<Guid, ProductReference> _products = new();
    private readonly ConcurrentDictionary<Guid, SkuReference> _skus = new();
    private readonly ConcurrentDictionary<Guid, SkuUomInfo> _uoms = new();
    private readonly ConcurrentDictionary<Guid, bool> _down = new();

    public void AddItem(Guid itemId, bool lotControlled = false, bool serialControlled = false, string baseUomId = "EA")
        => _products[itemId] = new ProductReference(itemId.ToString(), "Active", baseUomId, lotControlled, serialControlled);

    public void AddSku(Guid skuId, SkuLevel level, Guid linkedItemId, string baseUomId = "EA", params UomConversionRule[] conversions)
    {
        _skus[skuId] = new SkuReference(skuId.ToString(), WireNames.ToWire(level), linkedItemId.ToString());
        _uoms[skuId] = new SkuUomInfo(baseUomId, conversions);
    }

    public void MarkUnavailable(params Guid[] ids)
    {
        foreach (var id in ids) _down[id] = true;
    }

    public Task<IReadOnlyList<ProductRefValidation>> ValidateRefsAsync(IReadOnlyList<ProductRefQuery> refs, CancellationToken cancellationToken = default)
    {
        ThrowIfDown(refs.Select(r => r.ItemId ?? r.SkuId ?? Guid.Empty));
        IReadOnlyList<ProductRefValidation> results = refs.Select(r => r.ItemId.HasValue
                ? new ProductRefValidation(r.ItemId.ToString(), null, null, _products.ContainsKey(r.ItemId.Value))
                : new ProductRefValidation(null, r.SkuId.ToString(), r.SkuLevel.HasValue ? WireNames.ToWire(r.SkuLevel.Value) : null,
                    r.SkuId.HasValue && _skus.TryGetValue(r.SkuId.Value, out var sku)
                    && string.Equals(sku.SkuLevel, r.SkuLevel.HasValue ? WireNames.ToWire(r.SkuLevel.Value) : null, StringComparison.Ordinal)))
            .ToList();
        return Task.FromResult(results);
    }

    public Task<ProductReference?> GetProductAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        ThrowIfDown([itemId]);
        return Task.FromResult(_products.TryGetValue(itemId, out var p) ? p : null);
    }

    public Task<SkuReference?> GetSkuAsync(Guid skuId, SkuLevel level, CancellationToken cancellationToken = default)
    {
        ThrowIfDown([skuId]);
        return Task.FromResult(_skus.TryGetValue(skuId, out var s) && s.SkuLevel == WireNames.ToWire(level) ? s : null);
    }

    public Task<SkuUomInfo?> GetSkuUomAsync(Guid skuId, CancellationToken cancellationToken = default)
    {
        ThrowIfDown([skuId]);
        return Task.FromResult(_uoms.TryGetValue(skuId, out var u) ? u : null);
    }

    private void ThrowIfDown(IEnumerable<Guid> ids)
    {
        if (ids.Any(_down.ContainsKey))
        {
            throw new ProductMasterUnavailableException("stub: PRODUCT-MASTER unreachable");
        }
    }
}
