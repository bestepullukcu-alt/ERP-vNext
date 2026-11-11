using System.Net;
using System.Text.Json;
using Diten.SafetyStockService.Application;
using Microsoft.AspNetCore.Http;

namespace Diten.SafetyStockService.Infrastructure;

public sealed class ProductMasterBaseUomReader(HttpClient client, IHttpContextAccessor context) : ISkuBaseUomReader
{
    public async Task<string?> ReadAsync(Guid skuId, CancellationToken ct)
    {
        var bearer = context.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(bearer) || !bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A caller token is required for SKU validation.");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/product-master/skus/{skuId:D}");
        request.Headers.TryAddWithoutValidation("Authorization", bearer);
        using var response = await client.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (response.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException("PRODUCT-MASTER is unavailable.");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = json.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("skuId", out var id) || !Guid.TryParse(id.GetString(), out var returned) ||
            returned != skuId ||
            !root.TryGetProperty("lifecycleStatus", out var status) || status.GetString() != "Active" ||
            !root.TryGetProperty("baseUomId", out var uom) || uom.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(uom.GetString()))
            return null;
        return uom.GetString();
    }
}
