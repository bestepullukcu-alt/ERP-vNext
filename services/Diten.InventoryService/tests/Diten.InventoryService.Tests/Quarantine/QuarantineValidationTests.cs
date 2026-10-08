using System.Text.Json;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Rules;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Quarantine;

/// <summary>
/// MOD-0175 pack §12 V1–V14 through the REAL API (each refusal answers its contract code and changes nothing) — §13 failure
/// paths 1–5, 13, 15, 16, 21 — plus the pure rules (KQ-A matrix, V9, V10–V12) on <see cref="QuarantineDecisionRules"/>.
/// </summary>
public sealed class QuarantineValidationTests : IClassFixture<QuarantineApiFactory>
{
    private readonly QuarantineApiFactory _factory;

    public QuarantineValidationTests(QuarantineApiFactory factory) => _factory = factory;

    private static string Id(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    private async Task<(QuarantineScenario S, string Lot, string HoldId)> HoldOf(string quantity)
    {
        var s = new QuarantineScenario(_factory);
        var lot = await s.ReleasedLotAsync();
        var (_, hold) = await s.HoldAsync(quantity, lot);
        return (s, lot, Id(hold, "holdId"));
    }

    private static async Task Refused(QuarantineScenario s, object body, int status, string code)
    {
        var key = QuarantineScenario.Key("v");
        var b = JsonSerializer.SerializeToNode(body)!.AsObject();
        b["idempotencyKey"] = key;
        var json = await s.ExpectAsync(HttpMethod.Post, "/api/quarantine/decisions", b, status, key);
        Assert.Equal(code, QuarantineScenario.Code(json));
        Assert.Empty(s.Decisions());
    }

    [Fact]
    public async Task V1_V2_lines_must_cover_the_hold_quantity()
    {
        var (s, _, hold) = await HoldOf("1200");
        // §13 #1: 700 + 400 on 1 200
        await Refused(s, new { kind = "HoldDecision", targetId = hold, reason = QuarantineScenario.Reason, coaChecked = true, blockReasonCode = "DEVIATION",
            lines = new[] { QuarantineScenario.Line(1, "Release", 700m), QuarantineScenario.Line(2, "Reject", 400m) } }, 400, "QUARANTINE_PARTIAL_SUM_MISMATCH");
        await Refused(s, new { kind = "HoldDecision", targetId = hold, reason = QuarantineScenario.Reason, coaChecked = true,
            lines = new[] { QuarantineScenario.Line(1, "Release", 1000m) } }, 400, "QUARANTINE_DECISION_INVALID");
        await Refused(s, new { kind = "HoldDecision", targetId = hold, reason = QuarantineScenario.Reason, coaChecked = true,
            lines = new[] { QuarantineScenario.Line(1, "Release", 600m), QuarantineScenario.Line(2, "Release", 600m) } }, 400, "QUARANTINE_DECISION_INVALID");
    }

    [Fact]
    public async Task V3_V4_V5_reason_coa_and_block_reason()
    {
        var (s, _, hold) = await HoldOf("100");
        await Refused(s, new { kind = "HoldDecision", targetId = hold, reason = "too short", coaChecked = true,
            lines = new[] { QuarantineScenario.Line(1, "Release", 100m) } }, 400, "REASON_REQUIRED"); // §13 #4 (9 chars)
        await Refused(s, new { kind = "HoldDecision", targetId = hold, reason = QuarantineScenario.Reason, coaChecked = false,
            lines = new[] { QuarantineScenario.Line(1, "Release", 100m) } }, 400, "QUARANTINE_COA_NOT_CONFIRMED"); // §13 #2
        await Refused(s, new { kind = "HoldDecision", targetId = hold, reason = QuarantineScenario.Reason,
            lines = new[] { QuarantineScenario.Line(1, "Reject", 100m) } }, 400, "QUARANTINE_BLOCK_REASON_REQUIRED"); // §13 #3
        await Refused(s, new { kind = "HoldDecision", targetId = hold, reason = QuarantineScenario.Reason, blockReasonCode = "NOT_A_REASON",
            lines = new[] { QuarantineScenario.Line(1, "Block", 100m) } }, 400, "QUARANTINE_BLOCK_REASON_REQUIRED");
    }

    [Fact]
    public async Task V6_one_active_decision_per_hold_and_a_decided_hold_is_closed()
    {
        var (s, _, hold) = await HoldOf("100");
        var first = await s.DecideAsync(hold, [QuarantineScenario.Line(1, "Release", 100m)]);
        await s.SubmitAsync(Id(first, "decisionId"));
        // §13 #5: a second decision while one is pending
        await RefusedAfterOne(s, new { kind = "HoldDecision", targetId = hold, reason = QuarantineScenario.Reason, coaChecked = true,
            lines = new[] { QuarantineScenario.Line(1, "Release", 100m) } }, 409, "QUARANTINE_DECISION_ALREADY_PENDING");

        await s.OutcomeAsync(Id(first, "decisionId"));
        await RefusedAfterOne(s, new { kind = "HoldDecision", targetId = hold, reason = QuarantineScenario.Reason, coaChecked = true,
            lines = new[] { QuarantineScenario.Line(1, "Release", 100m) } }, 409, "QUARANTINE_HOLD_CLOSED");
    }

    private static async Task RefusedAfterOne(QuarantineScenario s, object body, int status, string code)
    {
        var key = QuarantineScenario.Key("v");
        var b = JsonSerializer.SerializeToNode(body)!.AsObject();
        b["idempotencyKey"] = key;
        var before = s.Decisions().Count;
        Assert.Equal(code, QuarantineScenario.Code(await s.ExpectAsync(HttpMethod.Post, "/api/quarantine/decisions", b, status, key)));
        Assert.Equal(before, s.Decisions().Count);
    }

    [Fact]
    public async Task V7_the_bucket_must_still_hold_the_quantity()
    {
        var (s, lot, hold) = await HoldOf("100");
        await s.Lots.PostOkAsync(s.Lots.Issue("30", lot, movementType: "SCRAP", fromStatus: "QUARANTINE")); // stock moved meanwhile
        await Refused(s, new { kind = "HoldDecision", targetId = hold, reason = QuarantineScenario.Reason, coaChecked = true,
            lines = new[] { QuarantineScenario.Line(1, "Release", 100m) } }, 409, "QUARANTINE_STOCK_CHANGED");
    }

    [Fact]
    public async Task V8_on_a_lot_blocked_lot_only_destruction_is_possible()
    {
        var (s, lot, hold) = await HoldOf("100");
        await s.Lots.BlockAsync(lot);
        // §13 #16: no workflow starts for an impossible decision
        await Refused(s, new { kind = "HoldDecision", targetId = hold, reason = QuarantineScenario.Reason, coaChecked = true,
            lines = new[] { QuarantineScenario.Line(1, "Release", 100m) } }, 409, "QUARANTINE_LOT_HELD");
    }

    [Fact]
    public async Task V10_V11_V12_V13_V14_disposition_and_unblock_rules()
    {
        var (s, _, hold) = await HoldOf("50");
        var applied = await s.DecideAndApproveAsync(s.DecideAsync(hold, [QuarantineScenario.Line(1, "Block", 50m)], "OTHER", coa: false));
        var blocked = Id(await s.BlockedOfDecisionAsync(Id(applied, "decisionId"), 1), "blockedId");
        var before = s.Decisions().Count;

        async Task Disp(object disposition, int status, string code)
            => Assert.Equal(code, QuarantineScenario.Code(await s.DisposeAsync(blocked, disposition, status)));

        await Disp(new { kind = "Destroy", quantity = 51m, destructionMethod = "Yakma", destructionDate = DateTime.UtcNow.ToString("yyyy-MM-dd") }, 400, "QUARANTINE_QUANTITY_INVALID");
        await Disp(new { kind = "ReturnToSupplier", quantity = 10m }, 400, "QUARANTINE_SUPPLIER_REQUIRED");
        await Disp(new { kind = "Destroy", quantity = 10m, destructionDate = DateTime.UtcNow.ToString("yyyy-MM-dd") }, 400, "QUARANTINE_DESTRUCTION_METHOD_REQUIRED");
        await Disp(new { kind = "Destroy", quantity = 10m, destructionMethod = "Yakma", destructionDate = DateTime.UtcNow.AddDays(-31).ToString("yyyy-MM-dd") }, 400,
            "QUARANTINE_DESTRUCTION_METHOD_REQUIRED");
        await Disp(new { kind = "Sample", quantity = 10m }, 409, "QUARANTINE_SAMPLE_LOCATION_NOT_SET");
        Assert.Equal("QUARANTINE_REVIEW_NOT_COMPLETED", QuarantineScenario.Code(await s.UnblockAsync(blocked, 409))); // §13 #15
        Assert.Equal(before, s.Decisions().Count);
    }

    [Fact]
    public async Task Submit_without_an_approval_path_fails_closed_and_moves_nothing()
    {
        // The production host (InventoryApiFactory: test port off) — Q1 has no workflow, so a submit cannot start an approval.
        using var factory = new TraceApiFactory();
        var lots = new LotLedgerScenario(factory);
        var lot = await lots.CreateLotAsync();
        await lots.ReleaseAsync(lot);
        var tx = await lots.PostOkAsync(lots.Receipt("100", lot, toStatus: "QUARANTINE"));
        var token = TestTokens.Mint(lots.Ledger.Tenant, lots.Ledger.LegalEntity, lots.Ledger.User, QuarantineScenario.QuarantinePermissions);
        var client = factory.CreateContractClient();
        var holdId = lots.Persisted("quarantine_holds").Single(h => h["SourceMovementId"].AsString == tx)["_id"].AsString;

        async Task<(int, JsonElement)> Post(string path, object body, string key)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = System.Net.Http.Json.JsonContent.Create(body) };
            request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            using var response = await client.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            return ((int)response.StatusCode, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone());
        }

