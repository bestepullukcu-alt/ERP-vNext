using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.SupplyChainService.Application.Features.Returns.Receipt;
using Diten.SupplyChainService.Domain.Features.Returns;
using Diten.SupplyChainService.Infrastructure.Features.Returns;
using Xunit;

namespace Diten.SupplyChainService.Tests.Returns.Receipt;

/// <summary>
/// MOD-0186 §34.1 / §34.2, plan r3 §4 step 2: what the shipment seam yields for the CUSTOMER_RETURN (stockIssued, per line
/// skuLevel, lotId, issueTransactionId) and the two INVENTORY bodies built from it.
/// </summary>
public sealed class CustomerReturnMapperTests
{
    private static readonly Guid Shipment = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Return = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static JsonObject Detail(JsonNode? stockIssued, string? issue = "9b7c0d1e-0000-4000-8000-000000000001") => JsonNode.Parse($$"""
        {"shipmentId":"{{Shipment}}","status":"Delivered","sourceModule":"test","sourceType":"test","warehouseReferenceId":"W","shipToReference":"to",
         "lines":[{"lineNumber":"1","quantity":"10","uomId":"EA","itemId":"22222222-2222-2222-2222-222222222222","skuId":"33333333-3333-3333-3333-333333333333",
                   "skuLevel":"Lsku","lotId":"lot-7","fromLocationId":"loc-1","issueTransactionId":{{(issue is null ? "null" : $"\"{issue}\"")}}}],
         "contractVersion":"v1","lifecycleCorrelationId":"00000000-0000-0000-0000-000000000000","dispatchState":"None","stockIssued":{{stockIssued?.ToJsonString() ?? "null"}}}
        """)!.AsObject();
    private static ReturnReferenceSnapshot Read(JsonObject node) =>
        ReturnReferenceReader.ParseShipment(JsonDocument.Parse(node.ToJsonString()).RootElement, Shipment, DateTimeOffset.UtcNow);

    [Fact]
    public void Reader_ReadsStockIssuedAndTheLineIssueFields()
    {
        var issued = Read(Detail(true));
        Assert.True(issued.StockIssued);
        var line = Assert.Single(issued.Lines);
        Assert.Equal(("Lsku", "lot-7", "9b7c0d1e-0000-4000-8000-000000000001"), (line.SkuLevel, line.LotId, line.IssueTransactionId));
        Assert.Null(Read(Detail(null, null)).StockIssued);
        Assert.False(Read(Detail(false, null)).StockIssued);
        var absent = Detail(null); absent.Remove("stockIssued");
        Assert.Null(Read(absent).StockIssued);
        Assert.Equal(502, Assert.Throws<ReturnFailureException>(() => Read(Detail(JsonValue.Create("yes")))).Status);
    }

    private static ReturnSourceLine Source(string? lot = "lot-7") =>
        new("2", "10", "EA", Guid.Parse("22222222-2222-2222-2222-222222222222"), Guid.Parse("33333333-3333-3333-3333-333333333333"), "Lsku", lot, "tx-issue-2");

    [Fact]
    public void Receipt_CarriesEverySection341Field()
    {
        var at = DateTimeOffset.Parse("2026-10-29T08:00:00Z");
        var body = JsonNode.Parse(CustomerReturnMovementMapper.Receipt(Return, Shipment, new ReturnLine("2", "4", "EA"), Source(), 3, "wh-01", "loc-q1", at))!.AsObject();
        Assert.Equal($"{Return}:2:customer-return:a3", body["idempotencyKey"]!.GetValue<string>());
        Assert.Equal("CUSTOMER_RETURN", body["movementType"]!.GetValue<string>());
        Assert.Equal("QUARANTINE", body["toStockStatus"]!.GetValue<string>());
        Assert.Equal(("22222222-2222-2222-2222-222222222222", "33333333-3333-3333-3333-333333333333", "Lsku", "lot-7"),
            (body["itemId"]!.GetValue<string>(), body["skuId"]!.GetValue<string>(), body["skuLevel"]!.GetValue<string>(), body["lotId"]!.GetValue<string>()));
        Assert.Equal(("4", "EA", "wh-01", "loc-q1"), (body["quantity"]!.GetValue<string>(), body["uomId"]!.GetValue<string>(), body["toWarehouseId"]!.GetValue<string>(), body["toLocationId"]!.GetValue<string>()));
        Assert.Equal(("MOD-0186", "CUSTOMER_RETURN", Return.ToString(), "2", Shipment.ToString()),
            (body["sourceModule"]!.GetValue<string>(), body["sourceType"]!.GetValue<string>(), body["sourceDocumentId"]!.GetValue<string>(), body["sourceLineId"]!.GetValue<string>(), body["documentGroupId"]!.GetValue<string>()));
        Assert.Equal("2026-10-29T08:00:00.0000000Z", body["transactionDate"]!.GetValue<string>());
        Assert.Equal(body["transactionDate"]!.GetValue<string>(), body["postingDate"]!.GetValue<string>());
        Assert.False(body.ContainsKey("serialIds"));
        Assert.False(body.ContainsKey("fromWarehouseId"));
        var noLot = JsonNode.Parse(CustomerReturnMovementMapper.Receipt(Return, Shipment, new ReturnLine("2", "4", "EA"), Source(null), 1, "wh-01", null, at))!.AsObject();
        Assert.False(noLot.ContainsKey("lotId"));
        Assert.False(noLot.ContainsKey("toLocationId"));
    }

    [Fact]
    public void Reversal_CarriesOnlyTheSection342Fields()
    {
        var at = DateTimeOffset.Parse("2026-10-29T09:30:00Z");
        var receipt = CustomerReturnMovementMapper.Receipt(Return, Shipment, new ReturnLine("2", "4", "EA"), Source(), 1, "wh-01", "loc-q1", at);
        var key = ReceiptAttempt.ReversalKey(Return, "2", 1);
        var body = JsonNode.Parse(CustomerReturnMovementMapper.Reversal(Shipment, receipt, "tx-c2", key, at))!.AsObject();
        Assert.Equal($"{Return}:2:customer-return-reversal:a1", key);
        Assert.Equal(new[] { "documentGroupId", "idempotencyKey", "itemId", "movementType", "postingDate", "quantity", "skuId", "skuLevel", "sourceDocumentId", "sourceModule", "sourceType", "transactionDate", "uomId" },
            body.Select(p => p.Key).Order(StringComparer.Ordinal));
        Assert.Equal(("REVERSAL", "MOD-0173", "REVERSAL", "tx-c2", "4"), (body["movementType"]!.GetValue<string>(), body["sourceModule"]!.GetValue<string>(),
            body["sourceType"]!.GetValue<string>(), body["sourceDocumentId"]!.GetValue<string>(), body["quantity"]!.GetValue<string>()));
    }

    [Theory]
    [InlineData("2026-10-29T08:00:00.123456789Z", true, "2026-10-29T08:00:00.1234567Z")]
    [InlineData("2016-12-31T23:59:60Z", false, null)]
    public void Instant_CutsBeyond100Nanoseconds_RefusesALeapSecond(string normalized, bool ok, string? expected)
    {
        Assert.Equal(ok, CustomerReturnMovementMapper.TryInstant(normalized, out var at));
        if (ok) Assert.Equal(expected, Diten.SupplyChainService.Application.Features.Shipments.Dispatch.InventoryMovementMapper.Instant(at));
    }
}
