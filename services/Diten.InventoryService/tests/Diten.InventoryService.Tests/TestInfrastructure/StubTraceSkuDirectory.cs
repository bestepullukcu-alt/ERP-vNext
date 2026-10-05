using System.Collections.Concurrent;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Application.Features.Traceability.Ports;
using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// In-process PRODUCT-MASTER <c>getSku</c> for the MOD-0174 API tests: a SKU answers its D-SKU-LINK item and its GTIN
/// (null = MDM's current <c>gtin: null</c>, MOD-0290 D-1). Unknown → null (404). <see cref="MarkUnavailable"/> makes a
/// SKU fail like an unreachable upstream. Keyed by fresh per-test ids, so parallel tests never meet.
/// </summary>
public sealed class StubTraceSkuDirectory : ITraceSkuDirectory
{
    private readonly ConcurrentDictionary<(Guid, SkuLevel), TraceSku> _skus = new();
    private readonly ConcurrentDictionary<Guid, bool> _down = new();

    public void Add(Guid skuId, SkuLevel level, Guid itemId, string? gtin) => _skus[(skuId, level)] = new TraceSku(skuId, level, itemId, gtin);

    public void MarkUnavailable(Guid skuId) => _down[skuId] = true;

    public Task<TraceSku?> GetSkuAsync(Guid skuId, SkuLevel level, CancellationToken cancellationToken = default)
    {
        if (_down.ContainsKey(skuId)) throw new ProductMasterUnavailableException("stub: PRODUCT-MASTER unreachable");
        return Task.FromResult(_skus.TryGetValue((skuId, level), out var sku) ? sku : null);
    }
}