        var k1 = QuarantineScenario.Key("d");
        var (s1, draft) = await Post("/api/quarantine/decisions", new { kind = "HoldDecision", targetId = holdId, reason = QuarantineScenario.Reason, coaChecked = true,
            lines = new[] { QuarantineScenario.Line(1, "Release", 100m) }, idempotencyKey = k1 }, k1);
        Assert.Equal(201, s1);
        var k2 = QuarantineScenario.Key("s");
        var (s2, refused) = await Post($"/api/quarantine/decisions/{Id(draft, "decisionId")}/submit", new { idempotencyKey = k2 }, k2);
        Assert.Equal(503, s2);
        Assert.Equal("QUARANTINE_WORKFLOW_UNAVAILABLE", QuarantineScenario.Code(refused));
        Assert.Equal(["Submitted", "StartFailed"], lots.Persisted("quarantine_decision_events").OrderBy(e => e["Sequence"].ToInt64()).Select(e => e["Kind"].AsString));
        Assert.Equal("Open", lots.Persisted("quarantine_holds").Single()["Status"].AsString);
        Assert.True(lots.Persisted("quarantine_holds").Single()["ActiveDecisionId"].IsBsonNull);
        Assert.Equal(100m, await lots.Ledger.OnHandAsync(lots.Ledger.LocationA, "QUARANTINE"));

