using System.Text.Json;
using MongoDB.Bson;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-0177-R7a — the two reads of the Vaka Detayı page (RECALL 1.6.0) on the real host, every answer validated against the contract:
/// <list type="bullet">
/// <item>getCaseCustomers: the customers of the scope's shipments with the ledger movements behind each shipment — the caller's company ONLY
/// (R7a sabotage (g): the sales-issue read without its company filter shows another company's movement), the live MVP-6 read
/// (Ok / NotFound / …, D-19), the notification (current + history) and the effective consumptions;</item>
/// <item>getCaseEffectiveness: Live until completed, then Stored — the live answer IS the stored result (one computation, R3 / R4's), and
/// totals are per SKU + base unit, never across units (D-20; R7a sabotage (e)).</item>
/// </list>
/// </summary>
public sealed class RecallCaseDetailReadTests : IClassFixture<RecallApiFactory>
{
    private readonly RecallApiFactory _factory;

    public RecallCaseDetailReadTests(RecallApiFactory factory) => _factory = factory;

    private static string Str(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    [Fact]
    public async Task Customers_are_the_scope_shipments_with_their_ledger_movement_live_read_notification_and_consumptions()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        var customer = ShipmentBundleDouble.ShipTo(w.Shipment);
        var (caseId, _) = await RecallR3.ApproveAsync(w.S, w.Fg);
        var ticari = w.S.Mint(w.S.Ticari, RecallScenario.TicariKeys);
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var (sn, notified, _) = await w.S.CommandAsync(caseId, "notifications", ticari, new Dictionary<string, object?>
        {
            ["idempotencyKey"] = RecallScenario.Key("nt"), ["customerRef"] = customer, ["notifiedOn"] = today, ["channel"] = "Phone", ["note"] = "Eczane aradi"
        }, null);
        Assert.True(sn == 201, notified.ToString());
        var (sc, declared, _) = await w.S.CommandAsync(caseId, "consumption-declarations", ticari, new Dictionary<string, object?>
        {
            ["idempotencyKey"] = RecallScenario.Key("cd"), ["customerRef"] = customer, ["skuId"] = w.S.Ledger.Sku.ToString("D"), ["skuLevel"] = "Lsku",
            ["quantity"] = 1m, ["declaredOn"] = today, ["evidence"] = "Musteri yazisi 2026/78"
        }, null);
        Assert.True(sc == 201, declared.ToString());

        var rows = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{caseId}/customers", w.S.Qa1Token, null, 200);
        var row = Assert.Single(rows.EnumerateArray());
        Assert.Equal((customer, customer), (Str(row, "customerRef"), Str(row, "label")));
        var shipment = Assert.Single(row.GetProperty("shipments").EnumerateArray());
        Assert.Equal((w.Shipment, 4m, "Ok", customer), (Str(shipment, "sourceDocumentId"), shipment.GetProperty("quantity").GetDecimal(), Str(shipment, "read"), Str(shipment, "shipToReference")));
        var movement = Assert.Single(shipment.GetProperty("movements").EnumerateArray());
        Assert.Equal(4m, movement.GetProperty("quantity").GetDecimal());
        Assert.Equal(Str(notified, "notificationId"), Str(row.GetProperty("notification"), "notificationId"));
        Assert.Single(row.GetProperty("notifications").EnumerateArray());
        Assert.Equal(Str(declared, "declarationId"), Str(Assert.Single(row.GetProperty("consumptions").EnumerateArray()), "declarationId"));

        // KR-E degrade: the shipment is re-read at every display (no cache) — MVP-6 says 404 now
        _factory.Shipments.Behaviour[w.Shipment] = ShipmentDoubleBehaviour.NotFound;
        try
        {
            var again = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{caseId}/customers", w.S.Qa1Token, null, 200);
            var line = again[0].GetProperty("shipments")[0];
            Assert.Equal("NotFound", Str(line, "read"));
            Assert.False(line.TryGetProperty("actualDeliverAt", out _));
        }
        finally
        {
            _factory.Shipments.Behaviour.TryRemove(w.Shipment, out _);
        }
    }

    [Fact]
    public async Task A_customer_shows_only_its_companys_ledger_movements_and_another_company_cannot_read_the_case()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        var (caseId, _) = await RecallR3.ApproveAsync(w.S, w.Fg);
        var otherCompany = Guid.NewGuid();
        // the same lot + document posted in ANOTHER company of the tenant (a tampered / migrated row): never part of this company's customer row
        w.CloneTransaction(w.Shipment, d =>
        {
            d["LegalEntityId"] = RecallTraceWorld.SameShape(d["LegalEntityId"], otherCompany);
            d["TransactionNumber"] = "OTHER-CO-77";
            d["BaseQuantity"] = new Decimal128(77m);
        });

