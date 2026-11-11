using System.Text.Json;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Recall.Rules;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-0177-R3 — KR-C, pack §8.5, every row of the movement-type table through the REAL posting endpoint (<c>POST /api/inventory/movements</c>):
/// GOODS_ISSUE_SALES (MVP-6 included), GOODS_ISSUE_PROD and WAREHOUSE_TRANSFER refused 422 <c>LOT_UNDER_RECALL</c>; every other type passes. The
/// exemption is by TYPE only (a <c>sourceModule</c> of MOD-0175 changes nothing — O-F2-2). Typed lot by number + SKU; another company of the
/// tenant refused too (CT-R7, no case id leaks); the pre-check decides normally, the in-transaction re-check when the block lands in between
/// (race); every refusal is an <c>ExitRefused</c> timeline row + audit; the block lifts at closure and holds while another active case keeps it;
/// the FEFO order excludes the recalled lot (<c>RECALLED</c>) and a refused posting writes no FEFO snapshot / exception (recall before FEFO).
/// </summary>
public sealed class RecallExitBlockTests : IClassFixture<RecallApiFactory>
{
    private readonly RecallApiFactory _factory;

    public RecallExitBlockTests(RecallApiFactory factory) => _factory = factory;

    private static string Str(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    [Fact]
    public void The_rule_blocks_exactly_sales_production_issue_and_warehouse_transfer_and_has_no_sourceModule()
    {
        Assert.Equal(new[] { MovementType.GoodsIssueSales, MovementType.GoodsIssueProd, MovementType.WarehouseTransfer }.OrderBy(t => t),
            Enum.GetValues<MovementType>().Where(RecallExitRule.IsBlocked).OrderBy(t => t));
        Assert.All(typeof(RecallExitRule).GetMethods().SelectMany(m => m.GetParameters()),
            p => Assert.DoesNotContain("sourcemodule", p.Name!.ToLowerInvariant(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Every_movement_type_through_the_posting_endpoint_blocked_or_exempt_by_type_only()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        var (caseId, approved) = await RecallR3.ApproveAsync(w.S, w.Fg);
        var lots = w.Lots;
        var number = Str(approved.GetProperty("rootLot"), "lotNumber");
        var covered = new HashSet<MovementType>();

        // exempt receipt first: AVAILABLE stock of the recalled lot for the exempt outbound rows (KR-B quarantined the approved stock)
        Assert.Equal(201, (await RecallR3.PostAsync(lots, lots.Receipt("20", w.Fg, movementType: "GOODS_RECEIPT_PO"))).Status);
        covered.Add(MovementType.GoodsReceiptPo);
        var before = w.S.Persisted("inventory_transactions").Count;

        // ── blocked: 422 LOT_UNDER_RECALL, nothing written ──
        var refusals = new List<(string Type, Dictionary<string, object?> Body)>
        {
            ("GOODS_ISSUE_SALES", RecallR3.WithLot(lots.Ledger.Issue("1"), w.Fg, "MOD-0183")),          // the MVP-6 shipment issue
            ("GOODS_ISSUE_SALES", RecallR3.WithLot(lots.Ledger.Issue("1"), w.Fg, "MOD-0175")),          // O-F2-2: caller data never exempts
            ("GOODS_ISSUE_PROD", RecallR3.WithLot(lots.Ledger.Issue("1", "GOODS_ISSUE_PROD"), w.Fg)),
            ("WAREHOUSE_TRANSFER", WarehouseTransfer(lots, w.Fg))
        };
        foreach (var (type, body) in refusals)
        {
            var (status, error) = await RecallR3.PostAsync(lots, body);
            Assert.True(status == 422, $"{type}: {status} {error}");
            Assert.Equal("LOT_UNDER_RECALL", RecallR3.Code(error));
            var d = error.GetProperty("error").GetProperty("details");
            Assert.Equal((w.Fg, number, caseId, Str(approved, "number"), type),
                (Str(d, "lotId"), Str(d, "lotNumber"), Str(d, "recallCaseId"), Str(d, "recallCaseNumber"), Str(d, "movementType")));
            covered.Add(WireNames.TryParseMovementType(type, out var t) ? t : throw new InvalidOperationException(type));
        }

        Assert.Equal(before, w.S.Persisted("inventory_transactions").Count);
        var exits = RecallR3.Events(w.S, caseId, "ExitRefused");
        Assert.Equal(refusals.Count, exits.Count);
        Assert.All(exits, e => Assert.Equal("pre-check", e["Refs"]["stage"].AsString)); // the pre-check decides; the re-check is the race guard
        Assert.Equal(refusals.Count, w.S.Audit("recall.exit.refused").Count);

        // ── exempt: every other type posts ──
        async Task Ok(MovementType type, Dictionary<string, object?> body)
        {
            var (status, answer) = await RecallR3.PostAsync(lots, body);
            Assert.True(status == 201, $"{type} must pass the exit block: {status} {answer}");
            covered.Add(type);
        }

        var scrap = lots.Issue("1", w.Fg, movementType: "SCRAP");
        var (scrapStatus, scrapBody) = await RecallR3.PostAsync(lots, scrap);
        Assert.Equal(201, scrapStatus);
        covered.Add(MovementType.Scrap);
        await Ok(MovementType.Reversal, lots.Reversal(scrap, Str(scrapBody, "transactionId")));
        await Ok(MovementType.WriteOff, lots.Issue("1", w.Fg, movementType: "WRITE_OFF"));
        await Ok(MovementType.SupplierReturn, lots.Issue("1", w.Fg, movementType: "SUPPLIER_RETURN"));
        await Ok(MovementType.CountLoss, lots.Issue("1", w.Fg, movementType: "COUNT_LOSS"));
        await Ok(MovementType.CountGain, lots.Receipt("1", w.Fg, movementType: "COUNT_GAIN"));
        await Ok(MovementType.GoodsReceiptProd, lots.Receipt("1", w.Fg, movementType: "GOODS_RECEIPT_PROD"));
        await Ok(MovementType.OpeningBalance, lots.Receipt("1", w.Fg, movementType: "OPENING_BALANCE"));
        await Ok(MovementType.CustomerReturn, lots.Receipt("1", w.Fg, movementType: "CUSTOMER_RETURN", toStatus: "QUARANTINE"));
        await Ok(MovementType.StatusTransfer, lots.StatusTransfer("1", w.Fg));
        await Ok(MovementType.LocationTransfer, lots.Transfer("1", w.Fg));

        Assert.Equal(Enum.GetValues<MovementType>().OrderBy(t => t), covered.OrderBy(t => t)); // the table is complete
        Assert.Equal(refusals.Count, RecallR3.Events(w.S, caseId, "ExitRefused").Count);   // no exempt posting was recorded as a refusal
    }

    private static Dictionary<string, object?> WarehouseTransfer(LotLedgerScenario lots, string lotId)
    {
        var body = lots.Ledger.Movement("WAREHOUSE_TRANSFER", "1");
        body["fromWarehouseId"] = lots.Ledger.Warehouse;
        body["fromLocationId"] = lots.Ledger.LocationA;
        body["toWarehouseId"] = lots.Ledger.Warehouse2;
        body["toLocationId"] = lots.Ledger.LocationC;
        body["lotId"] = lotId;
        return body;
    }

    [Fact]
    public async Task A_typed_lot_text_equal_to_the_recalled_lot_number_of_the_same_sku_is_refused_the_same_way()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        var (_, approved) = await RecallR3.ApproveAsync(w.S, w.Fg);
        var number = Str(approved.GetProperty("rootLot"), "lotNumber");

        foreach (var typed in new[] { number, number.ToLowerInvariant(), " " + number + " " })
        {
            var (status, error) = await RecallR3.PostAsync(w.Lots, RecallR3.WithLot(w.Lots.Ledger.Issue("1"), typed));
            Assert.True(status == 422 && RecallR3.Code(error) == "LOT_UNDER_RECALL", $"'{typed}': {status} {error}");
        }

        // the same text on another SKU is not this recall's lot
        var otherSku = w.Lots.AddSecondSku();
        var body = RecallR3.WithLot(w.Lots.Ledger.Issue("1"), number);
        body["skuId"] = otherSku.ToString("D");
        body["skuLevel"] = "Gsku";
        var (otherStatus, otherAnswer) = await RecallR3.PostAsync(w.Lots, body);
        Assert.False(otherStatus == 422 && RecallR3.Code(otherAnswer) == "LOT_UNDER_RECALL", otherAnswer.ToString());
    }

    [Fact]
    public async Task The_block_holds_in_every_company_of_the_tenant_without_leaking_the_case_id_CT_R7()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        var (caseId, approved) = await RecallR3.ApproveAsync(w.S, w.Fg);
        var otherCompany = Guid.NewGuid();
        var token = TestTokens.Mint(w.S.Tenant, otherCompany, w.S.Lots.Ledger.User, TestTokens.AllPermissions.Concat(TestTokens.TracePermissions));

        var (status, error) = await RecallR3.PostAsync(w.Lots, RecallR3.WithLot(w.Lots.Ledger.Issue("1"), w.Fg, "MOD-0183"), token);

        Assert.True(status == 422 && RecallR3.Code(error) == "LOT_UNDER_RECALL", $"{status} {error}");
        var d = error.GetProperty("error").GetProperty("details");
        Assert.Equal(Str(approved, "number"), Str(d, "recallCaseNumber"));
        Assert.Equal(JsonValueKind.Null, d.GetProperty("recallCaseId").ValueKind);
        var exit = Assert.Single(RecallR3.Events(w.S, caseId, "ExitRefused"));
        Assert.Equal("other", exit["Refs"]["postingCompany"].AsString); // the case's timeline (its company) records it
    }

    [Fact]
    public async Task A_block_that_lands_between_the_pre_check_and_the_transaction_is_refused_by_the_re_check()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        var lot = await w.S.LotAsync();
        await w.PostAsync(w.Lots.Receipt("5", lot, movementType: "GOODS_RECEIPT_PO"), "MOD-0172", "PO-RACE");
        _factory.TrackRecallTenant(w.S.Tenant);
        _factory.AfterPassedPreCheck[lot] = () => { RecallR3.InsertBlock(w.S, lot, "RC-RACE-0001"); return Task.CompletedTask; };
        var before = w.S.Persisted("inventory_transactions").Count;

        var (status, error) = await RecallR3.PostAsync(w.Lots, RecallR3.WithLot(w.Lots.Ledger.Issue("1"), lot, "MOD-0183"));

        Assert.True(status == 422 && RecallR3.Code(error) == "LOT_UNDER_RECALL", $"the re-check must refuse: {status} {error}");
        Assert.Equal("RC-RACE-0001", Str(error.GetProperty("error").GetProperty("details"), "recallCaseNumber"));
        Assert.Equal(before, w.S.Persisted("inventory_transactions").Count);
        Assert.Contains(w.S.Audit("recall.exit.refused"), a => a["Detail"]["stage"] == "re-check");
    }

    [Fact]
    public async Task The_block_lifts_at_closure_and_a_lot_in_two_active_cases_stays_blocked_until_both_release()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        var (caseA, _) = await RecallR3.ApproveAsync(w.S, w.Fg);
        var companyB = Guid.NewGuid();
        var (caseB, approvedB) = await RecallR3.ApproveAsync(w.S, w.Fg, companyB); // the same (tenant-wide) lot recalled in another company
        await RecallR3.PostAsync(w.Lots, w.Lots.Receipt("5", w.Fg, movementType: "GOODS_RECEIPT_PO"));
        Assert.Equal(2, RecallR3.Blocks(w.S, w.Fg).Count(b => b["Active"].AsBoolean));

        await RecallR3.CloseAsync(w.S, caseA);
        var (stillStatus, still) = await RecallR3.PostAsync(w.Lots, RecallR3.WithLot(w.Lots.Ledger.Issue("1"), w.Fg));
        Assert.True(stillStatus == 422 && RecallR3.Code(still) == "LOT_UNDER_RECALL", $"case B still holds the lot: {stillStatus} {still}");
        Assert.Equal(Str(approvedB, "number"), Str(still.GetProperty("error").GetProperty("details"), "recallCaseNumber"));

        await RecallR3.CloseAsync(w.S, caseB, companyB);
        Assert.DoesNotContain(RecallR3.Blocks(w.S, w.Fg), b => b["Active"].AsBoolean);
        var (freeStatus, free) = await RecallR3.PostAsync(w.Lots, RecallR3.WithLot(w.Lots.Ledger.Issue("1"), w.Fg));
        Assert.True(freeStatus == 201, $"released at closure: {freeStatus} {free}");
    }

