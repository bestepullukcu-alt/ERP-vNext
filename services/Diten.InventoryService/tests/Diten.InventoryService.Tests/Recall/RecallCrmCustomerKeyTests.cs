using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// MOD-0177-FU01 AC-1, AC-2, AC-3 (pack §8.2, §8.3) on the real host: the customer key order customerRef → shipToReference → DOC, the "D" GUID
/// guard of a ship-to, the canonical lower-case CRM key and the case-insensitive match / fingerprint of the record commands.
/// </summary>
public sealed class RecallCrmCustomerKeyTests : IClassFixture<RecallApiFactory>
{
    private readonly RecallApiFactory _factory;

    public RecallCrmCustomerKeyTests(RecallApiFactory factory) => _factory = factory;

    private static JsonElement ShipmentNode(JsonElement trace, string shipment)
        => Assert.Single(RecallTraceWorld.Nodes(trace), n => n.GetProperty("kind").GetString() == "Shipment" && n.GetProperty("nodeKey").GetString()!.EndsWith(shipment));

    [Fact]
    public async Task CustomerRef_wins_over_shipTo()
    {
        var w = await CrmWorld.CreateAsync(_factory);
        var shipment = await w.SellAsync(CrmWorld.A, shipTo: "CUST-100/ADDR-2");
        var trace = await w.GetAsync($"/api/recall/trace?rootLotId={w.Fg}&direction=Forward");
        var node = ShipmentNode(trace, shipment);
        Assert.Equal(CrmWorld.A.ToString("D"), CrmWorld.Str(node, "customerRef"));
        Assert.Equal("Crm", CrmWorld.Str(CrmWorld.Customer(node), "source"));

        var (caseId, _) = await w.ActiveAsync();
        var item = Assert.Single(w.ScopeDocs(caseId), d => d["Kind"] == "Shipment" && d["SourceDocumentId"] == shipment);
        Assert.Equal((CrmWorld.A.ToString("D"), "Crm"), (item["CustomerRef"].AsString, item["CustomerRefSource"].AsString));
    }

    [Fact]
    public async Task Fallback_order_shipTo_then_DOC_for_every_unusable_customerRef()
    {
        var w = await CrmWorld.CreateAsync(_factory);
        var absent = await w.SellRawAsync(null, shipTo: "CUST-A/1");                    // (a) the 3.4.0 body
        var empty = await w.SellRawAsync("\"\"", shipTo: "CUST-B/1");                   // (b)
        var text = await w.SellRawAsync("\"not-a-guid\"", shipTo: "CUST-C/1");          // (c)
        var zero = await w.SellRawAsync("\"" + Guid.Empty.ToString("D") + "\"", shipTo: "CUST-D/1"); // (d)
        var number = await w.SellRawAsync("42", shipTo: "CUST-E/1");                    // (e)
        var noShipTo = await w.SellRawAsync(null, shipTo: "");                          // (f)
        var forbidden = await w.SellAsync(CrmWorld.A);                                  // (g) the read fails: no customerRef is read
        var missing = await w.SellAsync(CrmWorld.A);
        var slow = await w.SellAsync(CrmWorld.A);
        var guidShipTo = await w.SellRawAsync(null, shipTo: Guid.NewGuid().ToString("D")); // (h)
        _factory.Shipments.Behaviour[forbidden] = ShipmentDoubleBehaviour.Forbidden;
        _factory.Shipments.Behaviour[missing] = ShipmentDoubleBehaviour.NotFound;
        _factory.Shipments.Behaviour[slow] = ShipmentDoubleBehaviour.Timeout;
        try
        {
            var trace = await w.GetAsync($"/api/recall/trace?rootLotId={w.Fg}&direction=Forward");
            foreach (var (shipment, shipTo) in new[] { (absent, "CUST-A/1"), (empty, "CUST-B/1"), (text, "CUST-C/1"), (zero, "CUST-D/1"), (number, "CUST-E/1") })
            {
                var node = ShipmentNode(trace, shipment);
                Assert.Equal(shipTo, CrmWorld.Str(node, "customerRef"));
                Assert.Equal(("ShipTo", "NoCrm"), (CrmWorld.Str(CrmWorld.Customer(node), "source"), CrmWorld.Str(CrmWorld.Customer(node), "state")));
            }

            foreach (var shipment in new[] { noShipTo, forbidden, missing, slow, guidShipTo })
            {
                var node = ShipmentNode(trace, shipment);
                Assert.Equal("DOC:" + shipment, CrmWorld.Str(node, "customerRef"));
                Assert.Equal("Document", CrmWorld.Str(CrmWorld.Customer(node), "source"));
            }

            // (a) the 1.9.0 members of the node are unchanged: only "customer" is new
            var plain = ShipmentNode(trace, absent);
            string[] v190 = ["nodeKey", "parentKey", "level", "side", "kind", "lot", "item", "untracked", "warehouseId", "locationId", "stockStatus", "quantity", "baseUomId",
                "movement", "shipment", "customerRef", "serials", "gapCode", "count", "text"];
            Assert.Empty(plain.EnumerateObject().Select(p => p.Name).Where(n => n != "customer").Except(v190));
            Assert.Equal("CUST-A/1", plain.GetProperty("shipment").GetProperty("shipToReference").GetString());
            // the unusable values are logged without the value; the GUID-shaped ship-to too
            var lines = _factory.Logs.Lines.ToList();
            Assert.Contains(lines, l => l.Contains("recall.shipment.customer_ref_invalid " + text, StringComparison.Ordinal));
            Assert.Contains(lines, l => l.Contains("recall.shipment.ship_to_guid_shaped " + guidShipTo, StringComparison.Ordinal));
            Assert.DoesNotContain(lines, l => l.Contains("not-a-guid", StringComparison.Ordinal));
        }
        finally
        {
            foreach (var s in new[] { forbidden, missing, slow }) _factory.Shipments.Behaviour.TryRemove(s, out _);
        }
    }

