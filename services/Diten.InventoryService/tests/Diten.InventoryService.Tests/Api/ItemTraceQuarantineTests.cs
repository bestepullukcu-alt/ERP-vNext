using System.Net.Http.Json;
using System.Text.Json;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// WP-0174-ITEM — TRACE-BUNDLE 1.3.0 (<c>Item</c> level, 422 <c>PRODUCT_MASTER_DATA_INCOMPLETE</c> / <c>SKU_LEVEL_NOT_STOCKABLE</c> on
/// createLot). A lot-controlled material item (<c>skuId == itemId</c>, PRODUCT-MASTER 1.1 §8.5 Q-1) gets a lot and its receipt posts
/// with that lot; before this slice the level was a 400 and the receipt could not post at all.
/// </summary>
public sealed class ItemLevelLotTests : IClassFixture<TraceApiFactory>
{
    private readonly TraceApiFactory _factory;

    public ItemLevelLotTests(TraceApiFactory factory) => _factory = factory;

    private static Dictionary<string, object?> LotBody(LotLedgerScenario s, string? lotNumber = null) => new()
    {
        ["idempotencyKey"] = "lot-" + Guid.NewGuid().ToString("N"),
        ["itemId"] = s.Ledger.Item.ToString("D"),
        ["lotNumber"] = lotNumber ?? TraceScenario.LotNo("IT"),
        ["skuId"] = s.Ledger.Sku.ToString("D"),
        ["skuLevel"] = WireNames(s.Ledger.Level),
        ["expiryDate"] = "2028-06-30T00:00:00Z"
    };

    private static string WireNames(SkuLevel level) => Diten.InventoryService.Domain.Rules.WireNames.ToWire(level);

    private static async Task<(int Status, JsonElement Body)> CreateAsync(LotLedgerScenario s, Dictionary<string, object?> body)
    {
        using var response = await s.Send(HttpMethod.Post, "/api/trace/lots", body,
            r => r.Headers.TryAddWithoutValidation("Idempotency-Key", (string)body["idempotencyKey"]!));
        var text = await response.Content.ReadAsStringAsync();
        return ((int)response.StatusCode, JsonDocument.Parse(text).RootElement.Clone());
    }

    private long LotCount(LotLedgerScenario s)
        => _factory.Collection("trace_lots").CountDocuments(new BsonDocument("TenantId", s.Ledger.Tenant.ToString()));

    [Fact]
    public async Task A_lot_controlled_Item_gets_a_lot_and_its_receipt_posts_with_that_lot()
    {
        var s = new LotLedgerScenario(_factory, lotControlled: true, level: SkuLevel.Item);
        Assert.Equal(s.Ledger.Item, s.Ledger.Sku);

        var (status, lot) = await CreateAsync(s, LotBody(s));
        Assert.True(status == 201, $"createLot: {status} {lot}");
        Assert.Equal("Item", lot.GetProperty("skuLevel").GetString());
        Assert.Equal(s.Ledger.Item.ToString("D"), lot.GetProperty("skuId").GetString());
        Assert.Equal("Lot", lot.GetProperty("trackingMode").GetString()); // Item is never serial-tracked
        var lotId = lot.GetProperty("lotId").GetString()!;

        var transactionId = await s.PostOkAsync(s.Receipt("120", lotId));
        Assert.False(string.IsNullOrEmpty(transactionId));
        Assert.Equal(120m, s.LotPositionOnHand(lotId));

        // the lot is found by the Item level on every read
        var list = await s.GetTraceAsync($"/api/trace/lots?skuId={s.Ledger.Sku:D}&skuLevel=Item&page=1&pageSize=10");
        Assert.Contains(list.GetProperty("items").EnumerateArray(), l => l.GetProperty("lotId").GetString() == lotId);
    }

    [Fact]
    public async Task An_Item_receipt_without_a_lot_is_still_refused_because_the_item_is_lot_controlled()
    {
        var s = new LotLedgerScenario(_factory, lotControlled: true, level: SkuLevel.Item);
        var body = s.Receipt("5", null);
        body.Remove("lotId");
        var json = await s.PostAsync(body, 422);
        Assert.Equal("LOT_REQUIRED", json.GetProperty("error").GetProperty("code").GetString());
    }

