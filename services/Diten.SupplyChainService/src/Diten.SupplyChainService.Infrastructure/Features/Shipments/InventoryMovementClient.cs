using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Diten.SupplyChainService.Application.Features.Shipments.Dispatch;
namespace Diten.SupplyChainService.Infrastructure.Features.Shipments;

/// <summary>
/// INVENTORY 1.5.0 postMovement through the Gateway (MOD-0183 §23): <c>{Inventory:ReferenceBaseUrl}api/inventory/movements</c>,
/// the caller's token, <c>Idempotency-Key</c>, <c>X-Legal-Entity-Id</c> = the shipment's LE and the command's correlation id —
/// the §23.1 headers only (no <c>X-Tenant-Id</c>: postMovement does not declare it, review F-8).
/// Each status is its own outcome; an ambiguous failure (timeout, connection lost) is marked so the sent line is resent.
/// </summary>
public sealed class InventoryMovementClient(HttpClient client, IConfiguration configuration) : IInventoryMovementClient
{
    public async Task<InventoryPostResult> PostAsync(InventoryPostRequest request, CancellationToken ct)
    {
        if (!Uri.TryCreate(configuration["Inventory:ReferenceBaseUrl"], UriKind.Absolute, out var root) || root.Scheme is not ("http" or "https"))
            return new(InventoryOutcome.Unavailable, 0);
        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(root, "api/inventory/movements"))
        {
            Content = new StringContent(request.BodyJson, Encoding.UTF8, "application/json")
        };
        message.Headers.TryAddWithoutValidation("Authorization", request.Authorization);
        message.Headers.Add("Idempotency-Key", request.IdempotencyKey);
        message.Headers.Add("X-Legal-Entity-Id", request.LegalEntityId.ToString("D"));
        message.Headers.Add("X-Correlation-Id", request.CorrelationId.ToString("D"));
        try
        {
            using var response = await client.SendAsync(message, ct);
            var status = (int)response.StatusCode;
            var text = await response.Content.ReadAsStringAsync(ct);
            if (response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK) return Posted(status, text);
            var (code, details) = Error(text);
            return response.StatusCode switch
            {
                HttpStatusCode.UnprocessableEntity => new(InventoryOutcome.Refused, status, InventoryCode: code, Details: details),
                HttpStatusCode.BadRequest => new(InventoryOutcome.Invalid, status, InventoryCode: code, Details: details),
                HttpStatusCode.Forbidden => new(InventoryOutcome.Forbidden, status, InventoryCode: code, Details: details),
                // 409 may be INVENTORY still processing the same key; the next holder resends it.
                HttpStatusCode.Conflict => new(InventoryOutcome.Unavailable, status, InventoryCode: code, Ambiguous: true),
                // Only INVENTORY's own 503 and a 401 certainly processed nothing. A 500, the Gateway's 502 / 504 or any other
                // 5xx can arrive after INVENTORY committed (review F-1): the sent line stays sent and is resent with its key.
                HttpStatusCode.ServiceUnavailable or HttpStatusCode.Unauthorized => new(InventoryOutcome.Unavailable, status, InventoryCode: code),
                _ => new(InventoryOutcome.Unavailable, status, InventoryCode: code, Ambiguous: status >= 500)
            };
        }
        catch (HttpRequestException) { return new(InventoryOutcome.Unavailable, 0, Ambiguous: true); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new(InventoryOutcome.Unavailable, 0, Ambiguous: true); }
    }

    private static InventoryPostResult Posted(int status, string text)
    {
        try
        {
            using var body = JsonDocument.Parse(text);
            var e = body.RootElement;
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("transactionId", out var id) || id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString()))
                return new(InventoryOutcome.Unavailable, status, Ambiguous: true);
            var replay = e.TryGetProperty("idempotentReplay", out var r) && r.ValueKind == JsonValueKind.True;
            string? lot = e.TryGetProperty("balanceSnapshot", out var snapshot) && snapshot.ValueKind == JsonValueKind.Object
                && snapshot.TryGetProperty("lotId", out var l) && l.ValueKind == JsonValueKind.String ? l.GetString() : null;
            return new(InventoryOutcome.Posted, status, id.GetString(), replay, lot);
        }
        // A 2xx we cannot read may still be a posted movement: resend the same key to learn its id.
        catch (JsonException) { return new(InventoryOutcome.Unavailable, status, Ambiguous: true); }
    }

    private static (string? Code, JsonObject? Details) Error(string text)
    {
        try
        {
            if (JsonNode.Parse(text) is JsonObject root && root["error"] is JsonObject error)
                return (error["code"] is JsonValue code && code.TryGetValue<string>(out var c) ? c : null, error["details"]?.DeepClone() as JsonObject);
        }
        catch (JsonException) { }
        return (null, null);
    }
}