        var rows = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{caseId}/customers", w.S.Qa1Token, null, 200);
        var text = rows.GetRawText();
        var movement = Assert.Single(rows[0].GetProperty("shipments")[0].GetProperty("movements").EnumerateArray());
        Assert.Equal(4m, movement.GetProperty("quantity").GetDecimal());
        Assert.DoesNotContain("OTHER-CO-77", text, StringComparison.Ordinal);

        var foreign = w.S.Mint(w.S.Qa1, RecallScenario.QaKeys, otherCompany);
        var (status, _, _) = await w.S.SendAsync(HttpMethod.Get, $"/api/recall/cases/{caseId}/customers", foreign);
        Assert.Equal(403, status);
        Assert.Equal(403, (await w.S.SendAsync(HttpMethod.Get, $"/api/recall/cases/{caseId}/effectiveness", foreign)).Status);
        Assert.Equal(403, (await w.S.SendAsync(HttpMethod.Get, $"/api/recall/cases/{caseId}/customers", w.S.Mint(w.S.Qa1, ["recall.dashboard.read"]))).Status);
    }

    [Fact]
    public async Task Effectiveness_is_live_until_completed_and_the_live_answer_is_the_result_the_completion_stores()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        var customer = ShipmentBundleDouble.ShipTo(w.Shipment);
        var (caseId, _) = await RecallR3.ApproveAsync(w.S, w.Fg);
        var ticari = w.S.Mint(w.S.Ticari, RecallScenario.TicariKeys);
        Assert.Equal(201, (await w.S.CommandAsync(caseId, "consumption-declarations", ticari, new Dictionary<string, object?>
        {
            ["idempotencyKey"] = RecallScenario.Key("cd"), ["customerRef"] = customer, ["skuId"] = w.S.Ledger.Sku.ToString("D"), ["skuLevel"] = "Lsku",
            ["quantity"] = 1m, ["declaredOn"] = DateTime.UtcNow.ToString("yyyy-MM-dd"), ["evidence"] = "Musteri yazisi 2026/79"
        }, null)).Status);

        var live = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{caseId}/effectiveness", w.S.Qa1Token, null, 200);
        Assert.Equal("Live", Str(live, "source"));
        var total = Assert.Single(live.GetProperty("totals").EnumerateArray());
        Assert.Equal((4m, 0m, 1m, 3m, 25m), (total.GetProperty("shipped").GetDecimal(), total.GetProperty("returned").GetDecimal(),
            total.GetProperty("consumed").GetDecimal(), total.GetProperty("open").GetDecimal(), total.GetProperty("percent").GetDecimal()));

        var closed = await RecallR3.CloseAsync(w.S, caseId);
        var stored = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{caseId}/effectiveness", w.S.Qa1Token, null, 200);
        Assert.Equal("Stored", Str(stored, "source"));
        Assert.Equal(closed.GetProperty("effectiveness").GetProperty("computedAtUtc").GetString(), Str(stored, "computedAtUtc"));
        foreach (var part in new[] { "rows", "totals" })
        {
            Assert.Equal(live.GetProperty(part).GetRawText(), stored.GetProperty(part).GetRawText()); // one computation: live then = stored now
        }
    }

    [Fact]
    public async Task Effectiveness_totals_are_per_SKU_and_base_unit_never_summed_across_units()
    {
        var s = new RecallScenario(_factory);
        var lot = await s.LotAsync();
        var c = await s.OpenAsync(lot);
        var id = RecallScenario.Id(c);
        var box = RecallScenario.ShipmentItem(lot, "SHP-2026-0901", "CUST-300/ADDR-1", 5m);
        box["baseUomId"] = "BOX";
        var (status, scope, _) = await s.AddItemsAsync(id, RecallScenario.ETag(c), RecallScenario.ShipmentItem(lot, "SHP-2026-0900", "CUST-300/ADDR-1", 12m), box);
        Assert.True(status == 200, scope.ToString());

        var live = await s.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{id}/effectiveness", s.Qa1Token, null, 200);
        var totals = live.GetProperty("totals").EnumerateArray().Select(t => (Str(t, "baseUomId"), t.GetProperty("shipped").GetDecimal())).OrderBy(t => t.Item1).ToList();
        Assert.Equal([("BOX", 5m), ("EA", 12m)], totals);
        Assert.Equal(2, live.GetProperty("rows").GetArrayLength());
    }
}
