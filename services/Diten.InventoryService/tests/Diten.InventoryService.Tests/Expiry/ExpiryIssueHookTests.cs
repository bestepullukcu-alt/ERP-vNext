using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Expiry;

/// <summary>
/// WP-0176-F2 — the FEFO hook of the posting path (pack §8.3.1, §8.3.4 row "Outbound"; KF-B; CT-F2 / CT-F3 / CT-F4 / CT-F10), measured through the
/// REAL posting endpoint (<c>POST /api/inventory/movements</c>, production controller, the validator pre-check and the ledger transaction
/// re-check). Two lots of the same SKU in the same warehouse: L1 expires first. Every response is validated against INVENTORY-BUNDLE 1.3.0.
/// </summary>
public sealed class ExpiryIssueHookTests : IClassFixture<ExpiryApiFactory>
{
    private const string Reason = "Musteri talebi: uzun raf omurlu lot istendi";

    private readonly ExpiryApiFactory _factory;

    public ExpiryIssueHookTests(ExpiryApiFactory factory) => _factory = factory;

    private sealed record World(ExpiryScenario S, string L1, string L2);

    /// <summary>L1 (expiry 2027-01-31) and L2 (2027-06-30), 10 each in AVAILABLE at bin A of the scenario warehouse.</summary>
    private async Task<World> TwoLotsAsync(bool stockInL1 = true)
    {
        var s = new ExpiryScenario(_factory);
        var l1 = await s.LotAsync("2027-01-31T00:00:00Z");
        var l2 = await s.LotAsync("2027-06-30T00:00:00Z");
        if (stockInL1) await s.Lots.PostOkAsync(s.Lots.Receipt("10", l1));
        await s.Lots.PostOkAsync(s.Lots.Receipt("10", l2));
        return new World(s, l1, l2);
    }

    private static Dictionary<string, object?> WithReason(Dictionary<string, object?> body, string? reason)
    {
        if (reason is not null) body["fefoBypassReason"] = reason;
        return body;
    }

    private static string Code(JsonElement e) => e.GetProperty("error").GetProperty("code").GetString()!;

    private List<BsonDocument> Cases(ExpiryScenario s, string type)
        => s.Persisted("expiry_exceptions").Where(d => d["Type"].AsString == type).ToList();