        // and the test-approval endpoint does not exist on this host
        var (s3, missing) = await Post($"/api/quarantine/test-approval/decisions/{Id(draft, "decisionId")}", new { outcome = "Approved" }, QuarantineScenario.Key("t"));
        Assert.Equal(404, s3);
        Assert.Equal(JsonValueKind.Undefined, missing.ValueKind); // no route at all — not even a contract error body
    }

    [Fact]
    public async Task Another_company_is_refused_and_another_tenant_sees_nothing()
    {
        var (s, _, hold) = await HoldOf("10");
        var otherCompany = TestTokens.Mint(s.Tenant, Guid.NewGuid(), s.Ledger.User, QuarantineScenario.QuarantinePermissions);
        var otherTenant = TestTokens.Mint(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), QuarantineScenario.QuarantinePermissions);
        // §13 #21 / V23: another company of the same tenant → 403 CROSS_LE_FORBIDDEN; another tenant → 404
        var cross = await s.ExpectAsync(HttpMethod.Get, $"/api/quarantine/holds/{hold}", null, 403, token: otherCompany);
        Assert.Equal("CROSS_LE_FORBIDDEN", QuarantineScenario.Code(cross));
        Assert.Equal(404, (await s.SendAsync(HttpMethod.Get, $"/api/quarantine/holds/{hold}", token: otherTenant)).Status);
        Assert.Equal(0, (await s.ExpectAsync(HttpMethod.Get, "/api/quarantine/holds", null, 200, token: otherCompany)).GetProperty("total").GetInt32());
    }

    // ── pure rules ──────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1200, 1200, 0, "Release", null, null)]
    [InlineData(1200, 700, 500, "Release", "Reject", null)]
    [InlineData(1200, 700, 400, "Release", "Reject", "QUARANTINE_PARTIAL_SUM_MISMATCH")]
    [InlineData(1200, 1200, 0, "Release", "Release", "QUARANTINE_DECISION_INVALID")]
    [InlineData(1200, 1100, 0, "Block", null, "QUARANTINE_DECISION_INVALID")]
    [InlineData(1200, 1200, 0, "Reject", "Block", "QUARANTINE_PARTIAL_SUM_MISMATCH")]
    public void KQ_A_line_matrix(int hold, int q1, int q2, string d1, string? d2, string? expected)
    {
        var lines = new List<RequestedLine> { new(1, Enum.Parse<LineDecision>(d1), q1) };
        if (d2 is not null) lines.Add(new RequestedLine(2, Enum.Parse<LineDecision>(d2), q2));
        Assert.Equal(expected, QuarantineDecisionRules.Lines(lines, hold)?.Code);
    }

    [Fact]
    public void V9_a_retest_release_needs_a_new_date_after_today_and_not_after_expiry()
    {
        var today = new DateOnly(2026, 10, 8);
        Assert.Null(QuarantineDecisionRules.RetestDate(false, null, today, null));
        Assert.Equal("QUARANTINE_RETEST_DATE_REQUIRED", QuarantineDecisionRules.RetestDate(true, null, today, null)!.Code);
        Assert.Equal("LOT_DATES_INVALID", QuarantineDecisionRules.RetestDate(true, today, today, null)!.Code);
        Assert.Equal("LOT_DATES_INVALID", QuarantineDecisionRules.RetestDate(true, today.AddDays(40), today, today.AddDays(30))!.Code);
        Assert.Null(QuarantineDecisionRules.RetestDate(true, today.AddDays(20), today, today.AddDays(30)));
    }
}
