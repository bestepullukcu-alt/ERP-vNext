using System.Text.Json;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Quarantine;

/// <summary>
/// MOD-0175 pack §16 Q1 #3 — the apply engine, driven by the Q1 test approval port: EVERY row of the pack §8 stock-effect
/// table posts exactly the listed INVENTORY movement with the listed from / to status and location, measured through the
/// ledger READ API (transactions + balance); a replay (the same outcome again, a re-apply) posts nothing. Plus §13 #8 (no
/// movement before approval), #9 (rejected → nothing, reopened), #11 (four eyes), #12 (ApplyFailed → re-apply posts once,
/// CT-Q9), #14 (sample stays BLOCKED), #17 (reserved stock refused).
/// </summary>
public sealed class QuarantineApplyEngineTests : IClassFixture<QuarantineApiFactory>
{
    private readonly QuarantineApiFactory _factory;

    public QuarantineApplyEngineTests(QuarantineApiFactory factory) => _factory = factory;

    private static string Id(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    private static void AssertMovement(JsonElement t, string type, string? from, string? to, decimal quantity, string? toLocation = null)
    {
        Assert.Equal(type, t.GetProperty("movementType").GetString());
        Assert.Equal(from, t.TryGetProperty("fromStockStatus", out var f) ? f.GetString() : null);
        Assert.Equal(to, t.TryGetProperty("toStockStatus", out var s) ? s.GetString() : null);
        Assert.Equal(quantity, QuarantineScenario.D(t.GetProperty("baseQuantity")));
        if (toLocation is not null) Assert.Equal(toLocation, t.GetProperty("toLocationId").GetString());
    }

    private async Task<(QuarantineScenario S, string Lot, JsonElement Hold)> HoldOf(string quantity, string status = "QUARANTINE")
    {
        var s = new QuarantineScenario(_factory);
        var lot = await s.ReleasedLotAsync();
        var (_, hold) = await s.HoldAsync(quantity, lot, status);
        return (s, lot, hold);
    }

    [Theory]
    [InlineData("QUARANTINE")]
    [InlineData("QUALITY_INSPECTION")]
    public async Task Release_posts_one_status_transfer_to_available_and_a_replay_posts_nothing(string status)
    {
        var (s, _, hold) = await HoldOf("1200", status);
        var draft = await s.DecideAsync(Id(hold, "holdId"), [QuarantineScenario.Line(1, "Release", 1200m)]);
        var number = Id(draft, "decisionNumber");
        Assert.Equal("Draft", Id(draft, "state"));
        Assert.Equal("STATUS_TRANSFER", Id(draft.GetProperty("plannedMovements")[0], "movementType"));

        // §13 #8: no stock moves before approval
        var submitted = await s.SubmitAsync(Id(draft, "decisionId"));
        Assert.Equal("PendingApproval", Id(submitted, "state"));
        Assert.Empty(await s.LedgerMovementsAsync(number));
        Assert.Equal(1200m, await s.OnHandAsync(status));
        Assert.Equal("DecisionPending", Id(await s.GetHoldAsync(Id(hold, "holdId")), "status"));

        var applied = await s.OutcomeAsync(Id(draft, "decisionId"));
        Assert.Equal("Applied", Id(applied, "state"));
        var moves = await s.LedgerMovementsAsync(number);
        AssertMovement(Assert.Single(moves), "STATUS_TRANSFER", status, "AVAILABLE", 1200m, s.Ledger.LocationA);
        Assert.Equal(0m, await s.OnHandAsync(status));
        Assert.Equal(1200m, await s.OnHandAsync("AVAILABLE"));
        var after = await s.GetHoldAsync(Id(hold, "holdId"));
        Assert.Equal("Decided", Id(after, "status"));

        // replay: the same outcome again changes nothing
        var count = await s.AllMovementCountAsync();
        Assert.Equal("Applied", Id(await s.OutcomeAsync(Id(draft, "decisionId")), "state"));
        Assert.Equal(count, await s.AllMovementCountAsync());
        Assert.Single(await s.LedgerMovementsAsync(number));
    }

    [Theory]
    [InlineData("Reject", true, true)]
    [InlineData("Block", false, false)]
    public async Task Reject_and_block_post_one_status_transfer_to_blocked_and_create_the_blocked_record(string decision, bool pending, bool reviewed)
    {
        var (s, _, hold) = await HoldOf("500");
        var applied = await s.DecideAndApproveAsync(s.DecideAsync(Id(hold, "holdId"), [QuarantineScenario.Line(1, decision, 500m)], "QUALITY_REJECT", coa: false));
        Assert.Equal("Applied", Id(applied, "state"));
        AssertMovement(Assert.Single(await s.LedgerMovementsAsync(Id(applied, "decisionNumber"))), "STATUS_TRANSFER", "QUARANTINE", "BLOCKED", 500m);
        Assert.Equal(500m, await s.OnHandAsync("BLOCKED"));

        var blocked = await s.BlockedOfDecisionAsync(Id(applied, "decisionId"), 1);
        Assert.Equal("QuarantineDecision", Id(blocked, "origin"));
        Assert.Equal(pending, blocked.GetProperty("pendingDisposition").GetBoolean());
        Assert.Equal(reviewed, blocked.GetProperty("reviewed").GetBoolean());
        Assert.Equal("QUALITY_REJECT", Id(blocked.GetProperty("blockReason"), "code"));
        Assert.Equal(500m, QuarantineScenario.D(blocked.GetProperty("quantity")));
    }

    [Fact]
    public async Task A_partial_decision_posts_one_status_transfer_per_line_in_one_document_group()
    {
        var (s, _, hold) = await HoldOf("1000");
        var applied = await s.DecideAndApproveAsync(s.DecideAsync(Id(hold, "holdId"),
            [QuarantineScenario.Line(1, "Release", 600m), QuarantineScenario.Line(2, "Reject", 400m)], "DEVIATION"));
        Assert.Equal("Applied", Id(applied, "state"));

        var moves = (await s.LedgerMovementsAsync(Id(applied, "decisionNumber"))).OrderBy(m => m.GetProperty("sourceLineId").GetString()).ToList();
        Assert.Equal(2, moves.Count);
        AssertMovement(moves[0], "STATUS_TRANSFER", "QUARANTINE", "AVAILABLE", 600m);
        AssertMovement(moves[1], "STATUS_TRANSFER", "QUARANTINE", "BLOCKED", 400m);
        Assert.Equal(600m, await s.OnHandAsync("AVAILABLE"));
        Assert.Equal(400m, await s.OnHandAsync("BLOCKED"));
        Assert.Equal(0m, await s.OnHandAsync("QUARANTINE"));
        Assert.Equal(2, applied.GetProperty("appliedMovementIds").GetArrayLength());
    }

    /// <summary>
    /// CT-Q9 continue + retry: line 1 of an approved decision was already committed under its ledger key (the crash between the
    /// ledger posting and the 0175 record). The engine answers it from the ledger and posts only line 2 — nothing twice.
    /// K3 (e): an engine without the deterministic key re-posts line 1 → this test is red.
    /// </summary>
    [Fact]
    public async Task A_line_already_committed_under_its_key_is_never_posted_twice()
    {
        var (s, lot, hold) = await HoldOf("1000");
        var draft = await s.DecideAsync(Id(hold, "holdId"), [QuarantineScenario.Line(1, "Release", 600m), QuarantineScenario.Line(2, "Reject", 400m)], "DEVIATION");
        var decisionId = Id(draft, "decisionId");
        await s.SubmitAsync(decisionId);

        // the ledger already holds line 1, posted under quarantine:{decisionId}:1 (same envelope as the engine's)
        var line1 = s.Ledger.Movement("STATUS_TRANSFER", "600");
        line1["idempotencyKey"] = $"quarantine:{decisionId}:1";
        line1["lotId"] = lot;
        line1["fromWarehouseId"] = s.Ledger.Warehouse; line1["fromLocationId"] = s.Ledger.LocationA; line1["fromStockStatus"] = "QUARANTINE";
        line1["toWarehouseId"] = s.Ledger.Warehouse; line1["toLocationId"] = s.Ledger.LocationA; line1["toStockStatus"] = "AVAILABLE";
        line1["sourceModule"] = "MOD-0175"; line1["sourceType"] = "QUARANTINE_DECISION";
        line1["sourceDocumentId"] = Id(draft, "decisionNumber"); line1["sourceLineId"] = "1"; line1["documentGroupId"] = decisionId;
        await s.Lots.PostOkAsync(line1);

        var applied = await s.OutcomeAsync(decisionId);
        Assert.Equal("Applied", Id(applied, "state"));
        Assert.Equal(2, (await s.LedgerMovementsAsync(Id(draft, "decisionNumber"))).Count);
        Assert.Equal(600m, await s.OnHandAsync("AVAILABLE"));
        Assert.Equal(400m, await s.OnHandAsync("BLOCKED"));
        Assert.Equal(0m, await s.OnHandAsync("QUARANTINE"));
    }

    [Fact]
    public async Task An_apply_failure_stops_records_ApplyFailed_and_reapply_posts_exactly_once()
    {
        var (s, lot, hold) = await HoldOf("200");
        var draft = await s.DecideAsync(Id(hold, "holdId"), [QuarantineScenario.Line(1, "Release", 200m)]);
        var decisionId = Id(draft, "decisionId");
        await s.SubmitAsync(decisionId);

        await s.Lots.BlockAsync(lot); // §13 #12: the lot became Blocked before apply (CT-D4 refuses the STATUS_TRANSFER)
        var failed = await s.OutcomeAsync(decisionId);
        Assert.Equal("ApplyFailed", Id(failed, "state"));
        Assert.Equal("LOT_BLOCKED", Id(failed.GetProperty("error"), "code"));
        Assert.Empty(await s.LedgerMovementsAsync(Id(draft, "decisionNumber")));
        Assert.Equal(200m, await s.OnHandAsync("QUARANTINE"));

        await s.Lots.ReleaseAsync(lot);
        var applied = await s.ExpectAsync(HttpMethod.Post, $"/api/quarantine/decisions/{decisionId}/apply", null, 200);
        Assert.Equal("Applied", Id(applied, "state"));
        Assert.Single(await s.LedgerMovementsAsync(Id(draft, "decisionNumber")));
        var again = await s.ExpectAsync(HttpMethod.Post, $"/api/quarantine/decisions/{decisionId}/apply", null, 409);
        Assert.Equal("QUARANTINE_DECISION_NOT_APPLICABLE", QuarantineScenario.Code(again));
        Assert.Single(await s.LedgerMovementsAsync(Id(draft, "decisionNumber")));
        Assert.Equal(200m, await s.OnHandAsync("AVAILABLE"));
    }

    [Theory]
    [InlineData("Rejected")]
    [InlineData("Cancelled")]
    [InlineData("TimedOut")]
    public async Task A_decision_that_is_not_approved_moves_nothing_and_reopens_the_hold(string outcome)
    {
        var (s, _, hold) = await HoldOf("100");
        var draft = await s.DecideAsync(Id(hold, "holdId"), [QuarantineScenario.Line(1, "Release", 100m)]);
        await s.SubmitAsync(Id(draft, "decisionId"));
        var closed = await s.OutcomeAsync(Id(draft, "decisionId"), outcome);
        Assert.Equal(outcome, Id(closed, "state"));
        Assert.Empty(await s.LedgerMovementsAsync(Id(draft, "decisionNumber")));
        var reopened = await s.GetHoldAsync(Id(hold, "holdId"));
        Assert.Equal("Open", Id(reopened, "status"));
        Assert.False(reopened.TryGetProperty("activeDecision", out _));
        Assert.Equal(100m, await s.OnHandAsync("QUARANTINE"));
    }

    [Fact]
    public async Task The_decider_approving_their_own_decision_is_a_four_eyes_violation_and_moves_nothing()
    {
        var (s, _, hold) = await HoldOf("100");
        var draft = await s.DecideAsync(Id(hold, "holdId"), [QuarantineScenario.Line(1, "Release", 100m)]);
        await s.SubmitAsync(Id(draft, "decisionId"));
        var result = await s.OutcomeAsync(Id(draft, "decisionId"), approver: s.Ledger.User); // the decider
        Assert.Equal("FourEyesViolation", Id(result, "state"));
        Assert.Empty(await s.LedgerMovementsAsync(Id(draft, "decisionNumber")));
        Assert.Equal("Open", Id(await s.GetHoldAsync(Id(hold, "holdId")), "status"));
    }

    // ── dispositions and unblock (the blocked record comes from a Reject) ────────────────────────────────

    private async Task<(QuarantineScenario S, string Lot, JsonElement Blocked)> RejectedOf(string quantity)
    {
        var (s, lot, hold) = await HoldOf(quantity);
        var applied = await s.DecideAndApproveAsync(s.DecideAsync(Id(hold, "holdId"), [QuarantineScenario.Line(1, "Reject", decimal.Parse(quantity))], "QUALITY_REJECT", coa: false));
        return (s, lot, await s.BlockedOfDecisionAsync(Id(applied, "decisionId"), 1));
    }

    [Fact]
    public async Task Destroy_posts_SCRAP_from_blocked_and_the_rest_stays_blocked()
    {
        var (s, _, blocked) = await RejectedOf("150");
        var applied = await s.DecideAndApproveAsync(s.DisposeAsync(Id(blocked, "blockedId"),
            new { kind = "Destroy", quantity = 100m, destructionMethod = "Yakma (lisanslı firma)", destructionDate = DateTime.UtcNow.ToString("yyyy-MM-dd") }));
        Assert.Equal("Applied", Id(applied, "state"));
        AssertMovement(Assert.Single(await s.LedgerMovementsAsync(Id(applied, "decisionNumber"))), "SCRAP", "BLOCKED", null, 100m);
        Assert.Equal(50m, await s.OnHandAsync("BLOCKED"));
        var after = await s.ExpectAsync(HttpMethod.Get, $"/api/quarantine/blocked/{Id(blocked, "blockedId")}", null, 200);
        Assert.Equal(50m, QuarantineScenario.D(after.GetProperty("quantity")));
        Assert.Equal("Open", Id(after, "status"));
    }

    [Fact]
    public async Task Return_to_supplier_posts_SUPPLIER_RETURN_from_blocked_and_closes_the_record()
    {
        var (s, _, blocked) = await RejectedOf("80");
        var applied = await s.DecideAndApproveAsync(s.DisposeAsync(Id(blocked, "blockedId"),
            new { kind = "ReturnToSupplier", quantity = 80m, supplierId = Guid.NewGuid().ToString("D"), returnDocumentNo = "IADE-2026-0091" }));
        AssertMovement(Assert.Single(await s.LedgerMovementsAsync(Id(applied, "decisionNumber"))), "SUPPLIER_RETURN", "BLOCKED", null, 80m);
        Assert.Equal(0m, await s.OnHandAsync("BLOCKED"));
        Assert.Equal("Closed", Id(await s.ExpectAsync(HttpMethod.Get, $"/api/quarantine/blocked/{Id(blocked, "blockedId")}", null, 200), "status"));
    }

    [Fact]
    public async Task Rework_posts_STATUS_TRANSFER_blocked_to_quarantine_and_opens_a_rework_hold()
    {
        var (s, _, blocked) = await RejectedOf("60");
        var applied = await s.DecideAndApproveAsync(s.DisposeAsync(Id(blocked, "blockedId"), new { kind = "Rework", quantity = 60m }));
        var move = Assert.Single(await s.LedgerMovementsAsync(Id(applied, "decisionNumber")));
        AssertMovement(move, "STATUS_TRANSFER", "BLOCKED", "QUARANTINE", 60m, s.Ledger.LocationA);
        Assert.Equal(60m, await s.OnHandAsync("QUARANTINE"));
        var rework = await s.HoldBySourceAsync(Id(move, "transactionId"));
        Assert.Equal("Rework", Id(rework, "source"));
        Assert.Equal(60m, QuarantineScenario.D(rework.GetProperty("quantity")));
    }

    /// <summary>KQ-E / §13 #13–14: Sample = LOCATION_TRANSFER to the company's sample location, the stock stays BLOCKED, no new status.</summary>
    [Fact]
    public async Task Sample_needs_a_sample_location_and_moves_the_stock_there_still_blocked()
    {
        var (s, _, blocked) = await RejectedOf("30");
        var refused = await s.DisposeAsync(Id(blocked, "blockedId"), new { kind = "Sample", quantity = 10m }, 409);
        Assert.Equal("QUARANTINE_SAMPLE_LOCATION_NOT_SET", QuarantineScenario.Code(refused));

        await s.SaveSettingsAsync(new
        {
            approverCandidates = new[] { "position:" + Guid.NewGuid().ToString("D") },
            sampleLocations = new Dictionary<string, string> { [s.Ledger.Warehouse] = s.Ledger.LocationB }
        });
        var applied = await s.DecideAndApproveAsync(s.DisposeAsync(Id(blocked, "blockedId"), new { kind = "Sample", quantity = 10m }));
        AssertMovement(Assert.Single(await s.LedgerMovementsAsync(Id(applied, "decisionNumber"))), "LOCATION_TRANSFER", "BLOCKED", "BLOCKED", 10m, s.Ledger.LocationB);
        Assert.Equal(10m, await s.OnHandAsync("BLOCKED", s.Ledger.LocationB));
        Assert.Equal(20m, await s.OnHandAsync("BLOCKED", s.Ledger.LocationA));
        var sample = Assert.Single(s.Blocked(), b => b["Status"].AsString == "SampleRetained");
        Assert.Equal(s.Ledger.LocationB, sample["LocationId"].AsString);
    }

    [Fact]
    public async Task Unblock_needs_a_completed_review_and_posts_STATUS_TRANSFER_blocked_to_available()
    {
        var (s, lot, hold) = await HoldOf("70");
        var applied = await s.DecideAndApproveAsync(s.DecideAsync(Id(hold, "holdId"), [QuarantineScenario.Line(1, "Block", 70m)], "DAMAGE", coa: false));
        var blocked = await s.BlockedOfDecisionAsync(Id(applied, "decisionId"), 1);
        var refused = await s.UnblockAsync(Id(blocked, "blockedId"), 409);
        Assert.Equal("QUARANTINE_REVIEW_NOT_COMPLETED", QuarantineScenario.Code(refused));

        var (_, _, raw) = await s.SendAsync(HttpMethod.Get, $"/api/quarantine/blocked/{Id(blocked, "blockedId")}");
        var key = QuarantineScenario.Key("rev");
        await s.ExpectAsync(HttpMethod.Post, $"/api/quarantine/blocked/{Id(blocked, "blockedId")}/review-complete",
            new { reason = QuarantineScenario.Reason, idempotencyKey = key }, 200, key, raw.Headers.ETag!.ToString());

        var unblocked = await s.DecideAndApproveAsync(s.UnblockAsync(Id(blocked, "blockedId")));
        AssertMovement(Assert.Single(await s.LedgerMovementsAsync(Id(unblocked, "decisionNumber"))), "STATUS_TRANSFER", "BLOCKED", "AVAILABLE", 70m);
        Assert.Equal(70m, await s.OnHandAsync("AVAILABLE"));
        Assert.Equal("Closed", Id(await s.ExpectAsync(HttpMethod.Get, $"/api/quarantine/blocked/{Id(blocked, "blockedId")}", null, 200), "status"));
    }

    // ── no-workflow safety actions ───────────────────────────────────────────────────────────────────────

    private static object Bucket(QuarantineScenario s, string lot, decimal quantity)
        => new { itemId = s.Ledger.Item, skuId = s.Ledger.Sku, skuLevel = "Lsku", warehouseId = s.Ledger.Warehouse, locationId = s.Ledger.LocationA, quantity, lotId = lot };

    [Fact]
    public async Task Karantinaya_Al_posts_available_to_quarantine_and_a_repeat_answers_from_the_record()
    {
        var s = new QuarantineScenario(_factory);
        var lot = await s.ReleasedLotAsync();
        await s.ReceiveAsync("100", lot, "AVAILABLE");
        var key = QuarantineScenario.Key("hold");
        var body = new { mode = "Manual", reason = QuarantineScenario.Reason, idempotencyKey = key, buckets = new[] { Bucket(s, lot, 40m) } };

        var holds = await s.ExpectAsync(HttpMethod.Post, "/api/quarantine/holds", body, 201, key);
        var hold = Assert.Single(holds.EnumerateArray());
        Assert.Equal("Manual", Id(hold, "source"));
        AssertMovement(Assert.Single(await s.LedgerMovementsAsync(Id(hold, "holdNumber"))), "STATUS_TRANSFER", "AVAILABLE", "QUARANTINE", 40m);
        Assert.Equal(60m, await s.OnHandAsync("AVAILABLE"));
        Assert.Equal(40m, await s.OnHandAsync("QUARANTINE"));

        var replay = await s.ExpectAsync(HttpMethod.Post, "/api/quarantine/holds", body, 200, key);
        Assert.Equal(Id(hold, "holdId"), Id(Assert.Single(replay.EnumerateArray()), "holdId"));
        Assert.Single(await s.LedgerMovementsAsync(Id(hold, "holdNumber")));
        Assert.Equal(40m, await s.OnHandAsync("QUARANTINE"));

        var other = await s.ExpectAsync(HttpMethod.Post, "/api/quarantine/holds",
            new { mode = "Manual", reason = QuarantineScenario.Reason, idempotencyKey = key, buckets = new[] { Bucket(s, lot, 41m) } }, 400, key);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", QuarantineScenario.Code(other));
    }

    [Fact]
    public async Task Stogu_bloke_et_posts_available_to_blocked_with_an_unreviewed_manual_record()
    {
        var s = new QuarantineScenario(_factory);
        var lot = await s.ReleasedLotAsync();
        await s.ReceiveAsync("100", lot, "AVAILABLE");
        var key = QuarantineScenario.Key("blk");
        var records = await s.ExpectAsync(HttpMethod.Post, "/api/quarantine/blocked",
            new { blockReasonCode = "DAMAGE", reason = QuarantineScenario.Reason, idempotencyKey = key, buckets = new[] { Bucket(s, lot, 25m) } }, 201, key);
        var record = Assert.Single(records.EnumerateArray());
        Assert.Equal("ManualBlock", Id(record, "origin"));
        Assert.False(record.GetProperty("reviewed").GetBoolean());
        AssertMovement(Assert.Single(await s.LedgerMovementsAsync(Id(record, "blockedNumber"))), "STATUS_TRANSFER", "AVAILABLE", "BLOCKED", 25m);
        Assert.Equal(25m, await s.OnHandAsync("BLOCKED"));
    }

    /// <summary>CT-Q6 / V18 / §13 #17: reserved stock is never uncovered — nothing posted, the reservations are listed.</summary>
    [Theory]
    [InlineData("/api/quarantine/holds")]
    [InlineData("/api/quarantine/blocked")]
    public async Task Taking_reserved_stock_out_of_use_is_refused_with_the_reservation_list(string path)
    {
        var s = new QuarantineScenario(_factory);
        var lot = await s.ReleasedLotAsync();
        await s.ReceiveAsync("100", lot, "AVAILABLE");
        await s.Lots.ReserveAsync("80", 201);
        var before = await s.AllMovementCountAsync();
        var key = QuarantineScenario.Key("rsv");
        object body = path.EndsWith("holds", StringComparison.Ordinal)
            ? new { mode = "Manual", reason = QuarantineScenario.Reason, idempotencyKey = key, buckets = new[] { Bucket(s, lot, 50m) } }
            : new { blockReasonCode = "OTHER", reason = QuarantineScenario.Reason, idempotencyKey = key, buckets = new[] { Bucket(s, lot, 50m) } };

        var refused = await s.ExpectAsync(HttpMethod.Post, path, body, 409, key);
        Assert.Equal("QUARANTINE_STOCK_RESERVED", QuarantineScenario.Code(refused));
        var reservations = refused.GetProperty("error").GetProperty("details").GetProperty("reservations");
        Assert.Equal(1, reservations.GetArrayLength());
        Assert.Equal(before, await s.AllMovementCountAsync());
        Assert.Equal(100m, await s.OnHandAsync("AVAILABLE"));
        Assert.Empty(s.Holds());
    }
}
