using System.Text.Json;
using Diten.InventoryService.Application.Features.Inventory.Commands;

namespace Diten.InventoryService.Api.Contract;

/// <summary>
/// Reads the contract <c>MovementRequest</c> body into <see cref="PostMovementCommand"/> without model binding, so
/// every malformed body is a contract 400 (never a framework 415 / ProblemDetails):
/// the body must be a JSON object; each declared property must have its declared JSON type (string, or array of
/// strings for <c>serialIds</c>), or be null / absent; unknown properties are ignored (the schema does not forbid
/// them) EXCEPT <c>tenantId</c> / <c>legalEntityId</c> in any casing, which are refused — scope comes from the
/// token only (pack §8).
/// </summary>
public static class MovementRequestReader
{
    private static readonly string[] ScopeProperties = ["tenantId", "legalEntityId", "tenant_id", "legal_entity_id"];

    public static async Task<(PostMovementCommand? Command, string? Error)> ReadAsync(
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

            foreach (var property in root.EnumerateObject())
            {
                if (ScopeProperties.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                {
                    return (null, "tenantId/legalEntityId must not be sent: scope is taken from the access token.");
                }
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

            List<string>? serialIds = null;
            if (root.TryGetProperty("serialIds", out var serials) && serials.ValueKind != JsonValueKind.Null)
            {
                if (serials.ValueKind != JsonValueKind.Array || serials.EnumerateArray().Any(e => e.ValueKind != JsonValueKind.String))
                {
                    errors.Add("serialIds must be an array of strings.");
                }
                else
                {
                    serialIds = serials.EnumerateArray().Select(e => e.GetString()!).ToList();
                }
            }

            var command = new PostMovementCommand
            {
                HeaderIdempotencyKey = headerIdempotencyKey,
                IdempotencyKey = S("idempotencyKey"),
                DocumentGroupId = S("documentGroupId"),
                MovementType = S("movementType"),
                MovementReason = S("movementReason"),
                ItemId = S("itemId"),
                SkuId = S("skuId"),
                SkuLevel = S("skuLevel"),
                FromWarehouseId = S("fromWarehouseId"),
                FromLocationId = S("fromLocationId"),
                ToWarehouseId = S("toWarehouseId"),
                ToLocationId = S("toLocationId"),
                Quantity = S("quantity"),
                UomId = S("uomId"),
                LotId = S("lotId"),
                SerialIds = serialIds,
                FromStockStatus = S("fromStockStatus"),
                ToStockStatus = S("toStockStatus"),
                SourceModule = S("sourceModule"),
                SourceType = S("sourceType"),
                SourceDocumentId = S("sourceDocumentId"),
                SourceLineId = S("sourceLineId"),
                SourceSystem = S("sourceSystem"),
                TransactionDate = S("transactionDate"),
                PostingDate = S("postingDate")
            };

            return errors.Count > 0 ? (null, string.Join(" ", errors)) : (command, null);
        }
    }
}
