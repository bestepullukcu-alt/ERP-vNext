using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Diten.SupplyChainService.Application.Features.Returns;
using Diten.SupplyChainService.Domain.Features.Returns;
namespace Diten.SupplyChainService.Infrastructure.Features.Returns;

/// <summary>Shipment detail is the only HTTP seam. Inventory/Warehouse references stay opaque.</summary>
public sealed class ReturnReferenceReader(HttpClient client, IConfiguration configuration, ReturnRequestContext context) : IReturnReferenceReader
{
    private static void Invalid() => throw new ReturnFailureException(502, "DEPENDENCY_RESPONSE_INVALID");
    private static bool Text(JsonElement e) => e.ValueKind == JsonValueKind.String;
    private static bool Uuid(JsonElement e) => Text(e) && ReturnWire.Uuid(e.GetString());
    private static bool Date(JsonElement e) => Text(e) && ReturnWire.Instant(e.GetString());
    private static bool Nullable(JsonElement e, Func<JsonElement, bool> test) => e.ValueKind == JsonValueKind.Null || test(e);
    private static bool Array(JsonElement e, Func<JsonElement, bool> test) => e.ValueKind == JsonValueKind.Array && e.EnumerateArray().All(test);
    private static void Property(JsonElement e, string name, Func<JsonElement, bool> test)
    { if (e.TryGetProperty(name, out var value) && !test(value)) Invalid(); }
    private static bool PositiveQuantity(JsonElement e) => Text(e) && Regex.IsMatch(e.GetString()!, @"\A[0-9]+(?:\.[0-9]+)?\z") && e.GetString()!.Any(c => c is >= '1' and <= '9');
    private static bool Line(JsonElement e)
    {
        string[] required = ["lineNumber", "itemId", "skuId", "quantity", "uomId"];
        if (e.ValueKind != JsonValueKind.Object || required.Any(n => !e.TryGetProperty(n, out _))) return false;
        if (e.EnumerateObject().Any(p => !required.Contains(p.Name) && p.Name != "inventoryReferenceId") || e.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != e.EnumerateObject().Count()) return false;
        return Text(e.GetProperty("lineNumber")) && Uuid(e.GetProperty("itemId")) && Uuid(e.GetProperty("skuId")) && PositiveQuantity(e.GetProperty("quantity")) && Text(e.GetProperty("uomId")) && (!e.TryGetProperty("inventoryReferenceId", out var reference) || Nullable(reference, Text));
    }
    private static bool Pod(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Null) return true;
        if (e.ValueKind != JsonValueKind.Object || new[] { "recipientName", "receivedAt", "evidenceReferenceIds" }.Any(n => !e.TryGetProperty(n, out _))) return false;
        return Text(e.GetProperty("recipientName")) && Date(e.GetProperty("receivedAt")) && Array(e.GetProperty("evidenceReferenceIds"), Text) && (!e.TryGetProperty("note", out var note) || Nullable(note, Text));
    }
    private static async Task<bool> IsShipmentRootInvalid(HttpContent content, CancellationToken ct)
    {
        try
        {
            using var body = JsonDocument.Parse(await content.ReadAsStringAsync(ct));
            return body.RootElement.ValueKind == JsonValueKind.Object &&
                body.RootElement.TryGetProperty("contractVersion", out var version) && Text(version) && version.GetString() == "v1" &&
                body.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object &&
                error.TryGetProperty("code", out var code) && Text(code) && code.GetString() == "SHIPMENT_ROOT_INVALID";
        }
        catch (JsonException)
        {
            return false;
        }
    }
    public static ReturnReferenceSnapshot ParseShipment(JsonElement e, Guid expectedId, DateTimeOffset observedAt)
    {
        string[] required = ["sourceModule", "sourceType", "warehouseReferenceId", "shipToReference", "lines", "contractVersion"];
        if (e.ValueKind != JsonValueKind.Object || required.Any(n => !e.TryGetProperty(n, out _))) Invalid();
        if (e.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != e.EnumerateObject().Count()) Invalid();
        foreach (var name in new[] { "sourceModule", "sourceType", "warehouseReferenceId", "shipToReference", "shipmentNumber", "sourceDocumentId" }) Property(e, name, Text);
        Property(e, "contractVersion", x => Text(x) && x.GetString() == "v1");
        Property(e, "shipmentId", Uuid);
        Property(e, "status", x => Text(x) && new[] { "Draft", "Planned", "Dispatched", "InTransit", "Delivered", "Exception", "Closed", "Cancelled" }.Contains(x.GetString(), StringComparer.Ordinal));
        Property(e, "carrierId", x => Nullable(x, Uuid)); Property(e, "loadId", x => Nullable(x, Uuid));
        Property(e, "plannedShipAt", Date); Property(e, "plannedDeliverAt", x => Nullable(x, Date)); Property(e, "actualDeliverAt", x => Nullable(x, Date)); Property(e, "pod", Pod);
        Property(e, "lines", x => Array(x, Line));
        var lines = e.GetProperty("lines").EnumerateArray().Select(x => new ReturnSourceLine(x.GetProperty("lineNumber").GetString()!, x.GetProperty("quantity").GetString()!, x.GetProperty("uomId").GetString()!, Guid.Parse(x.GetProperty("itemId").GetString()!), Guid.Parse(x.GetProperty("skuId").GetString()!))).ToArray();
        if (lines.Select(x => x.LineNumber).Distinct(StringComparer.Ordinal).Count() != lines.Length) Invalid();
        if (!e.TryGetProperty("shipmentId", out var shipmentId) || !e.TryGetProperty("status", out var status)) throw new ReturnFailureException(503, "REFERENCE_STATE_UNAVAILABLE");
        if (Guid.Parse(shipmentId.GetString()!) != expectedId) Invalid();
        if (!e.TryGetProperty("lifecycleCorrelationId", out var root) || root.ValueKind == JsonValueKind.Null) throw new ReturnFailureException(503, "RETURN_SHIPMENT_ROOT_UNAVAILABLE");
        if (!Uuid(root)) throw new ReturnFailureException(502, "RETURN_SHIPMENT_ROOT_INVALID");
        return new(expectedId, status.GetString()!, Guid.Parse(root.GetString()!), observedAt, lines);
    }
    public async Task<ReturnReferenceSnapshot> ObserveAsync(ReturnOrder order, CancellationToken ct)
    {
        if (!Uri.TryCreate(configuration["Returns:ReferenceBaseUrl"], UriKind.Absolute, out var root) || root.Scheme is not ("http" or "https")) throw new ReturnFailureException(503, "DEPENDENCY_UNAVAILABLE");
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(root, "api/shipment-bundle/shipments/" + order.ShipmentId.ToString("D")));
        request.Headers.TryAddWithoutValidation("Authorization", context.Authorization);
        request.Headers.Add("X-Tenant-Id", context.Scope.TenantId.ToString("D"));
        request.Headers.Add("X-Legal-Entity-Id", context.Scope.LegalEntityId.ToString("D"));
        request.Headers.Add("X-Correlation-Id", context.CorrelationId.ToString("D"));
        try
        {
            using var response = await client.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) throw new ReturnFailureException(404, "SHIPMENT_NOT_FOUND");
            if (response.StatusCode == HttpStatusCode.InternalServerError && await IsShipmentRootInvalid(response.Content, ct))
                throw new ReturnFailureException(502, "RETURN_SHIPMENT_ROOT_INVALID");
            if ((int)response.StatusCode >= 500 || response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) throw new ReturnFailureException(503, "DEPENDENCY_UNAVAILABLE");
            if (response.StatusCode != HttpStatusCode.OK) Invalid();
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return ParseShipment(body.RootElement, order.ShipmentId, DateTimeOffset.UtcNow);
        }
        catch (HttpRequestException) { throw new ReturnFailureException(503, "DEPENDENCY_UNAVAILABLE"); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ReturnFailureException(503, "DEPENDENCY_UNAVAILABLE"); }
        catch (JsonException) { throw new ReturnFailureException(502, "DEPENDENCY_RESPONSE_INVALID"); }
    }
}