    // ── §8.3.4 Outbound × Gerekçe iste (default) ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RequireReason_default_refuses_an_unreasoned_bypass_and_posts_a_reasoned_one_with_exception_and_snapshot()
    {
        var (s, l1, l2) = await TwoLotsAsync();

        // K3 sabotage (a) is measured by Skipping_a_below_minimum_lot_needs_no_reason (the pre-check alone knows the SKU minimum).
        var refused = await s.Lots.PostAsync(s.Lots.Issue("1", l2), 422);
        Assert.Equal("FEFO_BYPASS_REASON_REQUIRED", Code(refused));
        var details = refused.GetProperty("error").GetProperty("details");
        Assert.Equal(l1, details.GetProperty("firstEligibleLotId").GetString());
        Assert.Equal("2027-01-31", details.GetProperty("firstEligibleExpiry").GetString());
        var shortReason = await s.Lots.PostAsync(WithReason(s.Lots.Issue("1", l2), "kisa"), 422);
        Assert.Equal("FEFO_BYPASS_REASON_REQUIRED", Code(shortReason));
        Assert.Equal(2, s.Persisted("inventory_transactions").Count); // the two receipts — no movement (pack §13 #1)
        Assert.Empty(s.Persisted("expiry_fefo_issue_snapshots"));
        Assert.Empty(s.Persisted("expiry_exceptions"));
        Assert.Equal(2, s.Audit("expiry.fefo.refused").Count);

        var body = WithReason(s.Lots.Issue("1", l2), Reason);
        var posted = await s.Lots.PostAsync(body, 201);
        var checks = posted.GetProperty("expiryChecks");
        Assert.Equal("Bypassed", checks.GetProperty("fefo").GetString());
        var exceptionId = Assert.Single(checks.GetProperty("exceptionIds").EnumerateArray()).GetString()!;

        var c = Assert.Single(Cases(s, "FefoBypass"));
        Assert.Equal(exceptionId, c["_id"].AsString);
        Assert.Equal("System", c["Origin"].AsString);
        Assert.Equal(s.Ledger.User.ToString(), c["OriginUserId"].AsString); // CT-F10: the poster
        Assert.Equal(l1, c["Facts"]["firstEligibleLotId"].AsString);
        Assert.Equal(Reason, c["Facts"]["bypassReason"].AsString);
        Assert.Equal(posted.GetProperty("transactionId").GetString(), c["SourceMovement"]["TransactionId"].AsString);
        Assert.Equal($"FEFO_BYPASS:{posted.GetProperty("transactionId").GetString()}", c["DedupKey"].AsString);
        var snapshot = Assert.Single(s.Persisted("expiry_fefo_issue_snapshots"));
        Assert.True(snapshot["Bypassed"].AsBoolean);
        Assert.Equal(l1, snapshot["FirstEligibleLotId"].AsString);
        Assert.Equal(exceptionId, snapshot["ExceptionId"].AsString);

        // idempotent replay: the first answer (same checks), no second exception / snapshot
        var replay = await s.Lots.PostAsync(body, 200);
        Assert.Equal(exceptionId, replay.GetProperty("expiryChecks").GetProperty("exceptionIds")[0].GetString());
        Assert.Single(Cases(s, "FefoBypass"));
        Assert.Single(s.Persisted("expiry_fefo_issue_snapshots"));
        // the same key without the reason is another request
        var reused = await s.Lots.PostAsync(Rekey(s.Lots.Issue("1", l2), (string)body["idempotencyKey"]!), 400);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", Code(reused));
    }

    private static Dictionary<string, object?> Rekey(Dictionary<string, object?> body, string key)
    {
        body["idempotencyKey"] = key;
        return body;
    }

    [Fact]
    public async Task A_compliant_issue_posts_with_a_compliant_snapshot_and_no_exception()
    {
        var (s, l1, _) = await TwoLotsAsync();
        var posted = await s.Lots.PostAsync(s.Lots.Issue("1", l1), 201);
        Assert.Equal("Compliant", posted.GetProperty("expiryChecks").GetProperty("fefo").GetString());
        Assert.Empty(posted.GetProperty("expiryChecks").GetProperty("exceptionIds").EnumerateArray());
        var snapshot = Assert.Single(s.Persisted("expiry_fefo_issue_snapshots"));
        Assert.True(snapshot["Compliant"].AsBoolean);
        Assert.Equal(posted.GetProperty("transactionId").GetString(), snapshot["TransactionId"].AsString);
        Assert.Empty(s.Persisted("expiry_exceptions"));
    }

    [Fact]
    public async Task Equal_expiry_is_compliant_whatever_the_tie_break_order()
    {
        var s = new ExpiryScenario(_factory);
        // same expiry; L-early is made earlier in the tie-break (manufacturing date), the issue takes L-late
        var early = await s.Lots.CreateLotAsync(expiryDate: "2027-03-31T00:00:00Z", manufacturingDate: "2026-01-01T00:00:00Z");
        var late = await s.Lots.CreateLotAsync(expiryDate: "2027-03-31T00:00:00Z", manufacturingDate: "2026-02-01T00:00:00Z");
        foreach (var lot in new[] { early, late })
        {
            await s.Lots.ReleaseAsync(lot);
            await s.Lots.PostOkAsync(s.Lots.Receipt("5", lot));
        }

        // K3 sabotage (d): compare the tie-break rank instead of the expiry → this issue becomes a bypass (422)
        var posted = await s.Lots.PostAsync(s.Lots.Issue("1", late), 201);
        Assert.Equal("Compliant", posted.GetProperty("expiryChecks").GetProperty("fefo").GetString());
    }

