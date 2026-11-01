using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Diten.SupplyChainService.Tests.Common;
using Diten.SupplyChainService.Tests.Shipments.Dispatch;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;
using static Diten.SupplyChainService.Tests.Returns.Receipt.ReceiptScenario;

namespace Diten.SupplyChainService.Tests.Returns.Receipt;

/// <summary>
/// MOD-0186 R34-8 = MOD-0183 S23-16, plan r3 §4 step 4: one host, one INVENTORY. The shipment is dispatched by the real
/// MOD-0183 coordinator (attempt a1 refused and reversed, a2 issued), delivered, and its return is read by the real
/// <c>ReturnReferenceReader</c> over the real Shipments API — never MOD-0183's store.
/// </summary>
[Collection("Returns Mongo")]
public sealed class ReturnReceiptJointScenarioTests(ITestOutputHelper output)
{
    private static readonly string[] ShipmentPermissions = ["read", "create", "dispatch", "pod.capture", "cancel"];

    [Fact]
    public async Task R34_8_JointScenario_ReturnReadsAttemptA2_AvailableMinus30_QuarantinePlus14()
    {
        using var app = new ReturnReceiptHost();
        var s = new ReceiptScenario(app, output);
        var permissions = ReturnPermissionsAll.Concat(ShipmentPermissions.Select(p => "supplychain.shipments." + p)).ToArray();
        // The return reads the shipment through the same host's Shipments API, with the caller's token.
        using var server = new HttpMessageInvoker(app.Server.CreateHandler());
        app.Shipments.Forward = (request, ct) => server.SendAsync(request, ct);

        async Task<(int Status, JsonObject Body)> Shipment(string path, JsonObject body, string key)
        {
            using var client = app.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/shipment-bundle/shipments" + path) { Content = JsonContent.Create(body) };
            request.Headers.Add("Authorization", "Bearer " + AuthServiceToken.Mint(ReturnReceiptHost.Secret, s.Tenant, s.Actor, permissions));
            request.Headers.Add("X-Tenant-Id", s.Tenant.ToString()); request.Headers.Add("X-Legal-Entity-Id", s.LegalEntity.ToString());
            request.Headers.Add("X-Correlation-Id", s.Correlation.ToString()); request.Headers.Add("Idempotency-Key", key);
            using var response = await client.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            output.WriteLine($"POST shipments{path} => {(int)response.StatusCode} {text}");
            return ((int)response.StatusCode, JsonNode.Parse(text)!.AsObject());
        }
        JsonObject To(string status) => new() { ["targetStatus"] = status, ["occurredAt"] = "2026-10-28T08:15:00Z" };

        var created = await Shipment("", new JsonObject
        {
            ["sourceModule"] = "MOD-0141", ["sourceType"] = "SALES_ORDER", ["sourceDocumentId"] = "SO-R34-8", ["warehouseReferenceId"] = "wh-01",
            ["shipToReference"] = "CUST-1", ["plannedShipAt"] = "2026-10-28T08:00:00Z",
            ["lines"] = new JsonArray(new[] { "1", "2", "3" }.Select(l => (JsonNode)new JsonObject
            {
                ["lineNumber"] = l, ["itemId"] = Item(l), ["skuId"] = Sku(l), ["quantity"] = "10", ["uomId"] = "EA",
                ["skuLevel"] = "Lsku", ["lotId"] = "lot-" + l, ["fromLocationId"] = "loc-" + l
            }).ToArray())
        }, "create");
        Assert.Equal(201, created.Status);
        var shipment = Guid.Parse(created.Body["shipmentId"]!.GetValue<string>());
        Assert.Equal(200, (await Shipment($"/{shipment}/transition", To("Planned"), "plan")).Status);

        // (1) a1: line 2 refused → line 1 issued (T1) and reversed (R1), AVAILABLE net 0.
        var refuseLine2 = true;
        app.Inventory.Script = c => Task.FromResult(c.MovementType == "GOODS_ISSUE_SALES" && c.Line == "2" && refuseLine2
            ? FakeInventory.Error(422, "INSUFFICIENT_STOCK") : null);
        Assert.Equal(422, (await Shipment($"/{shipment}/transition", To("Dispatched"), "dispatch-1")).Status);
        var t1 = app.Inventory.Calls.Single(c => c.MovementType == "REVERSAL").Body["sourceDocumentId"]!.GetValue<string>();
        Assert.Equal(0m, app.Inventory.Net);

        // (2) a2 issues T2, T3, T4 — all different from T1; AVAILABLE −30.
        refuseLine2 = false;
        Assert.Equal(200, (await Shipment($"/{shipment}/transition", To("Dispatched"), "dispatch-2")).Status);
        Assert.Equal(-30m, app.Inventory.Net);
        foreach (var next in new[] { "InTransit", "Delivered" })
            Assert.Equal(200, (await Shipment($"/{shipment}/transition", To(next), next)).Status);
        var issues = await app.Db.GetCollection<BsonDocument>("sce_shipments").Find(new BsonDocument("_id", shipment.ToString())).SingleAsync();
        var a2 = issues["DispatchAttempt"]["Lines"].AsBsonArray.ToDictionary(l => l["LineNumber"].AsString, l => l["IssueTransactionId"].AsString);
        Assert.DoesNotContain(t1, a2.Values);

        // (3) return line 1 (4) and line 3 (10); line 3 refused → line 1 posted into QUARANTINE (C1, +4).
        var scenario = new ReceiptScenarioFor(s, shipment, permissions);
        var id = await scenario.InTransit(app, ("1", "4"), ("3", "10"));
        var refuseLine3 = true;
        app.Inventory.Script = c => Task.FromResult(c.MovementType == "CUSTOMER_RETURN" && c.Line == "3" && refuseLine3 ? FakeInventory.Error(422, "UNKNOWN_LOCATION") : null);
        var refused = await scenario.Receive(app, id, "receive-1");
        Assert.Equal(422, refused.Status);
        Assert.Equal(4m, app.Inventory.Quarantine);
        var c1 = app.Inventory.Calls.Where(c => c.MovementType == "CUSTOMER_RETURN" && c.Line == "1").ToArray();

        // (4) retry → line 1 replays C1 unchanged, line 3 posted (C2, +10).
        refuseLine3 = false;
        var done = await scenario.Receive(app, id, "receive-2");
        Assert.Equal(200, done.Status);
        var line1 = app.Inventory.Calls.Where(c => c.MovementType == "CUSTOMER_RETURN" && c.Line == "1").ToArray();
        Assert.Equal(2, line1.Length);
        Assert.Equal(c1[0].BodyJson, line1[1].BodyJson);
        Assert.Equal(-30m, app.Inventory.Net);
        Assert.Equal(14m, app.Inventory.Quarantine);
        var references = done.Body["lines"]!.AsArray().Select(l => l!["inventoryTransactionReferenceId"]!.GetValue<string>()).ToArray();
        Assert.Equal(2, references.Distinct().Count());

        // The return read attempt a2's issues (T2 for line 1, T4 for line 3), never a1's T1.
        var audit = await app.Db.GetCollection<BsonDocument>("returns_audit").Find(new BsonDocument { { "ReturnId", id.ToString() }, { "ToStatus", "Received" } }).SingleAsync();
        var read = audit["ReceiptAttempt"]["lines"].AsBsonArray.ToDictionary(l => l["ShipmentLineNumber"].AsString, l => l["IssueTransactionId"].AsString);
        Assert.Equal((a2["1"], a2["3"]), (read["1"], read["3"]));
        Assert.DoesNotContain(t1, read.Values);
    }

