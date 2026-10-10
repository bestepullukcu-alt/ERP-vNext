using System.Net.Http.Headers;
using System.Text.Json;
using Diten.SupplyChainService.Application.Features.SourceIntake.Contracts;

namespace Diten.SupplyChainService.Infrastructure.SourceIntake;

public sealed class WarehouseReadClient(HttpClient http) : IWarehouseReadClient
{
    public Task<DependencyResult<JsonElement>> GetAsync(TrustedSourceContext context, string outboundId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outboundId);
        return SourceRead.GetAsync(http, context, "/api/warehouse/outbound-shipments/" + Uri.EscapeDataString(outboundId),
            element => FrozenReadSchema.Warehouse(element) && element.GetProperty("outboundId").GetString() == outboundId, cancellationToken);
    }

    public Task<DependencyResult<JsonElement>> ListAsync(TrustedSourceContext context, string? status = null,
        string? warehouseId = null, string? cursor = null, CancellationToken cancellationToken = default)
        => SourceRead.GetAsync(http, context, "/api/warehouse/outbound-shipments" + SourceRead.Query(
            ("status", status), ("warehouseId", warehouseId), ("cursor", cursor)), FrozenReadSchema.WarehousePage, cancellationToken);
}

public sealed class InventoryReadClient(HttpClient http) : IInventoryReadClient
{
    public Task<DependencyResult<JsonElement>> AvailabilityAsync(TrustedSourceContext context, InventoryReference reference, CancellationToken cancellationToken = default)
        => ReadAsync(context, reference, false, cancellationToken);
    public Task<DependencyResult<JsonElement>> BalanceAsync(TrustedSourceContext context, InventoryReference reference, CancellationToken cancellationToken = default)
        => ReadAsync(context, reference, true, cancellationToken);

    private Task<DependencyResult<JsonElement>> ReadAsync(TrustedSourceContext context, InventoryReference reference, bool balance, CancellationToken cancellationToken)
    {
        if (reference.ItemId == Guid.Empty || reference.SkuId == Guid.Empty || !FrozenReadSchema.SkuLevels.Contains(reference.SkuLevel))
            return Task.FromResult(DependencyResult<JsonElement>.Blocked("INVALID_INVENTORY_REFERENCE"));
        return SourceRead.GetAsync(http, context, "/api/inventory/" + (balance ? "balance" : "availability") + SourceRead.Query(
            ("itemId", reference.ItemId.ToString()), ("skuId", reference.SkuId.ToString()), ("skuLevel", reference.SkuLevel),
            ("warehouseId", reference.WarehouseId), ("locationId", reference.LocationId), ("lotId", balance ? reference.LotId : null)),
            element => FrozenReadSchema.Inventory(element, balance, context.Scope.LegalEntityId, reference), cancellationToken);
    }
}

internal static class SourceRead
{
    public static string Query(params (string Key, string? Value)[] values)
    {
        var encoded = values.Where(v => v.Value is not null).Select(v => Uri.EscapeDataString(v.Key) + "=" + Uri.EscapeDataString(v.Value!));
        var query = string.Join("&", encoded);
        return query.Length == 0 ? "" : "?" + query;
    }

    public static async Task<DependencyResult<JsonElement>> GetAsync(HttpClient http, TrustedSourceContext context, string path,
        Func<JsonElement, bool> validate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.BearerToken);
        request.Headers.Add("X-Tenant-Id", context.Scope.TenantId.ToString());
        request.Headers.Add("X-Legal-Entity-Id", context.Scope.LegalEntityId.ToString());
        if (context.CorrelationId is { } correlation) request.Headers.Add("X-Correlation-Id", correlation.ToString());
        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return DependencyResult<JsonElement>.Blocked("DEPENDENCY_HTTP_" + (int)response.StatusCode, (int)response.StatusCode);
            if ((int)response.StatusCode != 200) return DependencyResult<JsonElement>.Blocked("SCHEMA_INCOMPATIBLE", (int)response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return validate(document.RootElement)
                ? DependencyResult<JsonElement>.Ok(document.RootElement.Clone())
                : DependencyResult<JsonElement>.Blocked("SCHEMA_INCOMPATIBLE", 200);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return DependencyResult<JsonElement>.Blocked("DEPENDENCY_TIMEOUT"); }
        catch (HttpRequestException) { return DependencyResult<JsonElement>.Blocked("DEPENDENCY_UNAVAILABLE"); }
        catch (JsonException) { return DependencyResult<JsonElement>.Blocked("SCHEMA_INCOMPATIBLE", 200); }
    }
}
