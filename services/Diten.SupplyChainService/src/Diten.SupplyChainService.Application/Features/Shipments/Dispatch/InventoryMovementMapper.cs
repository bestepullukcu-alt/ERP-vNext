using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.SupplyChainService.Domain.Features.Shipments;
namespace Diten.SupplyChainService.Application.Features.Shipments.Dispatch;

/// <summary>
/// The two INVENTORY 1.5.0 MovementRequest bodies of MOD-0183 §23: the GOODS_ISSUE_SALES of a line (§23.1) and the
/// compensating REVERSAL (§23.4 table). A body is built once, stored with the attempt and resent byte for byte.
/// </summary>
public static class InventoryMovementMapper
{
    public const string IssueType = "GOODS_ISSUE_SALES";
    public const string ReversalType = "REVERSAL";

    // One fixed UTC text form: INVENTORY's idempotency fingerprint includes the dates, so a resend must not re-format them.
    public static string Instant(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);

    public static string Issue(Shipment shipment, ShipmentLine line, DispatchAttempt attempt)
    {
        var key = attempt.IssueKey(shipment.Id, line.LineNumber);
        var body = new JsonObject
        {
            ["idempotencyKey"] = key,
            ["documentGroupId"] = shipment.Id.ToString(),
            ["movementType"] = IssueType,
            ["itemId"] = line.ItemId.ToString(),
            ["skuId"] = line.SkuId.ToString(),
            ["skuLevel"] = line.SkuLevel,
            ["fromWarehouseId"] = shipment.WarehouseReferenceId,
            ["quantity"] = line.Quantity,
            ["uomId"] = line.UomId,
            ["fromStockStatus"] = "AVAILABLE",
            ["sourceModule"] = "MOD-0183",
            ["sourceType"] = "SHIPMENT",
            ["sourceDocumentId"] = shipment.Id.ToString(),
            ["sourceLineId"] = line.LineNumber,
            ["transactionDate"] = Instant(attempt.TransactionDate),
            ["postingDate"] = Instant(attempt.PostingDate)
        };
        // Optional §23.1 fields are sent only when the line has them; serialIds is not sent in v1.
        if (line.FromLocationId is not null) body["fromLocationId"] = line.FromLocationId;
        if (line.LotId is not null) body["lotId"] = line.LotId;
        if (line.FefoBypassReason is not null) body["fefoBypassReason"] = line.FefoBypassReason;
        return body.ToJsonString();
    }

    /// <summary>
    /// §23.4 REVERSAL: item, SKU, level, quantity and unit equal to the stored issue; source = MOD-0173 / REVERSAL / the issue
    /// transactionId. Never lotId, serialIds, a from*/to* field, fefoBypassReason or sourceLineId — INVENTORY derives them.
    /// </summary>
    public static string Reversal(Guid shipmentId, string issueRequestJson, string issueTransactionId, string idempotencyKey, DateTimeOffset reversalDate)
    {
        using var issue = JsonDocument.Parse(issueRequestJson);
        var original = issue.RootElement;
        var body = new JsonObject
        {
            ["idempotencyKey"] = idempotencyKey,
            ["documentGroupId"] = shipmentId.ToString(),
            ["movementType"] = ReversalType,
            ["itemId"] = original.GetProperty("itemId").GetString(),
            ["skuId"] = original.GetProperty("skuId").GetString(),
            ["skuLevel"] = original.GetProperty("skuLevel").GetString(),
            ["quantity"] = original.GetProperty("quantity").GetString(),
            ["uomId"] = original.GetProperty("uomId").GetString(),
            ["sourceModule"] = "MOD-0173",
            ["sourceType"] = "REVERSAL",
            ["sourceDocumentId"] = issueTransactionId,
            ["transactionDate"] = Instant(reversalDate),
            ["postingDate"] = Instant(reversalDate)
        };
        return body.ToJsonString();
    }
}
