using System.Globalization;
using System.Net;
using System.Text.Json;
using Diten.PlanningService.Application.Features.Mrp;
using Diten.PlanningService.Api.Features.DemandPlanning;

namespace Diten.PlanningService.Api.Features.Mrp;

public sealed class InventoryAvailabilityReader(IHttpClientFactory clients,
    IHttpContextAccessor contexts) : IMrpInventoryReader
{
    public async Task<MrpAvailableStock?> ReadAsync(Guid tenantId, Guid legalEntityId,
        Guid skuId, Guid warehouseId, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || legalEntityId == Guid.Empty || skuId == Guid.Empty ||
            warehouseId == Guid.Empty) return null;
        var context = contexts.HttpContext;
        var bearer = context?.Request.Headers.Authorization.ToString();
        if (context?.Items["DemandPlanning.TenantId"] is not Guid current || current != tenantId ||
            string.IsNullOrWhiteSpace(bearer) ||
            !bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;

        var path = $"api/inventory/availability?skuId={skuId:D}&warehouseId={warehouseId:D}";
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        PlanningCorrelation.Forward(context, request);
        request.Headers.TryAddWithoutValidation("Authorization", bearer);
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId.ToString("D"));
        request.Headers.TryAddWithoutValidation("X-Legal-Entity-Id", legalEntityId.ToString("D"));
        using var response = await clients.CreateClient("DemandGateway")
            .SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK) return null;
        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
        var data = json.RootElement;
        if (!Guid.TryParse(data.GetProperty("skuId").GetString(), out var returnedSku) ||
            !Guid.TryParse(data.GetProperty("legalEntityId").GetString(), out var returnedCompany) ||
            returnedSku != skuId || returnedCompany != legalEntityId ||
            data.GetProperty("scope").GetProperty("warehouseId").GetString() != warehouseId.ToString("D"))
            return null;
        var uom = data.GetProperty("baseUomId").GetString();
        var wireQuantity = data.GetProperty("available").GetString();
        var wireAsOf = data.GetProperty("asOf").GetString();
        if (string.IsNullOrWhiteSpace(uom) ||
            !decimal.TryParse(wireQuantity, NumberStyles.Number,
                CultureInfo.InvariantCulture, out var available) || available < 0 ||
            !DateTimeOffset.TryParse(wireAsOf, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var asOf)) return null;
        return new(tenantId, legalEntityId, skuId, warehouseId, uom, available, asOf);
    }
}
