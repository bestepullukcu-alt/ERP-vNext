using System.Text.Json;
using Diten.InventoryService.Application.Features.Quarantine.Ports;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.InventoryService.Tests.Quarantine;

/// <summary>
/// MOD-0175 Q4 — KQ-H retest hard block, CT-Q3 / CT-Q16, on the REAL service and Mongo (every response contract-checked):
/// <list type="bullet">
/// <item>posting: a retest-due lot refuses sales / production issue, transfer and supplier return with 422 <c>LOT_RETEST_DUE</c>;
/// STATUS_TRANSFER AVAILABLE → QUARANTINE / QUALITY_INSPECTION / BLOCKED, SCRAP, WRITE_OFF and REVERSAL still post;</item>
/// <item>the stored hold (CT-Q16): the sweeper sets <c>RetestHeldAtUtc</c> and takes the lot's AVAILABLE stock out of the SKU position in
/// one transaction — availability drops, a NEW reservation is refused, an EXISTING one stays (CT-Q6); a second sweep changes nothing;
/// an extended retest date clears it and the stock comes back;</item>
/// <item>consistency (CT-Q16 (4)): after every scenario the live SKU position equals what the rebuilder computes; under a race between
/// the sweeper and an exception posting (SCRAP) there is no double subtraction (CT-Q16 (3));</item>
/// <item>the retest queue (listRetest), "Karantinaya Al" mode Retest (one hold per AVAILABLE bucket, Source Retest), and the retest
/// release (the approved new retest date on the lot, the hold cleared, the stock available at once).</item>
/// </list>
/// The scenario posting date is fixed (2026-09-30); a "due" lot carries retest date 2026-09-15 (before it and before today).
/// </summary>
public sealed class QuarantineRetestTests : IClassFixture<QuarantineApiFactory>
{
    private static readonly DateOnly Due = new(2026, 9, 15);
    private static readonly DateOnly Future = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60);
    private static readonly string FutureWire = LotLedgerScenario.Midnight(Future);

    private readonly QuarantineApiFactory _factory;

    public QuarantineRetestTests(QuarantineApiFactory factory) => _factory = factory;

    private static string Code(JsonElement e) => e.GetProperty("error").GetProperty("code").GetString()!;

    /// <summary>A Released lot with <paramref name="available"/> AVAILABLE at bin A (received while not due), then made retest-due.</summary>
    private static async Task<string> DueLotAsync(LotLedgerScenario s, string available = "30", string? atB = null)
    {
        var lot = await s.CreateLotAsync(expiryDate: "2028-01-31T00:00:00Z", retestDate: FutureWire);
        await s.ReleaseAsync(lot);
        await s.PostOkAsync(s.Receipt(available, lot));
        if (atB is not null) await s.PostOkAsync(s.Receipt(atB, lot, location: s.Ledger.LocationB));
        await s.SetRetestDateAsync(lot, Due);
        return lot;
    }

    // ── posting rule (KQ-H, V24) ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("GOODS_ISSUE_SALES")]
    [InlineData("GOODS_ISSUE_PROD")]
    [InlineData("SUPPLIER_RETURN")]
    public async Task A_retest_due_lot_refuses_issues_and_supplier_return_with_LOT_RETEST_DUE(string movementType)
    {
        var s = new LotLedgerScenario(_factory);
        var lot = await DueLotAsync(s);

        var message = await s.RefusedAsync(s.Issue("1", lot, movementType: movementType), "LOT_RETEST_DUE");
        Assert.Contains("retest", message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(30m, s.BalanceOnHandOfLot(lot)); // nothing moved
    }

    [Fact]
    public async Task A_retest_due_lot_refuses_a_transfer_with_LOT_RETEST_DUE_and_names_the_lot()
    {
        var s = new LotLedgerScenario(_factory);
        var lot = await DueLotAsync(s);

        var refused = await s.PostAsync(s.Transfer("2", lot), 422);
        Assert.Equal("LOT_RETEST_DUE", Code(refused));
        var details = refused.GetProperty("error").GetProperty("details");
        Assert.Equal(lot, details.GetProperty("lotId").GetString());
        Assert.Equal("2026-09-15", details.GetProperty("retestDate").GetString());
    }

    [Fact]
    public async Task The_exceptions_still_post_status_transfer_out_of_AVAILABLE_scrap_write_off_and_reversal()
    {
        var s = new LotLedgerScenario(_factory);
        var lot = await s.CreateLotAsync(expiryDate: "2028-01-31T00:00:00Z", retestDate: FutureWire);
        await s.ReleaseAsync(lot);
        var receipt = s.Receipt("30", lot);
        var receiptId = await s.PostOkAsync(receipt);
        var extra = s.Receipt("4", lot);
        var extraId = await s.PostOkAsync(extra);
        await s.SetRetestDateAsync(lot, Due);

        await s.PostOkAsync(s.StatusTransfer("3", lot));                         // AVAILABLE → QUALITY_INSPECTION
        var toQuarantine = s.StatusTransfer("2", lot);
        toQuarantine["toStockStatus"] = "QUARANTINE";
        await s.PostOkAsync(toQuarantine);                                        // AVAILABLE → QUARANTINE
        var toBlocked = s.StatusTransfer("1", lot);
        toBlocked["toStockStatus"] = "BLOCKED";
        await s.PostOkAsync(toBlocked);                                           // AVAILABLE → BLOCKED
        await s.PostOkAsync(s.Issue("2", lot, movementType: "SCRAP"));
        await s.PostOkAsync(s.Issue("1", lot, movementType: "WRITE_OFF"));
        await s.PostOkAsync(s.Reversal(extra, extraId));                          // REVERSAL of the 4

        // the way back into use is not an exception: QUALITY_INSPECTION → AVAILABLE of a due lot is refused
        var back = s.StatusTransfer("1", lot);
        back["fromStockStatus"] = "QUALITY_INSPECTION";
        back["toStockStatus"] = "AVAILABLE";
        await s.RefusedAsync(back, "LOT_RETEST_DUE");
        Assert.NotNull(receiptId);
        Assert.Equal(30m - 2m - 1m, s.BalanceOnHandOfLot(lot));
        await s.AssertConsistentAsync("exceptions on a due lot");
    }

    // ── the stored hold: availability, reservations, sweeper, consistency (CT-Q16) ──────────────────────

    [Fact]
    public async Task The_sweeper_holds_the_lot_availability_drops_a_new_reservation_is_refused_the_existing_one_stays()
    {
        var s = new LotLedgerScenario(_factory);
        var free = await s.CreateLotAsync();
        await s.ReleaseAsync(free);
        await s.PostOkAsync(s.Receipt("10", free, location: s.Ledger.LocationB));
        var lot = await s.CreateLotAsync(expiryDate: "2028-01-31T00:00:00Z", retestDate: FutureWire);
        await s.ReleaseAsync(lot);
        await s.PostOkAsync(s.Receipt("30", lot));
        var existing = await s.ReserveAsync("8", 201);
        Assert.Equal(40m, await s.AvailableAsync() + 8m);

        await s.SetRetestDateAsync(lot, Due);
        Assert.False(LotLedgerScenario.RetestHeld(s.LotDocument(lot))); // the date alone holds nothing — the stored mark does (CT-Q16)
        await s.AssertConsistentAsync("date passed, not swept");

        var swept = await s.SweepRetestAsync();
        Assert.Equal(1, swept.Held);
        Assert.True(LotLedgerScenario.RetestHeld(s.LotDocument(lot)));
        Assert.Equal(10m, s.SkuPositionAvailableOnHand());
        var availability = await s.Ledger.ReadAsync(await s.Ledger.GetAsync($"/api/inventory/availability?skuId={s.Ledger.Sku}"));
        Assert.Equal("2.000", availability.GetProperty("available").GetString()); // 10 free − 8 reserved
        Assert.Equal("40.000", availability.GetProperty("byStatus").GetProperty("AVAILABLE").GetString()); // the rows keep their status
        Assert.Equal("INSUFFICIENT_AVAILABLE", Code(await s.ReserveAsync("3", 422)));
        await s.AssertConsistentAsync("held");

        // CT-Q6: the existing reservation is neither released nor cancelled
        var reservation = Assert.Single(s.Persisted("inventory_reservations"), r => r["_id"].AsString == existing.GetProperty("reservationId").GetString());
        Assert.Equal("Active", reservation["Status"].AsString);

        var again = await s.SweepRetestAsync();
        Assert.Equal((0, 0), (again.Held, again.Cleared)); // two sweeps, one hold
        Assert.Equal(10m, s.SkuPositionAvailableOnHand());
        await s.AssertConsistentAsync("swept twice");
    }

    [Fact]
    public async Task Extending_the_retest_date_clears_the_hold_and_brings_the_stock_back()
    {
        var s = new LotLedgerScenario(_factory);
        var lot = await DueLotAsync(s, "30", atB: "5");
        await s.SweepRetestAsync();
        Assert.Equal(0m, s.SkuPositionAvailableOnHand());
        await s.RefusedAsync(s.Issue("1", lot), "LOT_RETEST_DUE");

        await s.SetRetestDateAsync(lot, Future);
        await s.PostOkAsync(s.Issue("1", lot)); // the posting rule reads the date: no longer due
        Assert.True(LotLedgerScenario.RetestHeld(s.LotDocument(lot))); // still held until the sweep clears it …
        await s.AssertConsistentAsync("extended, not swept"); // … and the held posting left the SKU position alone

        var cleared = await s.SweepRetestAsync();
        Assert.Equal(1, cleared.Cleared);
        Assert.False(LotLedgerScenario.RetestHeld(s.LotDocument(lot)));
        Assert.Equal(34m, s.SkuPositionAvailableOnHand());
        Assert.Equal(34m, await s.AvailableAsync());
        await s.ReserveAsync("34", 201);
        await s.AssertConsistentAsync("extended and swept");
        Assert.Equal((0, 0), ((await s.SweepRetestAsync()).Held, (await s.SweepRetestAsync()).Cleared));
    }

    [Fact]
    public async Task Exception_postings_on_a_held_lot_leave_the_position_alone()
    {
        var s = new LotLedgerScenario(_factory);
        var lot = await DueLotAsync(s, "30");
        await s.SweepRetestAsync();
        await s.PostOkAsync(s.Issue("4", lot, movementType: "SCRAP"));
        await s.PostOkAsync(s.StatusTransfer("3", lot));
        Assert.Equal(0m, s.SkuPositionAvailableOnHand()); // no double subtraction (CT-Q16 sabotage (g))
        await s.AssertConsistentAsync("SCRAP + status transfer on a held lot");
    }

    /// <summary>
    /// CT-Q16 (3): the sweeper setting the hold and a parallel SCRAP of the same lot. Both write the lot document in their own
    /// transaction (the sweeper the mark, the posting <c>StockTouchSeq</c>), so one re-runs on the other's commit: the result equals the
    /// rebuilder in every round, whichever commits first.
    /// </summary>
    [Fact]
    public async Task Sweeper_and_a_parallel_scrap_never_subtract_twice()
    {
        var s = new LotLedgerScenario(_factory);
        for (var round = 0; round < 6; round++)
        {
            var lot = await DueLotAsync(s, "20");
            var lotId = Guid.Parse(lot);
            using var scope = _factory.Services.CreateScope();
            var hold = scope.ServiceProvider.GetRequiredService<ILotRetestHold>();
            var sweep = Task.Run(() => hold.RefreshAsync(lotId, DateTimeOffset.UtcNow));
            var scrap = Task.Run(() => s.PostOkAsync(s.Issue("5", lot, movementType: "SCRAP")));
            await Task.WhenAll(sweep, scrap);

            Assert.True(await sweep);
            Assert.Equal(15m, s.BalanceOnHandOfLot(lot));
            await s.AssertConsistentAsync($"round {round}");
        }

        Assert.Equal(0m, s.SkuPositionAvailableOnHand()); // every lot is held, nothing of theirs is available
    }

    // ── retest queue + Karantinaya Al mode Retest ────────────────────────────────────────────────────────

    [Fact]
    public async Task The_retest_queue_lists_due_upcoming_and_quarantined_lots()
    {
        var q = new QuarantineScenario(_factory);
        var due = await DueLotAsync(q.Lots, "12");
        var soon = await q.Lots.CreateLotAsync(expiryDate: "2028-01-31T00:00:00Z", retestDate: LotLedgerScenario.Midnight(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10)));
        await q.Lots.ReleaseAsync(soon);
        await q.Lots.PostOkAsync(q.Lots.Receipt("7", soon, location: q.Ledger.LocationB));
        var far = await q.Lots.CreateLotAsync(expiryDate: "2028-01-31T00:00:00Z", retestDate: FutureWire);
        await q.Lots.ReleaseAsync(far);
        await q.Lots.PostOkAsync(q.Lots.Receipt("3", far));

        var list = (await q.ExpectAsync(HttpMethod.Get, "/api/quarantine/retest", null, 200)).EnumerateArray().ToList();
        Assert.Equal([due, soon], list.Select(i => i.GetProperty("lotId").GetString()).ToList()); // ordered by retest date; far is beyond 30 days
        var dueItem = list[0];
        Assert.Equal("DueClosed", dueItem.GetProperty("state").GetString());
        Assert.True(dueItem.GetProperty("daysRelative").GetInt32() < 0);
        Assert.Equal(12m, QuarantineScenario.D(dueItem.GetProperty("availableQuantity")));
        Assert.Single(dueItem.GetProperty("buckets").EnumerateArray());
        Assert.Equal("Upcoming", list[1].GetProperty("state").GetString());
        Assert.Equal(10, list[1].GetProperty("daysRelative").GetInt32());

        var wide = (await q.ExpectAsync(HttpMethod.Get, "/api/quarantine/retest?horizonDays=90", null, 200)).EnumerateArray().Select(i => i.GetProperty("lotId").GetString()).ToList();
        Assert.Contains(far, wide);
        var onlyDue = (await q.ExpectAsync(HttpMethod.Get, "/api/quarantine/retest?state=DueClosed", null, 200)).EnumerateArray().Select(i => i.GetProperty("lotId").GetString()).ToList();
        Assert.Equal([due], onlyDue);
        Assert.Equal("INVALID_REQUEST", Code(await q.ExpectAsync(HttpMethod.Get, "/api/quarantine/retest?horizonDays=91", null, 400)));

        var key = QuarantineScenario.Key("rt");
        await q.ExpectAsync(HttpMethod.Post, "/api/quarantine/holds", new { mode = "Retest", lotId = due, reason = QuarantineScenario.Reason, idempotencyKey = key }, 201, key);
        var quarantined = (await q.ExpectAsync(HttpMethod.Get, "/api/quarantine/retest?state=Quarantined", null, 200)).EnumerateArray().Single();
        Assert.Equal(due, quarantined.GetProperty("lotId").GetString());
        Assert.Equal(0m, QuarantineScenario.D(quarantined.GetProperty("availableQuantity")));
        Assert.Single(quarantined.GetProperty("holdIds").EnumerateArray());
    }

    [Fact]
    public async Task Karantinaya_Al_mode_Retest_quarantines_every_AVAILABLE_bucket_one_hold_each_and_replays()
    {
        var q = new QuarantineScenario(_factory);
        var lot = await DueLotAsync(q.Lots, "30", atB: "6");
        await q.Lots.ReserveAsync("1", 201); // a reservation of the SKU, made before the hold (CT-Q6: it stays)
        await q.Lots.SweepRetestAsync();     // the lot's 36 leave the position: the SKU is over-reserved, the held lot uncovers nothing more

        var key = QuarantineScenario.Key("rt");
        var body = new { mode = "Retest", lotId = lot, reason = QuarantineScenario.Reason, idempotencyKey = key };
        var holds = (await q.ExpectAsync(HttpMethod.Post, "/api/quarantine/holds", body, 201, key)).EnumerateArray().ToList();
        Assert.Equal(2, holds.Count);
        Assert.All(holds, h => Assert.Equal("Retest", h.GetProperty("source").GetString()));
        Assert.Equal([30m, 6m], holds.Select(h => QuarantineScenario.D(h.GetProperty("quantity"))).OrderByDescending(x => x).ToList());
        Assert.All(q.Holds(), h => Assert.Equal("2026-09-15", h["RetestDateAtIntake"].ToUniversalTime().ToString("yyyy-MM-dd")));
        Assert.Equal(36m, await q.OnHandAsync("QUARANTINE") + await q.OnHandAsync("QUARANTINE", q.Ledger.LocationB));
        Assert.Equal(0m, await q.OnHandAsync("AVAILABLE"));
        await q.Lots.AssertConsistentAsync("retest quarantine of a held lot");

        var replay = (await q.ExpectAsync(HttpMethod.Post, "/api/quarantine/holds", body, 200, key)).EnumerateArray().Select(h => h.GetProperty("holdId").GetString()).ToList();
        Assert.Equal(holds.Select(h => h.GetProperty("holdId").GetString()).ToList(), replay);
        var other = new { mode = "Retest", lotId = lot, reason = QuarantineScenario.Reason + " (başka)", idempotencyKey = key };
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", Code(await q.ExpectAsync(HttpMethod.Post, "/api/quarantine/holds", other, 400, key)));
        Assert.Equal("INVALID_REQUEST", Code(await q.ExpectAsync(HttpMethod.Post, "/api/quarantine/holds",
            new { mode = "Retest", reason = QuarantineScenario.Reason, idempotencyKey = QuarantineScenario.Key("rt") }, 400, QuarantineScenario.Key("rt"))));
    }

    [Fact]
    public async Task Karantinaya_Al_mode_Retest_on_a_due_lot_not_yet_swept_is_refused_when_reservations_would_be_uncovered()
    {
        var q = new QuarantineScenario(_factory);
        var lot = await DueLotAsync(q.Lots, "10");
        await q.Lots.ReserveAsync("8", 201); // not swept: the lot's 10 still count — taking them out would uncover 8

        var key = QuarantineScenario.Key("rt");
        var refused = await q.ExpectAsync(HttpMethod.Post, "/api/quarantine/holds", new { mode = "Retest", lotId = lot, reason = QuarantineScenario.Reason, idempotencyKey = key }, 409, key);
        Assert.Equal("QUARANTINE_STOCK_RESERVED", Code(refused));
        Assert.Empty(q.Holds());
        Assert.Equal(10m, await q.OnHandAsync("AVAILABLE"));
    }

    // ── retest release (KQ-H: the approved new retest date) ──────────────────────────────────────────────

    [Fact]
    public async Task Releasing_a_Retest_hold_needs_a_new_retest_date_then_sets_it_clears_the_hold_and_the_stock_is_available()
    {
        var q = new QuarantineScenario(_factory);
        var lot = await DueLotAsync(q.Lots, "20");
        await q.Lots.SweepRetestAsync();
        var key = QuarantineScenario.Key("rt");
        var hold = (await q.ExpectAsync(HttpMethod.Post, "/api/quarantine/holds", new { mode = "Retest", lotId = lot, reason = QuarantineScenario.Reason, idempotencyKey = key }, 201, key))
            .EnumerateArray().Single().GetProperty("holdId").GetString()!;

        var noDate = await q.DecideAsync(hold, [QuarantineScenario.Line(1, "Release", 20m)], expected: 400);
        Assert.Equal("QUARANTINE_RETEST_DATE_REQUIRED", Code(noDate));

        var newDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(120);
        var draftKey = QuarantineScenario.Key("dec");
        var draft = await q.ExpectAsync(HttpMethod.Post, "/api/quarantine/decisions", new Dictionary<string, object?>
        {
            ["kind"] = "HoldDecision", ["targetId"] = hold, ["lines"] = new[] { QuarantineScenario.Line(1, "Release", 20m) }, ["reason"] = QuarantineScenario.Reason,
            ["coaChecked"] = true, ["newRetestDate"] = newDate.ToString("yyyy-MM-dd"), ["idempotencyKey"] = draftKey
        }, 201, draftKey);
        var decisionId = draft.GetProperty("decisionId").GetString()!;
        await q.SubmitAsync(decisionId);
        var applied = await q.OutcomeAsync(decisionId);
        Assert.Equal("Applied", applied.GetProperty("state").GetString());

        var lotDoc = q.Lots.LotDocument(lot);
        Assert.Equal(newDate.ToString("yyyy-MM-dd"), lotDoc["RetestDate"].ToUniversalTime().ToString("yyyy-MM-dd"));
        Assert.False(LotLedgerScenario.RetestHeld(lotDoc));
        Assert.Equal(20m, await q.OnHandAsync("AVAILABLE"));
        Assert.Equal(20m, q.Lots.SkuPositionAvailableOnHand()); // available at once, no sweep needed
        await q.Lots.AssertConsistentAsync("retest release");

        var history = await q.Lots.GetTraceAsync($"/api/trace/lots/{lot}/history?pageSize=50");
        var row = Assert.Single(history.GetProperty("items").EnumerateArray(),
            e => e.TryGetProperty("field", out var f) && f.GetString() == "retestDate" && e.GetProperty("reason").GetString()!.StartsWith("MOD-0175 approval QKR-", StringComparison.Ordinal));
        Assert.Equal(q.Approver.ToString("D"), row.GetProperty("actorUserId").GetString());
        await q.Lots.PostOkAsync(q.Lots.Issue("1", lot)); // usable again
    }
}
