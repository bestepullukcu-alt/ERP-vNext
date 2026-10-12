using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Application.Features.Inventory.Services;

/// <summary>
/// SHA-256 over the canonicalised movement (pack §4 <c>RequestFingerprint</c>). Canonical means: typed values, not
/// raw text — <c>"100"</c> and <c>"100.000"</c> are the same quantity, an omitted status and its default are the
/// same status, timestamps are compared as UTC instants. Anything else that differs makes it a different request,
/// which under the same key is <c>IDEMPOTENCY_KEY_REUSED</c>.
/// </summary>
public static class RequestFingerprint
{
    public static string Compute(ParsedMovement movement)
    {
        var canonical = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["idempotencyKey"] = movement.IdempotencyKey,
            ["documentGroupId"] = movement.DocumentGroupId,
            ["movementType"] = WireNames.ToWire(movement.MovementType),
            ["movementReason"] = movement.MovementReason,
            ["itemId"] = movement.ItemId.ToString("D"),
            ["skuId"] = movement.SkuId.ToString("D"),
            ["skuLevel"] = WireNames.ToWire(movement.SkuLevel),
            ["quantity"] = InventoryQuantity.ToWire(movement.Quantity),
            ["uomId"] = movement.UomId,
            ["lotId"] = movement.LotId,
            ["serialIds"] = movement.SerialIds,
            ["fromWarehouseId"] = movement.Direction?.FromWarehouseId,
            ["fromLocationId"] = movement.Direction?.FromLocationId,
            ["fromStockStatus"] = movement.Direction?.FromStockStatus is { } from ? WireNames.ToWire(from) : null,
            ["toWarehouseId"] = movement.Direction?.ToWarehouseId,
            ["toLocationId"] = movement.Direction?.ToLocationId,
            ["toStockStatus"] = movement.Direction?.ToStockStatus is { } to ? WireNames.ToWire(to) : null,
            ["sourceModule"] = movement.SourceModule,
            ["sourceType"] = movement.SourceType,
            ["sourceDocumentId"] = movement.SourceDocumentId,
            ["sourceLineId"] = movement.SourceLineId,
            ["sourceSystem"] = movement.SourceSystem,
            ["transactionDate"] = movement.TransactionDate.UtcTicks,
            ["postingDate"] = movement.PostingDate.UtcTicks
        };
        // INVENTORY 1.3.0 (MOD-0176 KF-B): the bypass reason is part of the request ONLY when sent — a request without it keeps the
        // fingerprint it had before 1.3.0, so the replay of a movement committed earlier still matches.
        if (movement.FefoBypassReason is { } reason)
        {
            canonical["fefoBypassReason"] = reason;
        }


        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonical));
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}
