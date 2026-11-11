using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Diten.SupplyChainService.Application.Features.Shipments.Dispatch;
using Diten.SupplyChainService.Domain.Features.Returns;
namespace Diten.SupplyChainService.Application.Features.Returns.Receipt;

/// <summary>
/// The two INVENTORY 1.5.0 MovementRequest bodies of MOD-0186 §34: the CUSTOMER_RETURN of a return line into QUARANTINE
/// (§34.1) and the late writer's REVERSAL (§34.2). A body is built once per request version, stored with the attempt and
/// resent byte for byte.
/// </summary>
public static class CustomerReturnMovementMapper
{
    public const string ReceiptType = "CUSTOMER_RETURN";
    public const string Quarantine = "QUARANTINE";

    /// <summary>
    /// §34.1: item, SKU, level and lot come from the shipment line the completed dispatch attempt issued (read over the
    /// shipment seam), never from the client; quantity and unit from the return line; the target and dates from the stored
    /// request. <c>serialIds</c> is never sent (serial-tracked returns are out of v1).
    /// </summary>
    public static string Receipt(Guid returnId, Guid shipmentId, ReturnLine line, ReturnSourceLine source, int k, string toWarehouseId,
        string? toLocationId, DateTimeOffset occurredAt)
    {
        var at = InventoryMovementMapper.Instant(occurredAt);
        var body = new JsonObject
        {
            ["idempotencyKey"] = ReceiptAttempt.ReceiptKey(returnId, line.ShipmentLineNumber, k),
            ["documentGroupId"] = shipmentId.ToString(),
            ["movementType"] = ReceiptType,
            ["itemId"] = source.ItemId?.ToString(),
            ["skuId"] = source.SkuId?.ToString(),
            ["skuLevel"] = source.SkuLevel,
            ["quantity"] = line.Quantity,
            ["uomId"] = line.UomId,
            ["toWarehouseId"] = toWarehouseId,
            ["toStockStatus"] = Quarantine,
            ["sourceModule"] = "MOD-0186",
            ["sourceType"] = ReceiptType,
            ["sourceDocumentId"] = returnId.ToString(),
            ["sourceLineId"] = line.ShipmentLineNumber,
            ["transactionDate"] = at,
            ["postingDate"] = at
        };
        if (source.LotId is not null) body["lotId"] = source.LotId;
        if (toLocationId is not null) body["toLocationId"] = toLocationId;
        return body.ToJsonString();
    }

    /// <summary>
    /// §34.2 late writer: the §23.4 REVERSAL shape of MOD-0183 (item, SKU, level, quantity and unit of the receipt; source
    /// MOD-0173 / REVERSAL / its transactionId; no lot, serial, from* or to*), dated the moment the late writer got its 201.
    /// </summary>
    public static string Reversal(Guid shipmentId, string receiptRequestJson, string receiptTransactionId, string idempotencyKey, DateTimeOffset at) =>
        InventoryMovementMapper.Reversal(shipmentId, receiptRequestJson, receiptTransactionId, idempotencyKey, at);

    /// <summary>A validated, normalized <c>occurredAt</c> as an instant; fractions beyond 100 ns are cut, a leap second is refused.</summary>
    public static bool TryInstant(string normalized, out DateTimeOffset value)
    {
        var cut = Regex.Replace(normalized, @"\.([0-9]{7})[0-9]+", ".$1");
        return DateTimeOffset.TryParse(cut, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out value);
    }
}
