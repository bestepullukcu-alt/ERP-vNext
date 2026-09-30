using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Infrastructure.Upstream;

/// <summary>
/// Typed HTTP client for PRODUCT-MASTER-BUNDLE v1 (frozen, MOD-0290). Paths are the contract's, relative to the
/// configured base URL: <c>POST validate</c>, <c>GET products/{itemId}</c>, <c>GET skus/{skuId}?level=</c>,
/// <c>GET skus/{skuId}/uom</c>. Model A only; never the MDM database (pack §2).
/// </summary>
public sealed class ProductMasterHttpClient : IProductMasterClient
{
    private readonly HttpClient _http;

    public ProductMasterHttpClient(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<ProductRefValidation>> ValidateRefsAsync(IReadOnlyList<ProductRefQuery> refs, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            refs = refs.Select(r => new Dictionary<string, string?>
            {
                ["itemId"] = r.ItemId?.ToString(),
                ["skuId"] = r.SkuId?.ToString(),
                ["skuLevel"] = r.SkuLevel.HasValue ? WireNames.ToWire(r.SkuLevel.Value) : null
            }.Where(p => p.Value is not null).ToDictionary(p => p.Key, p => p.Value)).ToArray()
        };

        var results = await UpstreamHttp.SendAsync(
            _http,
            () => new HttpRequestMessage(HttpMethod.Post, "validate") { Content = JsonContent.Create(body) },
            notFoundIsAbsent: false,
            root => UpstreamHttp.RequiredArray(root, "results").EnumerateArray()
                .Select(r => new ProductRefValidation(
                    UpstreamHttp.OptionalString(r, "itemId"),
                    UpstreamHttp.OptionalString(r, "skuId"),
                    UpstreamHttp.OptionalString(r, "skuLevel"),
                    UpstreamHttp.OptionalBool(r, "exists")))
                .ToList(),
            Unavailable,
            cancellationToken);
        return results!;
    }

    public Task<ProductReference?> GetProductAsync(Guid itemId, CancellationToken cancellationToken = default)
        => UpstreamHttp.SendAsync(
            _http,
            () => new HttpRequestMessage(HttpMethod.Get, $"products/{itemId}"),
            notFoundIsAbsent: true,
            root => new ProductReference(
                UpstreamHttp.RequiredString(root, "itemId"),
                UpstreamHttp.OptionalString(root, "lifecycleStatus"),
                UpstreamHttp.OptionalString(root, "baseUomId"),
                UpstreamHttp.OptionalBool(root, "lotControlled"),
                UpstreamHttp.OptionalBool(root, "serialControlled")),
            Unavailable,
            cancellationToken);

    public Task<SkuReference?> GetSkuAsync(Guid skuId, SkuLevel level, CancellationToken cancellationToken = default)
        => UpstreamHttp.SendAsync(
            _http,
            () => new HttpRequestMessage(HttpMethod.Get, $"skus/{skuId}?level={Uri.EscapeDataString(WireNames.ToWire(level))}"),
            notFoundIsAbsent: true,
            root => new SkuReference(
                UpstreamHttp.RequiredString(root, "skuId"),
                UpstreamHttp.OptionalString(root, "skuLevel"),
                UpstreamHttp.OptionalString(root, "itemId")),
            Unavailable,
            cancellationToken);

    /// <summary>
    /// The contract declares no 404 for <c>getSkuUom</c>; a 404 is still read as "no conversion data" (→ 422
    /// <c>UOM_CONVERSION_MISSING</c>) rather than an outage, since the SKU itself was already confirmed.
    /// </summary>
    public Task<SkuUomInfo?> GetSkuUomAsync(Guid skuId, CancellationToken cancellationToken = default)
        => UpstreamHttp.SendAsync(
            _http,
            () => new HttpRequestMessage(HttpMethod.Get, $"skus/{skuId}/uom"),
            notFoundIsAbsent: true,
            root => new SkuUomInfo(
                UpstreamHttp.RequiredString(root, "baseUomId"),
                root.TryGetProperty("conversions", out var conversions) && conversions.ValueKind == JsonValueKind.Array
                    ? conversions.EnumerateArray().Select(ParseConversion).Where(c => c is not null).Select(c => c!).ToList()
                    : []),
            Unavailable,
            cancellationToken);

    /// <summary>A conversion with an unreadable factor is dropped (never guessed); a missing path then yields 422.</summary>
    private static UomConversionRule? ParseConversion(JsonElement c)
    {
        var from = UpstreamHttp.OptionalString(c, "fromUom");
        var to = UpstreamHttp.OptionalString(c, "toUom");
        if (from is null || to is null
            || !InventoryQuantity.TryParseContractDecimal(UpstreamHttp.OptionalString(c, "numerator"), out var numerator)
            || !InventoryQuantity.TryParseContractDecimal(UpstreamHttp.OptionalString(c, "denominator"), out var denominator))
        {
            return null;
        }

        return new UomConversionRule(from, to, numerator, denominator,
            ParseTime(UpstreamHttp.OptionalString(c, "effectiveFrom")),
            ParseTime(UpstreamHttp.OptionalString(c, "effectiveTo")));
    }

    private static DateTimeOffset? ParseTime(string? raw)
        => raw is not null && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value)
            ? value
            : null;

    private static Exception Unavailable(string message, Exception? inner) => new ProductMasterUnavailableException(message, inner);
}
