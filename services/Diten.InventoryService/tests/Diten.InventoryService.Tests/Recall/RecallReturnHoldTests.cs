using System.Text.Json;
using Diten.InventoryService.Persistence.Quarantine;
using Diten.InventoryService.Persistence.Recall;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-0177-R7b D-28 (RECALL 1.7.0, CT decision A — owner step MOD-0175, read only): a linked return carries the hold MOD-0175 opened FOR THAT
/// MOVEMENT (<c>HoldOfMovementAsync</c> → the existing <c>FindHoldBySourceMovementAsync</c>). Two returns of the same lot → two holds, each link its
/// own (never "the lot's first open hold"); a link whose movement has no hold (an old record that went to AVAILABLE) → holdId / holdNumber null,
/// nothing invented.
/// </summary>
public sealed class RecallReturnHoldTests : IClassFixture<RecallApiFactory>
{
    private readonly RecallApiFactory _factory;

    public RecallReturnHoldTests(RecallApiFactory factory) => _factory = factory;

    private static string? Text(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>The hold 0175's intake opened for a movement (post-commit observer — waited for, briefly).</summary>
    private async Task<BsonDocument> HoldOfAsync(string transactionId)
    {
        var holds = _factory.Database.GetCollection<BsonDocument>(QuarantineCollections.Holds);
        for (var i = 0; i < 50; i++)
        {
            if (await holds.Find(new BsonDocument("SourceMovementId", transactionId)).FirstOrDefaultAsync() is { } hold) return hold;
            await Task.Delay(100);
        }

        throw new Xunit.Sdk.XunitException($"MOD-0175 opened no hold for {transactionId}.");
    }

    [Fact]
    public async Task Each_linked_return_carries_the_hold_of_its_own_movement_and_a_return_without_a_hold_carries_none()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        var customer = ShipmentBundleDouble.ShipTo(w.Shipment);
        var (caseId, _) = await RecallR3.ApproveAsync(w.S, w.Fg);
        var first = await w.Lots.PostOkAsync(w.Lots.Receipt("1", w.Fg, movementType: "CUSTOMER_RETURN", toStatus: "QUARANTINE"));
        var second = await w.Lots.PostOkAsync(w.Lots.Receipt("2", w.Fg, movementType: "CUSTOMER_RETURN", toStatus: "QUARANTINE"));
        var free = await w.Lots.PostOkAsync(w.Lots.Receipt("1", w.Fg, movementType: "CUSTOMER_RETURN", toStatus: "AVAILABLE"));
        var firstHold = await HoldOfAsync(first);
        var secondHold = await HoldOfAsync(second);
        Assert.NotEqual(firstHold["HoldNumber"].AsString, secondHold["HoldNumber"].AsString);

        var depo = w.S.Mint(w.S.Depo, RecallR3.DepoKeys);
        async Task<JsonElement> LinkAsync(string tx)
        {
            var key = RecallScenario.Key("ln");
            var (status, json, _) = await w.S.SendAsync(HttpMethod.Post, $"/api/recall/cases/{caseId}/return-links", depo,
                new Dictionary<string, object?> { ["idempotencyKey"] = key, ["transactionId"] = tx, ["customerRef"] = customer, ["returnReference"] = "IADE-D28" }, key);
            Assert.True(status == 201, json.ToString());
            return json;
        }

        // the link answer itself carries its hold (LinkRecallReturnHandler → WithHoldAsync)
        var linkedSecond = await LinkAsync(second);
        Assert.Equal((secondHold["_id"].ToString(), secondHold["HoldNumber"].AsString), (Text(linkedSecond, "holdId"), Text(linkedSecond, "holdNumber")));
        var linkedFirst = await LinkAsync(first);
        Assert.Equal((firstHold["_id"].ToString(), firstHold["HoldNumber"].AsString), (Text(linkedFirst, "holdId"), Text(linkedFirst, "holdNumber")));

        // the returns read: each link its own hold, no wrong match (CaseReturns validated against RECALL 1.7.0)
        var returns = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{caseId}/returns", w.S.Qa1Token, null, 200);
        var links = returns.GetProperty("links").EnumerateArray().ToDictionary(l => Text(l, "transactionId")!);
        Assert.Equal(2, links.Count);
        Assert.Equal(firstHold["HoldNumber"].AsString, Text(links[first], "holdNumber"));
        Assert.Equal(secondHold["HoldNumber"].AsString, Text(links[second], "holdNumber"));
        Assert.Equal(firstHold["_id"].ToString(), Text(links[first], "holdId"));
        Assert.Equal(secondHold["_id"].ToString(), Text(links[second], "holdId"));

        // an old link whose movement went to AVAILABLE: 0175 has no hold for it → null, never another hold of the lot (which has two open)
        await _factory.Database.GetCollection<BsonDocument>(RecallCollections.ReturnLinks)
            .UpdateOneAsync(new BsonDocument("TransactionId", first), new BsonDocument("$set", new BsonDocument("TransactionId", free)));
        var after = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{caseId}/returns", w.S.Qa1Token, null, 200);
        var old = after.GetProperty("links").EnumerateArray().Single(l => Text(l, "transactionId") == free);
        Assert.Null(Text(old, "holdId"));
        Assert.Null(Text(old, "holdNumber"));
        Assert.Equal(secondHold["HoldNumber"].AsString, Text(after.GetProperty("links").EnumerateArray().Single(l => Text(l, "transactionId") == second), "holdNumber"));
    }
}
