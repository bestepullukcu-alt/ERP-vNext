using System.Text.Json.Nodes;
using Diten.SupplyChainService.Application.Features.Shipments.Dispatch;
using Diten.SupplyChainService.Domain.Features.Shipments;
using Diten.SupplyChainService.Infrastructure.Features.Shipments;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Diten.SupplyChainService.Tests.Shipments.Dispatch;

// MOD-0183 DL-1 plan r3 §4 step 3: the INVENTORY client and the two request bodies (S23-1, S23-4, S23-5, S23-17).
public sealed class InventoryMovementClientTests
{
    private static readonly Guid Le = Guid.NewGuid();
    private static Shipment Shipment(string? lot = "lot-7") => new()
    {
        Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), LegalEntityId = Le, WarehouseReferenceId = "wh-01", Status = ShipmentStatus.Planned,
        Lines = [new ShipmentLine("1", Guid.NewGuid(), Guid.NewGuid(), "30.000", "EA", "rsv-1") { SkuLevel = "Lsku", LotId = lot, FromLocationId = "loc-A", FefoBypassReason = lot is null ? null : "customer asked for this lot" }]
    };
    private static DispatchAttempt Attempt() => new()
    {
        N = 1, State = DispatchState.InProgress, OccurredAt = new DateTimeOffset(2026, 10, 28, 8, 15, 0, TimeSpan.Zero),
        TransactionDate = new DateTimeOffset(2026, 10, 28, 8, 15, 0, TimeSpan.Zero), PostingDate = new DateTimeOffset(2026, 10, 28, 8, 15, 0, TimeSpan.Zero)
    };
    private static (InventoryMovementClient Client, FakeInventory Fake) Client(string? baseUrl = "http://gateway.invalid/")
    {
        var fake = new FakeInventory();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Inventory:ReferenceBaseUrl"] = baseUrl }).Build();
        return (new InventoryMovementClient(new HttpClient(fake), config), fake);
    }
    private static InventoryPostRequest Request(Shipment s, string body, string key) =>
        new(key, body, s.TenantId, s.LegalEntityId, Guid.NewGuid(), "Bearer caller-token");

    // S23-1: one GOODS_ISSUE_SALES with every §23.1 field; key {shipmentId}:{line}:dispatch:a{n}; LE header = the shipment's LE.
    [Fact]
    public async Task IssueCarriesEverySection231FieldAndTheShipmentLegalEntity()
    {
        var s = Shipment(); var a = Attempt(); var (client, fake) = Client();
        var body = InventoryMovementMapper.Issue(s, s.Lines[0], a);
        var key = a.IssueKey(s.Id, "1");
        Assert.Equal($"{s.Id}:1:dispatch:a1", key);
        var result = await client.PostAsync(Request(s, body, key), default);
        Assert.Equal(InventoryOutcome.Posted, result.Outcome); Assert.False(result.Replay); Assert.Equal("lot-7", result.LotId);
        var call = Assert.Single(fake.Calls);
        Assert.Equal(key, call.Key); Assert.Equal(Le.ToString(), call.LegalEntity); Assert.Equal("Bearer caller-token", call.Authorization);
        Assert.Null(call.Tenant);      // §23.1 headers only (review F-8)
        var expected = new Dictionary<string, string?>
        {
            ["idempotencyKey"] = key, ["documentGroupId"] = s.Id.ToString(), ["movementType"] = "GOODS_ISSUE_SALES",
            ["itemId"] = s.Lines[0].ItemId.ToString(), ["skuId"] = s.Lines[0].SkuId.ToString(), ["skuLevel"] = "Lsku",
            ["fromWarehouseId"] = "wh-01", ["fromLocationId"] = "loc-A", ["quantity"] = "30.000", ["uomId"] = "EA", ["lotId"] = "lot-7",
            ["fromStockStatus"] = "AVAILABLE", ["sourceModule"] = "MOD-0183", ["sourceType"] = "SHIPMENT", ["sourceDocumentId"] = s.Id.ToString(),
            ["sourceLineId"] = "1", ["transactionDate"] = "2026-10-28T08:15:00.0000000Z", ["postingDate"] = "2026-10-28T08:15:00.0000000Z",
            ["fefoBypassReason"] = "customer asked for this lot"
        };
        Assert.Equal(expected.OrderBy(p => p.Key), call.Body.Select(p => new KeyValuePair<string, string?>(p.Key, p.Value!.GetValue<string>())).OrderBy(p => p.Key));
        // The legacy reservation reference is never sent (F-10), nor serialIds or reservationId.
        Assert.DoesNotContain("rsv-1", call.BodyJson);
        // Same key, same bytes → replay with the same id.
        var replay = await client.PostAsync(Request(s, body, key), default);
        Assert.Equal((InventoryOutcome.Posted, true, result.TransactionId), (replay.Outcome, replay.Replay, replay.TransactionId));
    }

    // S23-4 / S23-5: every INVENTORY answer is its own outcome; a 403 is never "unavailable". Only INVENTORY's own 503 and a
    // 401 are definite; 500 / 502 / 504 may follow a commit and are ambiguous (review F-1).
    [Theory]
    [InlineData(422, "LOT_UNDER_RECALL", InventoryOutcome.Refused, false)]
    [InlineData(422, "LOT_REQUIRED", InventoryOutcome.Refused, false)]
    [InlineData(400, "INVALID_REQUEST", InventoryOutcome.Invalid, false)]
    [InlineData(400, "IDEMPOTENCY_KEY_REUSED", InventoryOutcome.Invalid, false)]
    [InlineData(403, "CROSS_LE_FORBIDDEN", InventoryOutcome.Forbidden, false)]
    [InlineData(403, "FORBIDDEN", InventoryOutcome.Forbidden, false)]
    [InlineData(503, "PRODUCT_MASTER_UNAVAILABLE", InventoryOutcome.Unavailable, false)]
    [InlineData(500, "INTERNAL_ERROR", InventoryOutcome.Unavailable, true)]
    [InlineData(502, "BAD_GATEWAY", InventoryOutcome.Unavailable, true)]
    [InlineData(504, "GATEWAY_TIMEOUT", InventoryOutcome.Unavailable, true)]
    [InlineData(401, "UNAUTHENTICATED", InventoryOutcome.Unavailable, false)]
    [InlineData(409, "CONCURRENT", InventoryOutcome.Unavailable, true)]
    public async Task EachInventoryStatusIsItsOwnOutcome(int status, string code, InventoryOutcome expected, bool ambiguous)
    {
        var s = Shipment(); var (client, fake) = Client();
        fake.Script = _ => Task.FromResult<HttpResponseMessage?>(FakeInventory.Error(status, code, new JsonObject { ["lotId"] = "lot-7" }));
        var result = await client.PostAsync(Request(s, InventoryMovementMapper.Issue(s, s.Lines[0], Attempt()), "k"), default);
        Assert.Equal((expected, code, ambiguous, status), (result.Outcome, result.InventoryCode, result.Ambiguous, result.HttpStatus));
        if (expected is InventoryOutcome.Refused) Assert.Equal("lot-7", result.Details!["lotId"]!.GetValue<string>());
    }

    [Fact]
    public async Task TimeoutAndConnectionLossAreAmbiguousAndAMissingBaseUrlSendsNothing()
    {
        var s = Shipment(); var body = InventoryMovementMapper.Issue(s, s.Lines[0], Attempt());
        var (client, fake) = Client();
        fake.Script = _ => Task.FromException<HttpResponseMessage?>(new TaskCanceledException("timeout"));
        Assert.Equal((InventoryOutcome.Unavailable, true), await Outcome(client.PostAsync(Request(s, body, "k"), default)));
        fake.Script = _ => Task.FromException<HttpResponseMessage?>(new HttpRequestException("reset"));
        Assert.Equal((InventoryOutcome.Unavailable, true), await Outcome(client.PostAsync(Request(s, body, "k"), default)));
        fake.Script = _ => Task.FromResult<HttpResponseMessage?>(FakeInventory.Json(201, new JsonObject { ["idempotentReplay"] = false }));
        Assert.Equal((InventoryOutcome.Unavailable, true), await Outcome(client.PostAsync(Request(s, body, "k"), default)));
        var (unconfigured, none) = Client(baseUrl: null);
        Assert.Equal((InventoryOutcome.Unavailable, false), await Outcome(unconfigured.PostAsync(Request(s, body, "k"), default)));
        Assert.Empty(none.Calls);
    }
    private static async Task<(InventoryOutcome, bool)> Outcome(Task<InventoryPostResult> task) { var r = await task; return (r.Outcome, r.Ambiguous); }

    // S23-17: the REVERSAL carries only the §23.4 fields; INVENTORY accepts it, and the same body with lotId is 400.
    [Fact]
    public async Task ReversalCarriesOnlyTheSection234Fields()
    {
        var s = Shipment(); var a = Attempt(); var (client, fake) = Client();
        var issueBody = InventoryMovementMapper.Issue(s, s.Lines[0], a);
        var issue = await client.PostAsync(Request(s, issueBody, a.IssueKey(s.Id, "1")), default);
        var key = a.ReversalKey(s.Id, "1");
        Assert.Equal($"{s.Id}:1:reversal:a1", key);
        var at = new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);
        var body = InventoryMovementMapper.Reversal(s.Id, issueBody, issue.TransactionId!, key, at);
        var fields = JsonNode.Parse(body)!.AsObject().Select(p => p.Key).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "documentGroupId", "idempotencyKey", "itemId", "movementType", "postingDate", "quantity", "skuId", "skuLevel",
            "sourceDocumentId", "sourceModule", "sourceType", "transactionDate", "uomId" }, fields);
        var node = JsonNode.Parse(body)!.AsObject();
        Assert.Equal(("REVERSAL", "MOD-0173", "REVERSAL", issue.TransactionId, "30.000"), (node["movementType"]!.GetValue<string>(), node["sourceModule"]!.GetValue<string>(),
            node["sourceType"]!.GetValue<string>(), node["sourceDocumentId"]!.GetValue<string>(), node["quantity"]!.GetValue<string>()));
        Assert.Equal(InventoryOutcome.Posted, (await client.PostAsync(Request(s, body, key), default)).Outcome);
        Assert.Equal(0m, fake.Net);
        node["lotId"] = "lot-7";
        Assert.Equal(InventoryOutcome.Invalid, (await client.PostAsync(Request(s, node.ToJsonString(), key + "x"), default)).Outcome);
    }
}
