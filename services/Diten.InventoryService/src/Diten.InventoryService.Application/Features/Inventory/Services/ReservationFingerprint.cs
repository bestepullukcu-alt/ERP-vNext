using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Application.Features.Inventory.Services;

/// <summary>SHA-256 over the canonicalised reservation request (typed values) — same rule as <see cref="RequestFingerprint"/>.</summary>
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

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonical))));
    }
}
