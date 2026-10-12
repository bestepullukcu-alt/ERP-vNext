using System.Text.Json;
using Diten.InventoryService.Application.Features.Inventory.Commands;

namespace Diten.InventoryService.Api.Contract;

/// <summary>
/// Reads the contract <c>ReservationRequest</c> body without model binding (same rules as
/// <see cref="MovementRequestReader"/>): a JSON object; string members must be strings, <c>priority</c> an integer
/// number, nulls count as absent; <c>tenantId</c> / <c>legalEntityId</c> in any casing are refused.
/// </summary>
public static class ReservationRequestReader
{
    private static readonly string[] ScopeProperties = ["tenantId", "legalEntityId", "tenant_id", "legal_entity_id"];

    public static async Task<(CreateReservationCommand? Command, string? Error)> ReadAsync(
        Stream body, string? headerIdempotencyKey, CancellationToken cancellationToken)
    {
        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return (null, "The request body must be a JSON object.");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return (null, "The request body must be a JSON object.");
            }

            if (root.EnumerateObject().Any(p => ScopeProperties.Contains(p.Name, StringComparer.OrdinalIgnoreCase)))
            {
                return (null, "tenantId/legalEntityId must not be sent: scope is taken from the access token.");
            }

            var errors = new List<string>();
            string? S(string name)
            {
                if (!root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
                {
                    return null;
                }

                if (value.ValueKind != JsonValueKind.String)
                {
                    errors.Add($"{name} must be a string.");
                    return null;
                }

                return value.GetString();
            }

            string? priority = null;
            if (root.TryGetProperty("priority", out var p) && p.ValueKind != JsonValueKind.Null)
            {
                if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out _))
                {
                    priority = p.GetRawText();
                }
                else
                {
                    errors.Add("priority must be an integer.");
                }
            }

            var command = new CreateReservationCommand
            {
                HeaderIdempotencyKey = headerIdempotencyKey,
                IdempotencyKey = S("idempotencyKey"),
                ItemId = S("itemId"),
                SkuId = S("skuId"),
                SkuLevel = S("skuLevel"),
                Quantity = S("quantity"),
                BaseQuantity = S("baseQuantity"),
                ReservationType = S("reservationType"),
                SourceModule = S("sourceModule"),
                SourceDocumentId = S("sourceDocumentId"),
                SourceLineId = S("sourceLineId"),
                Priority = priority,
                ExpiresAt = S("expiresAt")
            };

            return errors.Count > 0 ? (null, string.Join(" ", errors)) : (command, null);
        }
    }
}