    // ── §8.3.4 Outbound × Uyar / Engelle ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Warn_posts_the_bypass_with_an_exception_without_a_reason()
    {
        var (s, _, l2) = await TwoLotsAsync();
        Assert.Equal(200, (await s.PutPolicyAsync(ExpiryScenario.Policy(fefo: "Warn"), "\"0\"")).Status);
        var posted = await s.Lots.PostAsync(s.Lots.Issue("1", l2), 201);
        Assert.Equal("Bypassed", posted.GetProperty("expiryChecks").GetProperty("fefo").GetString());
        Assert.Equal("—", Assert.Single(Cases(s, "FefoBypass"))["Facts"]["bypassReason"].AsString);
    }

    [Fact]
    public async Task Block_refuses_the_bypass_even_with_a_reason_and_audits_the_refusal()
    {
        var (s, l1, l2) = await TwoLotsAsync();
        Assert.Equal(200, (await s.PutPolicyAsync(ExpiryScenario.Policy(fefo: "Block"), "\"0\"")).Status);
        var refused = await s.Lots.PostAsync(WithReason(s.Lots.Issue("1", l2), Reason), 422);
        Assert.Equal("FEFO_ORDER_VIOLATION", Code(refused));
        Assert.Equal(20m, await s.Ledger.OnHandAsync()); // nothing left the location (L1 10 + L2 10)
        Assert.Equal(10m, s.Lots.LotPositionOnHand(l2));
        _ = l1;
        var audit = Assert.Single(s.Audit("expiry.fefo.refused"));
        Assert.Equal("Block", audit["Detail"]["mode"].AsString);
        Assert.Empty(s.Persisted("expiry_exceptions"));
        Assert.Empty(s.Persisted("expiry_fefo_issue_snapshots"));
    }

    // ── exemptions (pack §8.3, CT-D4, CT-F3) and the same path for MVP-6 ────────────────────────────────

    [Fact]
    public async Task A_reversal_of_a_later_lot_receipt_is_exempt()
    {
        var s = new ExpiryScenario(_factory);
        var l1 = await s.LotAsync("2027-01-31T00:00:00Z");
        var l2 = await s.LotAsync("2027-06-30T00:00:00Z");
        await s.Lots.PostOkAsync(s.Lots.Receipt("10", l1));
        var receipt = s.Lots.Receipt("4", l2);
        var receiptId = await s.Lots.PostOkAsync(receipt);

        // the reversal takes 4 of L2 out of AVAILABLE while L1 expires earlier — K3 sabotage (c): drop the REVERSAL exemption → 422
        var reversal = await s.Lots.PostAsync(s.Lots.Reversal(receipt, receiptId), 201);
        Assert.False(reversal.TryGetProperty("expiryChecks", out _));
        Assert.Empty(s.Persisted("expiry_fefo_issue_snapshots"));
    }

    [Fact]
    public async Task The_reversal_of_a_bypassing_issue_is_not_checked_and_leaves_the_exception_open()
    {
        // pack §13 #9 + CT-F4: the reversal puts L2 back (exempt type) and does not close the FEFO_BYPASS case
        var (s, _, l2) = await TwoLotsAsync();
        var issue = WithReason(s.Lots.Issue("2", l2), Reason);
        var issueId = await s.Lots.PostOkAsync(issue);
        var reversal = await s.Lots.PostAsync(s.Lots.Reversal(issue, issueId), 201);
        Assert.False(reversal.TryGetProperty("expiryChecks", out _));
        Assert.Equal("Open", Assert.Single(Cases(s, "FefoBypass"))["Status"].AsString);
        Assert.Single(s.Persisted("expiry_fefo_issue_snapshots"));
        Assert.Equal(10m, s.Lots.LotPositionOnHand(l2));
    }