    [Theory]
    [InlineData(SkuLevel.Item)]
    [InlineData(SkuLevel.Lsku)]
    public async Task An_unknown_lot_flag_refuses_lot_creation_with_422_data_incomplete_and_saves_nothing(SkuLevel level)
    {
        var s = new LotLedgerScenario(_factory, lotControlled: true, level: level);
        _factory.Skus.SetFlags(s.Ledger.Sku, lotControlled: null, serialControlled: false);
        var (status, body) = await CreateAsync(s, LotBody(s));
        Assert.True(status == 422, $"{status} {body}");
        Assert.Equal("PRODUCT_MASTER_DATA_INCOMPLETE", body.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(0, LotCount(s));
    }

    [Theory]
    [InlineData(SkuLevel.Item)]
    [InlineData(SkuLevel.Lsku)]
    public async Task A_stockable_false_sku_gets_no_lot(SkuLevel level)
    {
        var s = new LotLedgerScenario(_factory, lotControlled: true, level: level);
        _factory.Skus.SetStockable(s.Ledger.Sku, false);
        var (status, body) = await CreateAsync(s, LotBody(s));
        Assert.True(status == 422, $"{status} {body}");
        Assert.Equal("SKU_LEVEL_NOT_STOCKABLE", body.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(0, LotCount(s));
    }

    [Fact]
    public async Task A_stockable_true_or_unstated_sku_gets_its_lot()
    {
        var stated = new LotLedgerScenario(_factory, lotControlled: true, level: SkuLevel.Item);
        _factory.Skus.SetStockable(stated.Ledger.Sku, true);
        Assert.Equal(201, (await CreateAsync(stated, LotBody(stated))).Status);

        var unstated = new LotLedgerScenario(_factory, lotControlled: true, level: SkuLevel.Item); // ASSUMPTION-0174-ITEM-01
        Assert.Equal(201, (await CreateAsync(unstated, LotBody(unstated))).Status);
    }

    [Fact]
    public async Task The_GS1_check_at_Item_level_skips_the_GTIN_because_the_material_has_none()
    {
        var s = new LotLedgerScenario(_factory, lotControlled: true, level: SkuLevel.Item);
        var lotNumber = TraceScenario.LotNo("G");
        var (_, lot) = await CreateAsync(s, LotBody(s, lotNumber));
        var lotId = lot.GetProperty("lotId").GetString()!;
        using var response = await s.Send(HttpMethod.Post, "/api/trace/serials/gs1-check", new Dictionary<string, object?>
        {
            ["code"] = $"(01){TraceScenario.Gtin}(21)SN0001(17)280630(10){lotNumber}",
            ["skuId"] = s.Ledger.Sku.ToString("D"),
            ["skuLevel"] = "Item",
            ["lotId"] = lotId
        });
        var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.True((int)response.StatusCode == 200, result.ToString());
        Assert.True(result.GetProperty("accepted").GetBoolean());
        Assert.Equal(["Format:Ok", "Gtin:Skipped", "Lot:Ok", "Expiry:Ok"],
            result.GetProperty("checks").EnumerateArray().Select(c => c.GetProperty("field").GetString() + ":" + c.GetProperty("result").GetString()));
        Assert.Equal("PRODUCT_MASTER_GTIN_NULL", result.GetProperty("gtinSkippedReason").GetString());
    }
}

/// <summary>
/// WP-0174-ITEM (O-PM1-3; INVENTORY-BUNDLE 1.2.1): PRODUCT-MASTER <c>stockable: false</c> refuses every movement with 422
/// <c>SKU_LEVEL_NOT_STOCKABLE</c> and writes nothing; <c>true</c> or not stated keeps the PM1 rules (ASSUMPTION-0174-ITEM-01); a
/// REVERSAL is never checked against PRODUCT-MASTER (CT decision O-PM1-2).
/// </summary>
public sealed class StockableSkuTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public StockableSkuTests(InventoryApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData(SkuLevel.Lsku, "OPENING_BALANCE")]
    [InlineData(SkuLevel.Item, "OPENING_BALANCE")]
    [InlineData(SkuLevel.Item, "GOODS_RECEIPT_PO")]
    public async Task A_stockable_false_sku_takes_no_movement_and_nothing_is_written(SkuLevel level, string movementType)
    {
        var s = new InventoryScenario(_factory, level, lotControlled: false);
        _factory.Products.SetStockable(s.Sku, false);
        var refused = await s.PostExpectingAsync(s.Receipt("10", movementType), 422);
        Assert.Equal("SKU_LEVEL_NOT_STOCKABLE", refused.GetProperty("error").GetProperty("code").GetString());
        Assert.Empty(s.Persisted("inventory_transactions"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(null)]
    public async Task A_stockable_true_or_unstated_sku_posts(bool? stockable)
    {
        var s = new InventoryScenario(_factory, SkuLevel.Item, lotControlled: false);
        if (stockable is not null) _factory.Products.SetStockable(s.Sku, stockable);
        await s.PostExpectingAsync(s.Receipt("10"), 201);
    }

    [Fact]
    public async Task An_outbound_movement_of_a_sku_that_became_unstockable_is_refused_but_its_reversal_posts()
    {
        var s = new InventoryScenario(_factory, SkuLevel.Item, lotControlled: false);
        var receipt = s.Receipt("10");
        var posted = await s.PostExpectingAsync(receipt, 201);
        _factory.Products.SetStockable(s.Sku, false);

        var issue = await s.PostExpectingAsync(s.Issue("4"), 422);
        Assert.Equal("SKU_LEVEL_NOT_STOCKABLE", issue.GetProperty("error").GetProperty("code").GetString());
        await s.PostExpectingAsync(s.Reversal(receipt, posted.GetProperty("transactionId").GetString()!), 201);
        Assert.Equal(0m, await s.OnHandAsync());
    }
}

/// <summary>
/// WP-0174-ITEM — QUARANTINE-BUNDLE 1.1.0 (<c>Item</c> on <c>SkuLevel</c>). Item-level stock is quarantined (intake and manual),
/// blocked, decided and released; every body is validated against the 1.1.0 file by the contract client (before this slice the
/// hold list violated <c>HoldList</c>). The <c>qd-v1</c> subject of an Item decision has the SAME field list and carries the item
/// as its own SKU.
/// </summary>
public sealed class ItemQuarantineTests : IClassFixture<QuarantineApiFactory>
{
    private readonly QuarantineApiFactory _factory;

    public ItemQuarantineTests(QuarantineApiFactory factory) => _factory = factory;

    private static string Id(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    [Fact]
    public async Task An_Item_receipt_into_quarantine_is_listed_decided_on_the_qd_v1_subject_approved_and_released()
    {
        var s = new QuarantineScenario(_factory, level: SkuLevel.Item);
        var lot = await s.ReleasedLotAsync();
        var (_, hold) = await s.HoldAsync("40", lot);
        Assert.Equal("Item", hold.GetProperty("item").GetProperty("skuLevel").GetString());
        Assert.Equal(s.Ledger.Item.ToString("D"), hold.GetProperty("item").GetProperty("skuId").GetString());

        // the list and the detail are 1.1.0 bodies (the contract client fails the call otherwise)
        var list = await s.ExpectAsync(HttpMethod.Get, "/api/quarantine/holds?page=1&pageSize=50", null, 200);
        Assert.Contains(list.GetProperty("items").EnumerateArray(), h => Id(h, "holdId") == Id(hold, "holdId") && Id(h.GetProperty("item"), "skuLevel") == "Item");

        var draft = await s.DecideAsync(Id(hold, "holdId"), [QuarantineScenario.Line(1, "Release", 40m)]);
        var decisionId = Id(draft, "decisionId");
        var subject = await s.ExpectAsync(HttpMethod.Get, $"/api/quarantine/esign-subjects/quarantine.decision/{decisionId}", null, 200);
        var fields = subject.GetProperty("fields").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.ValueKind == JsonValueKind.Null ? null : p.Value.GetString());
        Assert.Equal(QuarantineSignableSubjectBuilder.FieldNames.OrderBy(n => n, StringComparer.Ordinal), fields.Keys.OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal("qd-v1", fields["subjectVersion"]);
        Assert.Equal(s.Ledger.Item.ToString("D"), fields["itemId"]);
        Assert.Equal(s.Ledger.Item.ToString("D"), fields["skuId"]);
        Assert.Equal(lot, fields["lotId"]);
        Assert.Equal("40", fields["line1.quantity"]);
        Assert.Equal("AVAILABLE", fields["line1.toStockStatus"]);

        await s.SubmitAsync(decisionId);
        var applied = await s.OutcomeAsync(decisionId);
        Assert.Equal("Applied", Id(applied, "state"));
        Assert.Equal(40m, await s.OnHandAsync("AVAILABLE"));
        Assert.Equal(0m, await s.OnHandAsync("QUARANTINE"));
        Assert.Equal("Decided", Id(await s.GetHoldAsync(Id(hold, "holdId")), "status"));
    }

    /// <summary>The PM1 measurement (O-PM1-1): an Item receipt into QUARANTINE opens a hold whose list / detail bodies broke HoldList.</summary>
    [Fact]
    public async Task A_non_lot_Item_receipt_into_quarantine_has_a_hold_whose_list_and_detail_are_1_1_0_bodies()
    {
        var s = new QuarantineScenario(_factory, lotControlled: false, level: SkuLevel.Item);
        var transactionId = await s.ReceiveAsync("12", null, "QUARANTINE");
        var hold = await s.HoldBySourceAsync(transactionId); // GET /holds/{id} through the contract client
        Assert.Equal("Item", hold.GetProperty("item").GetProperty("skuLevel").GetString());
        var list = await s.ExpectAsync(HttpMethod.Get, "/api/quarantine/holds?page=1&pageSize=50", null, 200);
        Assert.Contains(list.GetProperty("items").EnumerateArray(), h => Id(h, "holdId") == Id(hold, "holdId"));
    }

    [Fact]
    public async Task Item_level_stock_is_quarantined_and_blocked_manually()
    {
        var s = new QuarantineScenario(_factory, level: SkuLevel.Item);
        var lot = await s.ReleasedLotAsync();
        await s.ReceiveAsync("30", lot, "AVAILABLE");
        object Bucket(decimal quantity) => new
        {
            itemId = s.Ledger.Item, skuId = s.Ledger.Sku, skuLevel = "Item", warehouseId = s.Ledger.Warehouse, locationId = s.Ledger.LocationA,
            quantity, lotId = lot
        };

        var key = QuarantineScenario.Key("man");
        var holds = await s.ExpectAsync(HttpMethod.Post, "/api/quarantine/holds",
            new { mode = "Manual", buckets = new[] { Bucket(10m) }, reason = QuarantineScenario.Reason, idempotencyKey = key }, 201, key);
        Assert.Equal("Item", holds[0].GetProperty("item").GetProperty("skuLevel").GetString());

        var blockKey = QuarantineScenario.Key("blk");
        var blocked = await s.ExpectAsync(HttpMethod.Post, "/api/quarantine/blocked",
            new { buckets = new[] { Bucket(5m) }, blockReasonCode = "QUALITY_REJECT", reason = QuarantineScenario.Reason, idempotencyKey = blockKey }, 201, blockKey);
        Assert.Equal("Item", blocked[0].GetProperty("item").GetProperty("skuLevel").GetString());
        Assert.Equal(10m, await s.OnHandAsync("QUARANTINE"));
        Assert.Equal(5m, await s.OnHandAsync("BLOCKED"));
        Assert.Equal(15m, await s.OnHandAsync("AVAILABLE"));
    }
}

/// <summary>
/// WP-0174-ITEM — FU-0175-Q5-01 <c>updateHold</c> (PATCH /holds/{holdId}, key <c>quarantine.holds.review</c>, If-Match): the analysis
/// note and attachment references while the hold is open; stale version → 409 <c>QUARANTINE_CONFLICT</c>; decided → 409
/// <c>QUARANTINE_HOLD_CLOSED</c>; one <c>inventory_audit</c> row with the before / after values per change; the idempotency rule of
/// the contract; since QUARANTINE 1.2.0 (WP-MVP1-FUB2, FU-0175-ITEM-01) also one NoteUpdated history row per change — see HoldNoteLockTests.
/// </summary>
public sealed class UpdateHoldTests : IClassFixture<QuarantineApiFactory>
{
    private readonly QuarantineApiFactory _factory;

    public UpdateHoldTests(QuarantineApiFactory factory) => _factory = factory;

    private static string Id(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    private async Task<(QuarantineScenario S, string HoldId, string ETag)> OpenHoldAsync()
    {
        var s = new QuarantineScenario(_factory);
        var lot = await s.ReleasedLotAsync();
        var (_, hold) = await s.HoldAsync("25", lot);
        var holdId = Id(hold, "holdId");
        var (_, _, raw) = await s.SendAsync(HttpMethod.Get, $"/api/quarantine/holds/{holdId}");
        return (s, holdId, raw.Headers.ETag!.ToString());
    }

    private List<BsonDocument> Audit(QuarantineScenario s, string holdId)
        => _factory.Collection("inventory_audit")
            .Find(new BsonDocument { ["TenantId"] = s.Tenant.ToString(), ["Action"] = "QUARANTINE_HOLD_UPDATED", ["HoldId"] = holdId }).ToList();

    private int History(QuarantineScenario s) => s.Ledger.Persisted("quarantine_history").Count;

    [Fact]
    public async Task The_note_is_saved_with_a_new_version_one_audit_row_and_one_history_row()
    {
        var (s, holdId, etag) = await OpenHoldAsync();
        var historyBefore = History(s);
        var key = QuarantineScenario.Key("upd");
        var (status, body, raw) = await s.SendAsync(HttpMethod.Patch, $"/api/quarantine/holds/{holdId}",
            new { analysisNote = "  pH 6,8 — spesifikasyon içinde  ", attachments = new[] { new { name = "COA.pdf", dmsRef = "DMS-77" } }, idempotencyKey = key }, key, etag);
        Assert.True(status == 200, $"{status} {body}");
        Assert.Equal("pH 6,8 — spesifikasyon içinde", Id(body, "analysisNote"));
        Assert.Equal("DMS-77", Id(body.GetProperty("attachments")[0], "dmsRef"));
        var version = body.GetProperty("version").GetInt32();
        Assert.Equal($"W/\"{version}\"", raw.Headers.ETag!.ToString());
        Assert.NotEqual(etag, raw.Headers.ETag!.ToString());

        var audit = Assert.Single(Audit(s, holdId));
        Assert.Equal(BsonNull.Value, audit["PreviousAnalysisNote"]);
        Assert.Equal("pH 6,8 — spesifikasyon içinde", audit["AnalysisNote"].AsString);
        Assert.Equal(s.Ledger.User.ToString(), audit["UserId"].AsString);
        Assert.Equal(version - 1, audit["PreviousVersion"].AsInt32);
        Assert.Equal(historyBefore + 1, History(s)); // WP-MVP1-FUB2: the NoteUpdated row (was: no history row)

        // read back through getHold
        Assert.Equal("pH 6,8 — spesifikasyon içinde", Id(await s.GetHoldAsync(holdId), "analysisNote"));
    }

    [Fact]
    public async Task A_stale_version_is_409_and_nothing_changes()
    {
        var (s, holdId, etag) = await OpenHoldAsync();
        var first = QuarantineScenario.Key("upd");
        await s.ExpectAsync(HttpMethod.Patch, $"/api/quarantine/holds/{holdId}", new { analysisNote = "ilk not", idempotencyKey = first }, 200, first, etag);

        var second = QuarantineScenario.Key("upd");
        var refused = await s.ExpectAsync(HttpMethod.Patch, $"/api/quarantine/holds/{holdId}", new { analysisNote = "eski sürümden", idempotencyKey = second }, 409, second, etag);
        Assert.Equal("QUARANTINE_CONFLICT", QuarantineScenario.Code(refused));
        Assert.Equal("ilk not", Id(await s.GetHoldAsync(holdId), "analysisNote"));
        Assert.Single(Audit(s, holdId));
    }

    [Fact]
    public async Task The_same_key_answers_the_hold_again_and_a_different_body_with_it_is_400()
    {
        var (s, holdId, etag) = await OpenHoldAsync();
        var key = QuarantineScenario.Key("upd");
        var body = new { analysisNote = "tekrar", idempotencyKey = key };
        var first = await s.ExpectAsync(HttpMethod.Patch, $"/api/quarantine/holds/{holdId}", body, 200, key, etag);
        var again = await s.ExpectAsync(HttpMethod.Patch, $"/api/quarantine/holds/{holdId}", body, 200, key, etag);
        Assert.Equal(first.GetProperty("version").GetInt32(), again.GetProperty("version").GetInt32());
        Assert.Single(Audit(s, holdId));

        var reused = await s.ExpectAsync(HttpMethod.Patch, $"/api/quarantine/holds/{holdId}", new { analysisNote = "başka", idempotencyKey = key }, 400, key, etag);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", QuarantineScenario.Code(reused));
    }

    [Fact]
    public async Task A_decided_hold_is_closed_for_edits()
    {
        var (s, holdId, _) = await OpenHoldAsync();
        await s.DecideAndApproveAsync(s.DecideAsync(holdId, [QuarantineScenario.Line(1, "Release", 25m)]));
        var (_, _, raw) = await s.SendAsync(HttpMethod.Get, $"/api/quarantine/holds/{holdId}");
        var key = QuarantineScenario.Key("upd");
        var refused = await s.ExpectAsync(HttpMethod.Patch, $"/api/quarantine/holds/{holdId}", new { analysisNote = "geç", idempotencyKey = key }, 409, key, raw.Headers.ETag!.ToString());
        Assert.Equal("QUARANTINE_HOLD_CLOSED", QuarantineScenario.Code(refused));
    }

    [Fact]
    public async Task Shape_rules_If_Match_and_the_review_key_are_enforced()
    {
        var (s, holdId, etag) = await OpenHoldAsync();
        string K() => QuarantineScenario.Key("upd");
        var path = $"/api/quarantine/holds/{holdId}";

        var k1 = K();
        await s.ExpectAsync(HttpMethod.Patch, path, new { analysisNote = "If-Match yok", idempotencyKey = k1 }, 400, k1);
        var k2 = K();
        await s.ExpectAsync(HttpMethod.Patch, path, new { idempotencyKey = k2 }, 400, k2, etag);
        var k3 = K();
        await s.ExpectAsync(HttpMethod.Patch, path, new { analysisNote = new string('x', 2001), idempotencyKey = k3 }, 400, k3, etag);
        var k4 = K();
        await s.ExpectAsync(HttpMethod.Patch, path, new { attachments = Enumerable.Range(0, 21).Select(i => new { name = $"a{i}", dmsRef = $"d{i}" }), idempotencyKey = k4 }, 400, k4, etag);
        var k5 = K();
        await s.ExpectAsync(HttpMethod.Patch, path, new { analysisNote = "x", holdNumber = "QH-1", idempotencyKey = k5 }, 400, k5, etag);

        var noReview = TestTokens.Mint(s.Tenant, s.Ledger.LegalEntity, s.Ledger.User,
            TestTokens.AllPermissions.Concat(QuarantineScenario.QuarantinePermissions.Where(p => p != "quarantine.holds.review")));
        var k6 = K();
        await s.ExpectAsync(HttpMethod.Patch, path, new { analysisNote = "yetkisiz", idempotencyKey = k6 }, 403, k6, etag, noReview);
        Assert.Empty(Audit(s, holdId));
    }

    [Fact]
    public async Task An_empty_note_clears_it()
    {
        var (s, holdId, etag) = await OpenHoldAsync();
        var k1 = QuarantineScenario.Key("upd");
        var (_, _, raw) = await s.SendAsync(HttpMethod.Patch, $"/api/quarantine/holds/{holdId}", new { analysisNote = "silinecek", idempotencyKey = k1 }, k1, etag);
        var k2 = QuarantineScenario.Key("upd");
        var cleared = await s.ExpectAsync(HttpMethod.Patch, $"/api/quarantine/holds/{holdId}", new { analysisNote = "", idempotencyKey = k2 }, 200, k2, raw.Headers.ETag!.ToString());
        Assert.False(cleared.TryGetProperty("analysisNote", out var note) && note.ValueKind == JsonValueKind.String);
        Assert.Equal(2, Audit(s, holdId).Count);
    }
}