    /// <summary>The return helpers of <see cref="ReceiptScenario"/> for a real shipment and a token that may also read shipments.</summary>
    private sealed class ReceiptScenarioFor(ReceiptScenario s, Guid shipment, string[] permissions)
    {
        private async Task<(int Status, JsonObject Body)> Send(ReturnReceiptHost app, string path, JsonObject body, string key)
        {
            using var client = app.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/shipment-bundle/returns" + path) { Content = JsonContent.Create(body) };
            request.Headers.Add("Authorization", "Bearer " + AuthServiceToken.Mint(ReturnReceiptHost.Secret, s.Tenant, s.Actor, permissions));
            request.Headers.Add("X-Tenant-Id", s.Tenant.ToString()); request.Headers.Add("X-Legal-Entity-Id", s.LegalEntity.ToString());
            request.Headers.Add("X-Correlation-Id", s.Correlation.ToString()); request.Headers.Add("Idempotency-Key", key);
            using var response = await client.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            return ((int)response.StatusCode, JsonNode.Parse(text)!.AsObject());
        }
        public async Task<Guid> InTransit(ReturnReceiptHost app, params (string Line, string Quantity)[] lines)
        {
            var created = await Send(app, "", new JsonObject
            {
                ["shipmentId"] = shipment.ToString(), ["reasonCode"] = "damaged",
                ["lines"] = new JsonArray(lines.Select(l => (JsonNode)new JsonObject { ["shipmentLineNumber"] = l.Line, ["quantity"] = l.Quantity, ["uomId"] = "EA" }).ToArray())
            }, "return-create");
            Assert.Equal(201, created.Status);
            var id = Guid.Parse(created.Body["returnId"]!.GetValue<string>());
            foreach (var target in new[] { "Authorized", "InTransit" })
                Assert.Equal(200, (await Send(app, $"/{id}/transition", new JsonObject { ["targetStatus"] = target, ["occurredAt"] = "2026-10-29T07:00:00Z" }, target)).Status);
            return id;
        }
        public Task<(int Status, JsonObject Body)> Receive(ReturnReceiptHost app, Guid id, string key) => Send(app, $"/{id}/transition", Received(), key);
    }
}
