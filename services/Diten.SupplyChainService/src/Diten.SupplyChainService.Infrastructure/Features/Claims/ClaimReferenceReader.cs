using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Diten.SupplyChainService.Application.Features.Claims;
using Diten.SupplyChainService.Domain.Features.Claims;
namespace Diten.SupplyChainService.Infrastructure.Features.Claims;
public sealed class ClaimReferenceReader(HttpClient client, IConfiguration configuration, ClaimRequestContext context) : IClaimReferenceReader
{
    private static void Invalid() => throw new ClaimFailureException(502, "CLAIM_REFERENCE_INVALID");
    private static void Incomplete() => throw new ClaimFailureException(503, "CLAIM_REFERENCE_INCOMPLETE");
    private static bool Text(JsonElement e) => e.ValueKind == JsonValueKind.String;
    private static bool Uuid(JsonElement e) => Text(e) && ClaimWire.Uuid(e.GetString());
    private static bool Date(JsonElement e) => Text(e) && ClaimWire.Instant(e.GetString());
    private static bool Nullable(JsonElement e, Func<JsonElement, bool> test) => e.ValueKind == JsonValueKind.Null || test(e);
    private static bool Values(JsonElement e, params string[] values) => Text(e) && values.Contains(e.GetString(), StringComparer.Ordinal);
    private static bool Array(JsonElement e, Func<JsonElement, bool> test) => e.ValueKind == JsonValueKind.Array && e.EnumerateArray().All(test);
    private static void Required(JsonElement e, params string[] keys)
    {
        if (e.ValueKind != JsonValueKind.Object || keys.Any(k => !e.TryGetProperty(k, out _)) ||
            e.EnumerateObject().GroupBy(p => p.Name, StringComparer.Ordinal).Any(g => g.Count() > 1)) Invalid();
    }
    private static void Property(JsonElement e, string name, Func<JsonElement, bool> test)
    { if (e.TryGetProperty(name, out var value) && !test(value)) Invalid(); }
    private static bool Line(JsonElement e)
    {
        if (!ClaimWire.Object(e, ["lineNumber", "itemId", "skuId", "quantity", "uomId"], ["lineNumber", "itemId", "skuId", "quantity", "uomId", "inventoryReferenceId"])) return false;
        return Text(e.GetProperty("lineNumber")) && Uuid(e.GetProperty("itemId")) && Uuid(e.GetProperty("skuId")) &&
            Text(e.GetProperty("quantity")) && Regex.IsMatch(e.GetProperty("quantity").GetString()!, @"\A-?[0-9]+(\.[0-9]+)?\z") && Text(e.GetProperty("uomId")) &&
            (!e.TryGetProperty("inventoryReferenceId", out var reference) || Nullable(reference, Text));
    }
    private static bool Pod(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Null) return true;
        if (e.ValueKind != JsonValueKind.Object || new[] { "recipientName", "receivedAt", "evidenceReferenceIds" }.Any(k => !e.TryGetProperty(k, out _))) return false;
        return Text(e.GetProperty("recipientName")) && Date(e.GetProperty("receivedAt")) && Array(e.GetProperty("evidenceReferenceIds"), Text) &&
            (!e.TryGetProperty("note", out var note) || Nullable(note, Text));
    }
    public static void ValidateShipment(JsonElement e)
    {
        Required(e, "sourceModule", "sourceType", "warehouseReferenceId", "shipToReference", "lines", "contractVersion");
        foreach (var key in new[] { "sourceModule", "sourceType", "warehouseReferenceId", "shipToReference", "shipmentNumber", "sourceDocumentId" }) Property(e, key, Text);
        Property(e, "contractVersion", v => Values(v, "v1"));
        Property(e, "shipmentId", Uuid);
        Property(e, "status", v => Values(v, "Draft", "Planned", "Dispatched", "InTransit", "Delivered", "Exception", "Closed", "Cancelled"));
        Property(e, "carrierId", v => Nullable(v, Uuid)); Property(e, "loadId", v => Nullable(v, Uuid));
        Property(e, "plannedShipAt", Date); Property(e, "plannedDeliverAt", v => Nullable(v, Date)); Property(e, "actualDeliverAt", v => Nullable(v, Date));
        Property(e, "lines", v => Array(v, Line)); Property(e, "pod", Pod);
        // The approved empty-root exception is incomplete rather than malformed.
        Property(e, "lifecycleCorrelationId", v => v.ValueKind == JsonValueKind.Null || (Text(v) && (v.GetString() == "" || Uuid(v))));
    }
    public static void ValidateCarriers(JsonElement e)
    {
        Required(e, "items", "total", "contractVersion");
        Property(e, "contractVersion", v => Values(v, "v1"));
        Property(e, "total", v => v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var n) && decimal.Truncate(n) == n);
        if (e.GetProperty("items").ValueKind != JsonValueKind.Array) Invalid();
        var ids = new HashSet<Guid>();
        foreach (var carrier in e.GetProperty("items").EnumerateArray())
        {
            Required(carrier, "carrierId", "carrierCode", "displayName", "status", "supportedModes");
            Property(carrier, "carrierId", Uuid); Property(carrier, "carrierCode", Text); Property(carrier, "displayName", Text);
            Property(carrier, "status", v => Values(v, "Active", "Suspended", "Retired"));
            Property(carrier, "supportedModes", v => Array(v, mode => Values(mode, "Road", "Rail", "Air", "Sea", "Parcel")));
            if (!ids.Add(Guid.Parse(carrier.GetProperty("carrierId").GetString()!))) Invalid();
        }
    }
    private static async Task<bool> IsShipmentRootInvalid(HttpContent content, CancellationToken ct)
    {
        try
        {
            using var body = JsonDocument.Parse(await content.ReadAsStringAsync(ct));
            if (body.RootElement.ValueKind != JsonValueKind.Object ||
                !body.RootElement.TryGetProperty("contractVersion", out var version) || !Values(version, "v1") ||
                !body.RootElement.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object ||
                !error.TryGetProperty("code", out var code) || !Values(code, "SHIPMENT_ROOT_INVALID") ||
                !error.TryGetProperty("message", out var message) || !Text(message) ||
                !error.TryGetProperty("correlationId", out var correlation) || !Uuid(correlation)) return false;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
    private async Task<JsonElement> Get(string path, CancellationToken ct)
    {
        if (!Uri.TryCreate(configuration["Claims:ReferenceBaseUrl"], UriKind.Absolute, out var root) || root.Scheme is not ("http" or "https"))
            throw new ClaimFailureException(503, "CLAIM_REFERENCE_UNAVAILABLE");
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(root, path));
        request.Headers.TryAddWithoutValidation("Authorization", context.Authorization);
        request.Headers.Add("X-Tenant-Id", context.Scope.TenantId.ToString());
        request.Headers.Add("X-Legal-Entity-Id", context.Scope.LegalEntityId.ToString());
        // Dependency request tracing is transport metadata. It must not reuse or
        // replace the authoritative Shipment lifecycle root carried by Claims.
        request.Headers.Add("X-Correlation-Id", Guid.NewGuid().ToString());
        try
        {
            using var response = await client.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) throw new ClaimFailureException(404, "CLAIM_NOT_FOUND");
            if (response.StatusCode == HttpStatusCode.InternalServerError && await IsShipmentRootInvalid(response.Content, ct))
                throw new ClaimFailureException(502, "CLAIM_REFERENCE_INVALID");
            // Q420: a 403 from the Shipment or Carrier read means the caller lacks supplychain.shipments.read or
            // supplychain.carriers.read. That is an authorization outcome, reported with the annex's 403 code
            // (claims-semantics-v3.0.0.md: "403 FORBIDDEN includes scope/identity/action failures"), not as an outage.
            // A 401 or any 5xx is still the dependency failing and stays 503.
            if (response.StatusCode == HttpStatusCode.Forbidden) throw new ClaimFailureException(403, "FORBIDDEN");
            if ((int)response.StatusCode >= 500 || response.StatusCode is HttpStatusCode.Unauthorized)
                throw new ClaimFailureException(503, "CLAIM_REFERENCE_UNAVAILABLE");
            if (response.StatusCode != HttpStatusCode.OK) Invalid();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return json.RootElement.Clone();
        }
        catch (HttpRequestException) { throw new ClaimFailureException(503, "CLAIM_REFERENCE_UNAVAILABLE"); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new ClaimFailureException(503, "CLAIM_REFERENCE_UNAVAILABLE"); }
        catch (JsonException) { throw new ClaimFailureException(502, "CLAIM_REFERENCE_INVALID"); }
    }
    public async Task<ClaimReferenceSnapshot> ObserveAsync(Claim claim, CancellationToken ct)
    {
        var shipment = await Get("api/shipment-bundle/shipments/" + claim.ShipmentId, ct);
        ValidateShipment(shipment);
        if (!shipment.TryGetProperty("shipmentId", out var shipmentId)) Incomplete();
        if (Guid.Parse(shipmentId.GetString()!) != claim.ShipmentId) Invalid();
        if (!shipment.TryGetProperty("status", out _)) Incomplete();
        Guid? shipmentCarrier = null;
        if (shipment.TryGetProperty("carrierId", out var carrier) && carrier.ValueKind != JsonValueKind.Null) shipmentCarrier = Guid.Parse(carrier.GetString()!);
        string? carrierStatus = null;
        if (claim.CarrierId is { } explicitCarrier)
        {
            if (!shipment.TryGetProperty("carrierId", out _)) Incomplete();
            // Look up the explicit reference before business validation; never invent a by-ID endpoint.
            var carriers = await Get("api/shipment-bundle/carriers", ct);
            ValidateCarriers(carriers);
            var found = carriers.GetProperty("items").EnumerateArray().Where(e => Guid.Parse(e.GetProperty("carrierId").GetString()!) == explicitCarrier).ToArray();
            if (found.Length == 0) throw new ClaimFailureException(404, "CLAIM_NOT_FOUND");
            carrierStatus = found[0].GetProperty("status").GetString();
        }
        if (!shipment.TryGetProperty("lifecycleCorrelationId", out var lifecycleRoot) || lifecycleRoot.ValueKind == JsonValueKind.Null || lifecycleRoot.GetString() == "") Incomplete();
        // UUID parsing preserves a real nil root. Authority comes from the producer seam, not parsing itself.
        return new(claim.ShipmentId, shipment.GetProperty("status").GetString()!, shipmentCarrier, carrierStatus,
            Guid.Parse(lifecycleRoot.GetString()!), DateTimeOffset.UtcNow);
    }
}