    [Fact]
    public async Task Case_insensitive_key()
    {
        var w = await CrmWorld.CreateAsync(_factory);
        w.FirstSaleIs(CrmWorld.A);
        await w.SellAsync(CrmWorld.A, upper: true);
        await w.SellAsync(CrmWorld.A);
        var (caseId, _) = await w.ActiveAsync();
        var rows = await w.CustomersAsync(caseId);
        var row = Assert.Single(rows.EnumerateArray());
        Assert.Equal(CrmWorld.A.ToString("D"), CrmWorld.Str(row, "customerRef"));

        // (b) recordNotification with the account in upper case → 201, stored lower case; (c) the same key in lower case → the replay
        var ticari = w.S.Mint(w.S.Ticari, RecallScenario.TicariKeys);
        var key = RecallScenario.Key("nt");
        var (s1, first, _) = await w.S.CommandAsync(caseId, "notifications", ticari, w.Notify(CrmWorld.A.ToString("D").ToUpperInvariant(), key), null);
        Assert.True(s1 == 201, first.ToString());
        Assert.Equal(CrmWorld.A.ToString("D"), CrmWorld.Str(first, "customerRef"));
        var (s2, again, _) = await w.S.CommandAsync(caseId, "notifications", ticari, w.Notify(CrmWorld.A.ToString("D"), key), null);
        Assert.True(s2 == 200, again.ToString());
        Assert.Equal(CrmWorld.Str(first, "notificationId"), CrmWorld.Str(again, "notificationId"));

        // (d) a padded upper-case account declares consumption under the canonical key
        var (s3, declared, _) = await w.S.CommandAsync(caseId, "consumption-declarations", ticari, w.Consume("  " + CrmWorld.A.ToString("D").ToUpperInvariant() + " "), null);
        Assert.True(s3 == 201, declared.ToString());
        Assert.Equal(CrmWorld.A.ToString("D"), CrmWorld.Str(declared, "customerRef"));

        // AC-16: the two case detail reads are validated against RECALL 1.10.0 by the suite's contract handler
        await w.GetAsync($"/api/recall/cases/{caseId}/effectiveness");
        Assert.Contains(RecallContractValidatingHandler.Validated, v => v.Key.StartsWith("GET ", StringComparison.Ordinal) && v.Key.Contains("/customers", StringComparison.Ordinal)
                                                                        && v.Key.EndsWith("recall:CustomerRow[]", StringComparison.Ordinal) && v.Value > 0);
        Assert.Contains(RecallContractValidatingHandler.Validated, v => v.Key.Contains("/effectiveness", StringComparison.Ordinal) && v.Key.EndsWith("recall:Effectiveness", StringComparison.Ordinal)
                                                                        && v.Value > 0);
    }
}