    [Fact]
    public async Task A_quarantine_status_transfer_of_a_later_lot_is_not_checked()
    {
        // pack §13 #10: MOD-0175 moves a later lot AVAILABLE → QUALITY_INSPECTION while an earlier lot is eligible
        var (s, _, l2) = await TwoLotsAsync();
        var transfer = s.Lots.StatusTransfer("4", l2);
        transfer["sourceModule"] = "MOD-0175";
        var posted = await s.Lots.PostAsync(transfer, 201);
        Assert.False(posted.TryGetProperty("expiryChecks", out _));
        Assert.Empty(s.Persisted("expiry_fefo_issue_snapshots"));
        Assert.Empty(s.Persisted("expiry_exceptions"));
    }

    [Fact]
    public async Task Transfers_and_quarantine_movements_are_exempt()
    {
        var (s, _, l2) = await TwoLotsAsync();
        var transfer = await s.Lots.PostAsync(s.Lots.Transfer("1", l2), 201); // LOCATION_TRANSFER
        Assert.False(transfer.TryGetProperty("expiryChecks", out _));

        // a GOODS_ISSUE_SALES posted by MOD-0175 (sourceModule) — K3 sabotage (i): drop the MOD-0175 exemption → 422
        var byQuarantine = s.Lots.Issue("1", l2);
        byQuarantine["sourceModule"] = "MOD-0175";
        var posted = await s.Lots.PostAsync(byQuarantine, 201);
        Assert.False(posted.TryGetProperty("expiryChecks", out _));
        Assert.Empty(s.Persisted("expiry_fefo_issue_snapshots"));
    }

    [Fact]
    public async Task The_MVP_6_shipment_issue_goes_through_the_same_hook()
    {
        var (s, _, l2) = await TwoLotsAsync();
        var shipment = s.Lots.Issue("1", l2);
        shipment["sourceModule"] = "MOD-0183";
        shipment["sourceType"] = "SHIPMENT";
        shipment["sourceDocumentId"] = Guid.NewGuid().ToString("D");
        Assert.Equal("FEFO_BYPASS_REASON_REQUIRED", Code(await s.Lots.PostAsync(shipment, 422)));
        shipment["fefoBypassReason"] = Reason;
        var posted = await s.Lots.PostAsync(shipment, 201);
        Assert.Equal("Bypassed", posted.GetProperty("expiryChecks").GetProperty("fefo").GetString());
        Assert.Equal("MOD-0183", Assert.Single(Cases(s, "FefoBypass"))["SourceMovement"]["SourceModule"].AsString);
    }

    // ── exclusions (pack §8.2) ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_blocked_earlier_lot_is_not_eligible()
    {
        var (s, l1, l2) = await TwoLotsAsync();
        await s.Lots.BlockAsync(l1);
        var posted = await s.Lots.PostAsync(s.Lots.Issue("1", l2), 201);
        Assert.Equal("Compliant", posted.GetProperty("expiryChecks").GetProperty("fefo").GetString());
    }

    [Fact]
    public async Task Skipping_a_below_minimum_lot_needs_no_reason()
    {
        // L1 has 40 days left at the posting date (2026-09-30), below the SKU minimum of 90: it is excluded from the FEFO order, so issuing L2
        // is compliant. Only the pre-check reads the SKU minimum (getSku) — K3 sabotage (a): drop the pre-check call → L1 counts → 422.
        var s = new ExpiryScenario(_factory);
        _factory.ShelfLife[s.Ledger.Sku] = (null, null, 90);
        Assert.Equal(200, (await s.PutPolicyAsync(ExpiryScenario.Policy(below: "Warn"), "\"0\"")).Status);
        var l1 = await s.LotAsync("2026-11-09T00:00:00Z");
        var l2 = await s.LotAsync("2027-06-30T00:00:00Z");
        await s.Lots.PostOkAsync(s.Lots.Receipt("5", l1, movementType: "GOODS_RECEIPT_PO"));
        await s.Lots.PostOkAsync(s.Lots.Receipt("5", l2, movementType: "GOODS_RECEIPT_PO"));
        var posted = await s.Lots.PostAsync(s.Lots.Issue("1", l2), 201);
        Assert.Equal("Compliant", posted.GetProperty("expiryChecks").GetProperty("fefo").GetString());
    }

