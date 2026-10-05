using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Application.Features.Traceability.Ports;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Infrastructure.Upstream;

namespace Diten.InventoryService.Infrastructure.Traceability;

/// <summary>
/// PRODUCT-MASTER-BUNDLE v1 <c>GET skus/{skuId}?level=</c> (frozen, MOD-0290, Model A only) read for MOD-0174: the
/// SKU's D-SKU-LINK <c>itemId</c> and its <c>gtin</c> (nullable in the contract — MOD-0290 D-1). Same base URL, caller
/// headers and fail-closed rules as the ledger's <see cref="ProductMasterHttpClient"/>: 404 → null, anything outside the
/// contract → <see cref="ProductMasterUnavailableException"/>.
/// </summary>
public sealed class TraceSkuDirectoryHttpClient : ITraceSkuDirectory
{
    private readonly HttpClient _http;

    public TraceSkuDirectoryHttpClient(HttpClient http) => _http = http;

    public Task<TraceSku?> GetSkuAsync(Guid skuId, SkuLevel level, CancellationToken cancellationToken = default)
        => UpstreamHttp.SendAsync(
            _http,
            () => new HttpRequestMessage(HttpMethod.Get, $"skus/{skuId}?level={Uri.EscapeDataString(WireNames.ToWire(level))}"),
            notFoundIsAbsent: true,
            root =>
            {
                var itemId = UpstreamHttp.RequiredString(root, "itemId");
                if (!Guid.TryParse(itemId, out var item))
                {
                    throw new FormatException("getSku answered an itemId that is not a UUID.");
                }

                return new TraceSku(skuId, level, item, UpstreamHttp.OptionalString(root, "gtin"));
            },
            (message, inner) => new ProductMasterUnavailableException(message, inner),
            cancellationToken);
}