    [Fact]
    public async Task The_FEFO_order_excludes_the_recalled_lot_and_a_refused_issue_writes_no_FEFO_snapshot_or_exception()
    {
        var s = new RecallScenario(_factory);
        var recalled = await s.Lots.CreateLotAsync(expiryDate: "2028-06-30");
        await s.Lots.ReleaseAsync(recalled);
        var first = await s.Lots.CreateLotAsync(expiryDate: "2027-01-31"); // earlier: the FEFO-first lot
        await s.Lots.ReleaseAsync(first);
        await s.Lots.PostOkAsync(s.Lots.Receipt("10", recalled, movementType: "GOODS_RECEIPT_PO"));
        await s.Lots.PostOkAsync(s.Lots.Receipt("10", first, movementType: "GOODS_RECEIPT_PO"));
        await RecallR3.ApproveAsync(s, recalled);
        await s.Lots.PostOkAsync(s.Lots.Receipt("3", recalled, movementType: "GOODS_RECEIPT_PO")); // AVAILABLE again (exempt receipt)

        // CT-R12: getFefoOrder excludes it with RECALLED; the first lot leads
        var reader = s.Mint(s.Qa1, ["expiry.fefo-order.read"]);
        var order = await s.ExpectAsync(HttpMethod.Get,
            $"/api/expiry/fefo-order?warehouseId={s.Ledger.Warehouse}&itemId={s.Ledger.Item}&skuId={s.Ledger.Sku}&skuLevel=Lsku", reader, null, 200);
        Assert.Equal(first, Str(order.GetProperty("eligible")[0], "lotId"));
        var excluded = Assert.Single(order.GetProperty("excluded").EnumerateArray(), e => Str(e, "lotId") == recalled);
        Assert.Equal("RECALLED", Str(excluded, "reason"));
        Assert.DoesNotContain(order.GetProperty("eligible").EnumerateArray(), e => Str(e, "lotId") == recalled);

        // recall before FEFO: a non-FEFO-first issue of the recalled lot answers LOT_UNDER_RECALL (not a FEFO code) and writes nothing of 0176
        var snapshots = s.Persisted("expiry_fefo_issue_snapshots").Count;
        var exceptions = s.Persisted("expiry_exceptions").Count;
        var (status, error) = await RecallR3.PostAsync(s.Lots, RecallR3.WithLot(s.Ledger.Issue("1"), recalled, "MOD-0183"));
        Assert.True(status == 422 && RecallR3.Code(error) == "LOT_UNDER_RECALL", $"{status} {error}");
        Assert.Equal(snapshots, s.Persisted("expiry_fefo_issue_snapshots").Count);
        Assert.Equal(exceptions, s.Persisted("expiry_exceptions").Count);
        Assert.DoesNotContain(s.Persisted("inventory_audit"), a => a["Action"].AsString.StartsWith("expiry.fefo", StringComparison.Ordinal));

        // the FEFO-first lot still issues without a reason (the recalled lot is not a candidate)
        await s.Lots.PostOkAsync(s.Lots.Issue("1", first));
    }
}