/// <summary>
/// MOD-0177-FU01 AC-4, AC-5 (pack §8.4): the three record commands accept a CRM key (canonical in the record, the event and the audit row);
/// a legacy key keeps its exact, case-sensitive meaning and its fingerprint, and the stored legacy records are never rewritten.
/// </summary>
public sealed class RecallCrmRecordTests : IClassFixture<RecallApiFactory>
{
    private readonly RecallApiFactory _factory;

    public RecallCrmRecordTests(RecallApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Records_accept_crm_key()
    {
        var w = await CrmWorld.CreateAsync(_factory);
        w.FirstSaleIs(CrmWorld.A);
        await w.SellAsync(CrmWorld.A, quantity: "6");
        var (caseId, _) = await w.ActiveAsync();
        var a = CrmWorld.A.ToString("D");
        var ticari = w.S.Mint(w.S.Ticari, RecallScenario.TicariKeys);
        var depo = w.S.Mint(w.S.Depo, RecallR3.DepoKeys);
        Assert.Equal(201, (await w.S.CommandAsync(caseId, "notifications", ticari, w.Notify(a), null)).Status);
        Assert.Equal(201, (await w.S.CommandAsync(caseId, "consumption-declarations", ticari, w.Consume(a, 2m), null)).Status);
        var ret = await w.ReturnAsync("3");
        var (sl, linked, _) = await w.S.CommandAsync(caseId, "return-links", depo, w.Link(ret, a), null);
        Assert.True(sl == 201, linked.ToString());
        Assert.Equal(("Crm", "Resolved", CrmWorld.NameA), (CrmWorld.Str(linked.GetProperty("customer"), "source"), CrmWorld.Str(linked.GetProperty("customer"), "state"),
            CrmWorld.Str(linked.GetProperty("customer"), "name")));

        var effectiveness = await w.GetAsync($"/api/recall/cases/{caseId}/effectiveness");
        var row = Assert.Single(effectiveness.GetProperty("rows").EnumerateArray(), r => CrmWorld.Str(r, "customerRef") == a);
        Assert.Equal((3m, 2m), (row.GetProperty("returned").GetDecimal(), row.GetProperty("consumed").GetDecimal()));
        Assert.All(w.S.Audit("recall.customer.notified").Concat(w.S.Audit("recall.consumption.declared")).Concat(w.S.Audit("recall.return.linked")),
            d => Assert.Contains(a, d.ToJson(), StringComparison.Ordinal));

        // an account outside the scope → 404 RECALL_UNKNOWN_CUSTOMER, nothing written
        var counts = (w.Count("recall_notifications"), w.Count("recall_consumption_declarations"), w.Count("recall_return_links"));
        var stranger = Guid.NewGuid().ToString("D");
        var (n404, nb, _) = await w.S.CommandAsync(caseId, "notifications", ticari, w.Notify(stranger), null);
        Assert.Equal((404, "RECALL_UNKNOWN_CUSTOMER"), (n404, RecallScenario.Code(nb)));
        Assert.Equal(404, (await w.S.CommandAsync(caseId, "consumption-declarations", ticari, w.Consume(stranger), null)).Status);
        Assert.Equal(404, (await w.S.CommandAsync(caseId, "return-links", depo, w.Link(await w.ReturnAsync(), stranger), null)).Status);
        Assert.Equal(counts, (w.Count("recall_notifications"), w.Count("recall_consumption_declarations"), w.Count("recall_return_links")));
    }

    [Fact]
    public async Task Legacy_records_unchanged()
    {
        var w = await CrmWorld.CreateAsync(_factory);
        await w.SellRawAsync(null, shipTo: "CUST-100/ADDR-2");
        var (caseId, _) = await w.ActiveAsync();
        var ticari = w.S.Mint(w.S.Ticari, RecallScenario.TicariKeys);
        var items = _factory.Database.GetCollection<BsonDocument>("recall_scope_items");
        // the case written before FU01: no source on its items
        foreach (var d in w.ScopeDocs(caseId)) items.UpdateOne(new BsonDocument("_id", d["_id"]), new BsonDocument("$unset", new BsonDocument("CustomerRefSource", "")));
        Assert.Equal(201, (await w.S.CommandAsync(caseId, "notifications", ticari, w.Notify("CUST-100/ADDR-2"), null)).Status);
        var before = w.S.Persisted("recall_notifications").Select(d => d.ToJson()).ToList();

        // (b) the exact legacy key → 201 (no CRM call); another case → 404 (a legacy key stays case-sensitive)
        _factory.Crm.ResetCounts();
        var (s1, body1, _) = await w.S.CommandAsync(caseId, "notifications", ticari, w.Notify("CUST-100/ADDR-2"), null);
        Assert.True(s1 == 201, body1.ToString());
        Assert.Equal("CUST-100/ADDR-2", CrmWorld.Str(body1, "customerRef"));
        Assert.Equal(0, _factory.Crm.Calls);
        Assert.Equal(404, (await w.S.CommandAsync(caseId, "notifications", ticari, w.Notify("cust-100/addr-2"), null)).Status);

        // (a) the reads leave the stored legacy records byte-equal
        await w.CustomersAsync(caseId);
        await w.GetAsync($"/api/recall/cases/{caseId}/effectiveness");
        Assert.All(before, b => Assert.Contains(b, w.S.Persisted("recall_notifications").Select(d => d.ToJson())));

        // (c) the fingerprint of a non-"D" legacy key is the pre-FU01 one: a replay of the same key + body answers 200
        var key = RecallScenario.Key("nt");
        Assert.Equal(201, (await w.S.CommandAsync(caseId, "notifications", ticari, w.Notify("CUST-100/ADDR-2", key), null)).Status);
        Assert.Equal(200, (await w.S.CommandAsync(caseId, "notifications", ticari, w.Notify("CUST-100/ADDR-2", key), null)).Status);
        var record = Assert.Single(w.S.Persisted("recall_notifications"), d => d["IdempotencyKey"] == key);
        Assert.Equal(PreFu01Fingerprint(caseId, "CUST-100/ADDR-2"), record["RequestFingerprint"].AsString);

        // (c2) "D"-shaped legacy keys (a pre-FU01 hand entry): a lower-case one keeps its fingerprint; a record written before FU01 with an
        // upper-case one is answered 400 on the same key + body (the accepted difference of §8.3 — AC-20 (f) proves it does not exist in data)
        var lower = Guid.NewGuid().ToString("D");
        var upper = Guid.NewGuid().ToString("D").ToUpperInvariant();
        var template = w.ScopeDocs(caseId).First(d => d["Kind"] == "Shipment" && d["CustomerRef"] == "CUST-100/ADDR-2");
        foreach (var legacy in new[] { lower, upper })
        {
            var clone = template.DeepClone().AsBsonDocument;
            clone["_id"] = template["_id"].IsString ? Guid.NewGuid().ToString("D") : new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard);
            clone["NodeKey"] = $"Shipment:{w.Fg}:LEGACY-{legacy}";
            clone["SourceDocumentId"] = "LEGACY-" + legacy;
            clone["CustomerRef"] = legacy;
            items.InsertOne(clone);
        }

        var lowerKey = RecallScenario.Key("nt");
        Assert.Equal(201, (await w.S.CommandAsync(caseId, "notifications", ticari, w.Notify(lower, lowerKey), null)).Status);
        Assert.Equal(PreFu01Fingerprint(caseId, lower), Assert.Single(w.S.Persisted("recall_notifications"), d => d["IdempotencyKey"] == lowerKey)["RequestFingerprint"].AsString);
        var upperKey = RecallScenario.Key("nt");
        var written = Assert.Single(w.S.Persisted("recall_notifications"), d => d["IdempotencyKey"] == lowerKey).DeepClone().AsBsonDocument;
        written["_id"] = written["_id"].IsString ? Guid.NewGuid().ToString("D") : new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard);
        written["IdempotencyKey"] = upperKey;
        written["CustomerRef"] = upper;
        written["RequestFingerprint"] = PreFu01Fingerprint(caseId, upper);
        _factory.Database.GetCollection<BsonDocument>("recall_notifications").InsertOne(written);
        var (s400, b400, _) = await w.S.CommandAsync(caseId, "notifications", ticari, w.Notify(upper, upperKey), null);
        Assert.Equal((400, "IDEMPOTENCY_KEY_REUSED"), (s400, RecallScenario.Code(b400)));
    }

    /// <summary>The fingerprint the pre-FU01 handler wrote (RecordHandlers.cs before FU01: the trimmed customerRef as it came), today's date.</summary>
    private static string PreFu01Fingerprint(string caseId, string customer)
        => Diten.InventoryService.Application.Features.Recall.Services.RecallSupport.Fingerprint(("case", caseId), ("customerRef", customer),
            ("notifiedOn", DateTime.UtcNow.ToString("yyyy-MM-dd")), ("channel", "Phone"), ("note", null), ("supersedesId", null));
}
