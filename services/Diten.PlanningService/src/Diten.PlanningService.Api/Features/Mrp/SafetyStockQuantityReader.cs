using System.Net;
using System.Globalization;
using System.Text.Json;
using Diten.PlanningService.Application.Features.Mrp;

namespace Diten.PlanningService.Api.Features.Mrp;

public sealed class SafetyStockQuantityReader(IHttpClientFactory clients,
    IHttpContextAccessor contexts) : IMrpApprovedSafetyStockReader
{
    public async Task<MrpSafetyStockRead> ReadAsync(Guid tenantId, Guid legalEntityId,
        Guid skuId, Guid warehouseId, DateOnly asOf, CancellationToken ct)
    {
        var context = contexts.HttpContext;
        var bearer = context?.Request.Headers.Authorization.ToString();
        if (tenantId == Guid.Empty || legalEntityId == Guid.Empty || skuId == Guid.Empty ||
            warehouseId == Guid.Empty ||
            context?.Items["DemandPlanning.TenantId"] is not Guid current || current != tenantId ||
            string.IsNullOrWhiteSpace(bearer) ||
            !bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return new(MrpSafetyStockStatus.Unavailable);

        try
        {
            var path = $"api/v1/safety-stock/policies/manual-quantities/effective?skuId={skuId:D}&warehouseId={warehouseId:D}&asOf={asOf.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.TryAddWithoutValidation("Authorization", bearer);
            request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId.ToString("D"));
            request.Headers.TryAddWithoutValidation("X-Legal-Entity-Id", legalEntityId.ToString("D"));
            using var response = await clients.CreateClient("SafetyStockService").SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return new(MrpSafetyStockStatus.NotFound);
            if (response.StatusCode != HttpStatusCode.OK)
                return new(MrpSafetyStockStatus.Unavailable);

            using var json = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var data = json.RootElement.GetProperty("data");
            var quantity = data.GetProperty("quantity").GetDecimal();
            var baseUom = data.GetProperty("baseUomId").GetString();
            var source = data.GetProperty("source").GetString();
            var revision = data.GetProperty("policyRevisionId").GetGuid();
            var from = DateOnly.ParseExact(data.GetProperty("effectiveFrom").GetString()!,
                "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var until = DateOnly.ParseExact(data.GetProperty("effectiveUntil").GetString()!,
                "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var version = data.GetProperty("recordVersion").GetInt64();
            if (quantity < 0 || string.IsNullOrWhiteSpace(baseUom) ||
                source is not ("Manual" or "Calculated") || revision == Guid.Empty ||
                from > asOf || until < asOf || version <= 0)
                return new(MrpSafetyStockStatus.Unavailable);
            return new(MrpSafetyStockStatus.Found,
                new(quantity, baseUom, revision, from, until, version, source));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { return new(MrpSafetyStockStatus.Unavailable); }
    }
}
