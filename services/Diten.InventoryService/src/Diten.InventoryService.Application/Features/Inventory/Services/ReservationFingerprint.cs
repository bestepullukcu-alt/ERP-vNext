using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Application.Features.Inventory.Services;

/// <summary>
/// SHA-256 over the canonicalised reservation request (typed values) — same rule as <see cref="RequestFingerprint"/>. MOD-0173-FU03
/// (AC-7): <c>warehouseId</c> / <c>lotId</c> enter ONLY when sent (the 1.3.0 <c>fefoBypassReason</c> pattern), so an unscoped request
/// keeps its 1.5.0 fingerprint byte for byte.
/// </summary>
public static class ReservationFingerprint
{
    public static string Compute(ParsedReservation reservation)
    {
        var canonical = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["idempotencyKey"] = reservation.IdempotencyKey,
            ["itemId"] = reservation.ItemId.ToString("D"),
            ["skuId"] = reservation.SkuId.ToString("D"),
            ["skuLevel"] = WireNames.ToWire(reservation.SkuLevel),
            ["quantity"] = InventoryQuantity.ToWire(reservation.Quantity),
            ["reservationType"] = reservation.ReservationType,
            ["sourceModule"] = reservation.SourceModule,
            ["sourceDocumentId"] = reservation.SourceDocumentId,
            ["sourceLineId"] = reservation.SourceLineId,
            ["priority"] = reservation.Priority,
            ["expiresAt"] = reservation.ExpiresAt?.UtcTicks
        };
        if (reservation.WarehouseId is not null) canonical["warehouseId"] = reservation.WarehouseId;
        if (reservation.LotId is not null) canonical["lotId"] = reservation.LotId;

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonical))));
    }
}