    // ── the in-transaction re-check is authoritative (race) and the exception commits with the movement (crash) ─────────

    [Fact]
    public async Task A_lot_that_gets_stock_between_pre_check_and_commit_is_seen_by_the_re_check()
    {
        var (s, l1, l2) = await TwoLotsAsync(stockInL1: false); // only L2 has stock: the pre-check says compliant
        _factory.ArmAfterPreCheck(async () =>
        {
            // meanwhile L1 (earlier) receives stock
            using var response = await s.Ledger.PostAsync(s.Lots.Receipt("3", l1), token: s.Lots.Token);
            Assert.Equal(201, (int)response.StatusCode);
        });

        // K3 sabotage (b): drop the in-transaction re-check → this posts (201) on the stale pre-check verdict
        var refused = await s.Lots.PostAsync(s.Lots.Issue("1", l2), 422);
        Assert.Equal("FEFO_BYPASS_REASON_REQUIRED", Code(refused));
        Assert.Equal(l1, refused.GetProperty("error").GetProperty("details").GetProperty("firstEligibleLotId").GetString());
        Assert.Empty(s.Persisted("expiry_fefo_issue_snapshots"));
    }

    [Fact]
    public async Task If_the_exception_cannot_be_written_the_movement_is_not_posted_either()
    {
        var (s, _, l2) = await TwoLotsAsync();
        // crash injection: the FX counter of the tenant is poisoned, so writing the FEFO_BYPASS case fails inside the posting transaction
        var counterId = $"{s.Tenant:N}:FX:{_factory.Clock.GetUtcNow().UtcDateTime.Year}";
        _factory.Collection("expiry_counters").InsertOne(new BsonDocument { ["_id"] = counterId, ["Seq"] = "poison", ["TenantId"] = s.Tenant.ToString() });
        var body = WithReason(s.Lots.Issue("1", l2), Reason);
        var before = s.Persisted("inventory_transactions").Count;

        using var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/inventory/movements") { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", (string)body["idempotencyKey"]!);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.Lots.Token);
        using var crashed = await client.SendAsync(request);

        // K3 sabotage (f): write the exception best-effort outside the posting (post-commit semantics) → the movement stands without it
        Assert.NotEqual(201, (int)crashed.StatusCode);
        Assert.Equal(before, s.Persisted("inventory_transactions").Count);
        Assert.Empty(s.Persisted("expiry_exceptions"));
        Assert.Empty(s.Persisted("expiry_fefo_issue_snapshots"));

        _factory.Collection("expiry_counters").DeleteOne(new BsonDocument("_id", counterId));
        var posted = await s.Lots.PostAsync(body, 201); // the same request, now able to write its exception
        Assert.Equal("Bypassed", posted.GetProperty("expiryChecks").GetProperty("fefo").GetString());
        Assert.Single(Cases(s, "FefoBypass"));
    }

    // ── CT-F10 on a hook case ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_poster_of_a_bypass_cannot_resolve_its_exception()
    {
        var (s, _, l2) = await TwoLotsAsync();
        var posted = await s.Lots.PostAsync(WithReason(s.Lots.Issue("1", l2), Reason), 201);
        var id = posted.GetProperty("expiryChecks").GetProperty("exceptionIds")[0].GetString()!;
        var poster = s.Mint(s.Ledger.User, ExpiryScenario.QaKeys);

        var c = await s.GetAsync(id);
        // K3 sabotage (h): do not write the origin user on the hook case → the poster may prepare (200)
        var (status, refused, _) = await s.PrepareAsync(id, "AcceptedWithReason", ExpiryScenario.ETag(c), poster);
        Assert.Equal(409, status);
        Assert.Equal("EXPIRY_SOD_VIOLATION", ExpiryScenario.Code(refused));
        Assert.Equal(200, (await s.PrepareAsync(id, "AcceptedWithReason", ExpiryScenario.ETag(c))).Status); // QA (another user) may
    }
}
