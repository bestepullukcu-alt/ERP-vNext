using System.Text.Json;
using MongoDB.Bson;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Expiry;

/// <summary>
/// WP-0176-F2 — KF-D, the lot date suggestion and override (pack §8.3.3, §8.3.4 row "Lot date"; CT-F6 / CT-F7 / CT-F10), through the REAL
/// TRACE endpoints (<c>POST /api/trace/lots</c>, <c>PATCH /api/trace/lots/{id}</c>) and FEFO <c>POST /api/expiry/date-overrides</c>.
/// The SKU has shelfLifeDays 365 and retestDays 180: manufacturing 2026-01-01 suggests expiry 2027-01-01 and retest 2026-06-30.
/// </summary>
public sealed class ExpiryLotDateHookTests : IClassFixture<ExpiryApiFactory>
{
    private const string Manufacturing = "2026-01-01";
    private const string SuggestedExpiry = "2027-01-01";
    private const string SuggestedRetest = "2026-06-30";
    private const string OtherExpiry = "2026-12-01";
    private const string OverrideReason = "Tedarikci CoA belgesinde son kullanma daha erken";

    private readonly ExpiryApiFactory _factory;

    public ExpiryLotDateHookTests(ExpiryApiFactory factory) => _factory = factory;

    private sealed class World
    {
        public required ExpiryScenario S { get; init; }
        public required string OverrideToken { get; init; }
    }

    private World Make()
    {
        var s = new ExpiryScenario(_factory);
        _factory.ShelfLife[s.Ledger.Sku] = (365, 180, null);
        // the lot creator (Ledger.User) prepares its own overrides — the gate only accepts the caller's override
        return new World { S = s, OverrideToken = s.Mint(s.Ledger.User, ["expiry.date-overrides.create"]) };
    }

    private static string T(string day) => day + "T00:00:00Z";

    private static Dictionary<string, object?> LotBody(ExpiryScenario s, string lotNumber, string? expiry, string? retest, object? dateOverride = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = "lot-" + Guid.NewGuid().ToString("N"),
            ["itemId"] = s.Ledger.Item.ToString("D"),
            ["lotNumber"] = lotNumber,
            ["skuId"] = s.Ledger.Sku.ToString("D"),
            ["skuLevel"] = Diten.InventoryService.Domain.Rules.WireNames.ToWire(s.Ledger.Level),
            ["manufacturingDate"] = T(Manufacturing)
        };
        if (expiry is not null) body["expiryDate"] = T(expiry);
        if (retest is not null) body["retestDate"] = T(retest);
        if (dateOverride is not null) body["dateOverride"] = dateOverride;
        return body;
    }

    private static async Task<(int Status, JsonElement Body)> CreateLotAsync(ExpiryScenario s, Dictionary<string, object?> body)
    {
        using var response = await s.Lots.Send(HttpMethod.Post, "/api/trace/lots", body,
            r => r.Headers.TryAddWithoutValidation("Idempotency-Key", (string)body["idempotencyKey"]!));
        return ((int)response.StatusCode, await TraceScenario.Read(response));
    }

    private static Dictionary<string, object?> OverrideBody(ExpiryScenario s, string lotNumber, string? expiry, string? retest,
        string reason = OverrideReason, string? key = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = key ?? ExpiryScenario.Key("dov"),
            ["itemId"] = s.Ledger.Item.ToString("D"),
            ["skuId"] = s.Ledger.Sku.ToString("D"),
            ["skuLevel"] = Diten.InventoryService.Domain.Rules.WireNames.ToWire(s.Ledger.Level),
            ["lotNumber"] = lotNumber,
            ["manufacturingDate"] = Manufacturing,
            ["reason"] = reason
        };
        if (expiry is not null) body["expiryDate"] = expiry;
        if (retest is not null) body["retestDate"] = retest;
        return body;
    }

    private static Task<(int Status, JsonElement Body, HttpResponseMessage Raw)> PrepareOverrideAsync(World w, Dictionary<string, object?> body)
        => w.S.SendAsync(HttpMethod.Post, "/api/expiry/date-overrides", w.OverrideToken, body, (string)body["idempotencyKey"]!);

    private static async Task<string> OverrideAsync(World w, string lotNumber, string? expiry, string? retest)
    {
        var (status, body, _) = await PrepareOverrideAsync(w, OverrideBody(w.S, lotNumber, expiry, retest));
        Assert.True(status == 201, body.ToString());
        return body.GetProperty("overrideId").GetString()!;
    }

    private static string Code(JsonElement e) => e.GetProperty("error").GetProperty("code").GetString()!;

    private static BsonDocument StoredOverride(ExpiryScenario s, string id) => Assert.Single(s.Persisted("expiry_date_overrides"), d => d["_id"].AsString == id);

    // ── suggestion ──────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dates_equal_to_the_suggestion_need_no_override_and_the_suggestion_is_stored()
    {
        var w = Make();
        var (status, lot) = await CreateLotAsync(w.S, LotBody(w.S, TraceScenario.LotNo("F2"), SuggestedExpiry, SuggestedRetest));
        Assert.True(status == 201, lot.ToString());
        Assert.StartsWith(SuggestedExpiry, lot.GetProperty("suggestedExpiryDate").GetString());
        Assert.StartsWith(SuggestedRetest, lot.GetProperty("suggestedRetestDate").GetString());
        Assert.False(lot.TryGetProperty("expiryOverride", out var o) && o.ValueKind != JsonValueKind.Null);
        Assert.Empty(w.S.Persisted("expiry_exceptions"));
    }

    [Fact]
    public async Task A_SKU_without_shelf_life_days_has_no_rule()
    {
        var s = new ExpiryScenario(_factory);
        var (status, _) = await CreateLotAsync(s, LotBody(s, TraceScenario.LotNo("F2"), OtherExpiry, null));
        Assert.Equal(201, status);
    }

    [Fact]
    public async Task Another_date_without_an_override_is_422_with_the_suggestion()
    {
        var w = Make();
        var (status, refused) = await CreateLotAsync(w.S, LotBody(w.S, TraceScenario.LotNo("F2"), OtherExpiry, SuggestedRetest));
        Assert.Equal(422, status);
        Assert.Equal("LOT_DATE_OVERRIDE_REQUIRED", Code(refused));
        // TRACE's Error has no details member (1.4.0 leaves it unchanged): the suggestion is named in the message
        Assert.Contains($"expiry {SuggestedExpiry}, retest {SuggestedRetest}", refused.GetProperty("error").GetProperty("message").GetString());
        // ASSUMPTION-0176-07: an empty date while a suggestion exists differs too
        var (s2, r2) = await CreateLotAsync(w.S, LotBody(w.S, TraceScenario.LotNo("F2"), SuggestedExpiry, null));
        Assert.Equal(422, s2);
        Assert.Equal("LOT_DATE_OVERRIDE_REQUIRED", Code(r2));
        Assert.Empty(w.S.Persisted("trace_lots")); // the scenario tenant is fresh
    }

    // ── createDateOverride ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateDateOverride_prepares_a_pending_override_and_replays()
    {
        var w = Make();
        var number = TraceScenario.LotNo("F2");
        var body = OverrideBody(w.S, number, OtherExpiry, SuggestedRetest);
        var (status, created, _) = await PrepareOverrideAsync(w, body);
        Assert.Equal(201, status);
        Assert.Equal(SuggestedExpiry, created.GetProperty("suggestedExpiry").GetString());
        Assert.Equal(OtherExpiry, created.GetProperty("enteredExpiry").GetString());
        Assert.True(created.GetProperty("signatureRequired").GetBoolean()); // default ReasonAndSignature
        Assert.Equal("Pending", created.GetProperty("status").GetString());
        Assert.Single(w.S.Audit("expiry.date-override.prepared"));

        var (replayStatus, replay, _) = await PrepareOverrideAsync(w, body);
        Assert.Equal(200, replayStatus);
        Assert.Equal(created.GetProperty("overrideId").GetString(), replay.GetProperty("overrideId").GetString());
        Assert.Single(w.S.Persisted("expiry_date_overrides"));
    }

    [Fact]
    public async Task CreateDateOverride_refuses_what_needs_no_override()
    {
        var w = Make();
        var (s1, equal, _) = await PrepareOverrideAsync(w, OverrideBody(w.S, TraceScenario.LotNo("F2"), SuggestedExpiry, SuggestedRetest));
        Assert.Equal(422, s1);
        Assert.Equal("EXPIRY_DATE_EQUALS_SUGGESTION", Code(equal));

        var plain = new ExpiryScenario(_factory); // no shelf-life days → no suggestion
        var token = plain.Mint(plain.Ledger.User, ["expiry.date-overrides.create"]);
        var body = OverrideBody(plain, TraceScenario.LotNo("F2"), OtherExpiry, null);
        var (s2, none, _) = await plain.SendAsync(HttpMethod.Post, "/api/expiry/date-overrides", token, body, (string)body["idempotencyKey"]!);
        Assert.Equal(422, s2);
        Assert.Equal("EXPIRY_NO_SUGGESTION", Code(none));

        var (s3, _, _) = await PrepareOverrideAsync(w, OverrideBody(w.S, TraceScenario.LotNo("F2"), OtherExpiry, null, reason: "kisa"));
        Assert.Equal(400, s3);
        Assert.Empty(w.S.Persisted("expiry_date_overrides"));
    }

    // ── CT-F6: ReasonAndSignature (default) is fail-closed until F3 ──────────────────────────────────────

    [Fact]
    public async Task ReasonAndSignature_needs_a_signature_and_fails_closed()
    {
        var w = Make();
        var number = TraceScenario.LotNo("F2");
        var id = await OverrideAsync(w, number, OtherExpiry, SuggestedRetest);

        var (s1, r1) = await CreateLotAsync(w.S, LotBody(w.S, number, OtherExpiry, SuggestedRetest, new { overrideId = id }));
        Assert.Equal(400, s1);
        Assert.Equal("LOT_DATE_OVERRIDE_SIGNATURE_REQUIRED", Code(r1));

        var (s2, r2) = await CreateLotAsync(w.S, LotBody(w.S, number, OtherExpiry, SuggestedRetest, new { overrideId = id, signatureId = "sig-anything" }));
        Assert.Equal(409, s2);
        Assert.Equal("LOT_DATE_OVERRIDE_SIGNATURE_MISMATCH", Code(r2));

        Assert.Equal("Pending", StoredOverride(w.S, id)["Status"].AsString);
        Assert.Empty(w.S.Persisted("expiry_exceptions"));
    }

    // ── mode Reason: the override is consumed with the lot ───────────────────────────────────────────────

    [Fact]
    public async Task Reason_mode_creates_the_lot_consumes_the_override_and_opens_the_exception_in_one_transaction()
    {
        var w = Make();
        Assert.Equal(200, (await w.S.PutPolicyAsync(ExpiryScenario.Policy(date: "Reason"), "\"0\"")).Status);
        var number = TraceScenario.LotNo("F2");
        var id = await OverrideAsync(w, number, OtherExpiry, SuggestedRetest);

        var (status, lot) = await CreateLotAsync(w.S, LotBody(w.S, number, OtherExpiry, SuggestedRetest, new { overrideId = id }));
        Assert.True(status == 201, lot.ToString());
        var lotId = lot.GetProperty("lotId").GetString()!;
        var applied = lot.GetProperty("expiryOverride");
        Assert.Equal(id, applied.GetProperty("overrideId").GetString());
        Assert.Equal(OverrideReason, applied.GetProperty("reason").GetString());
        Assert.StartsWith(SuggestedExpiry, lot.GetProperty("suggestedExpiryDate").GetString());

        var stored = StoredOverride(w.S, id);
        Assert.Equal("Consumed", stored["Status"].AsString);
        Assert.Equal(lotId, stored["ConsumedByLotId"].AsString);
        var c = Assert.Single(w.S.Persisted("expiry_exceptions"));
        Assert.Equal("ExpiryDateOverride", c["Type"].AsString);
        Assert.Equal(w.S.Ledger.User.ToString(), c["OriginUserId"].AsString); // CT-F10: the user who set the dates
        Assert.Equal(OtherExpiry, c["Facts"]["enteredExpiry"].AsString);
        Assert.Single(w.S.Audit("expiry.date-override.consumed"));
        Assert.Contains(w.S.Persisted("trace_lot_history"), h => h["LotId"].AsString == lotId && h["Action"].AsString == "ExpiryOverridden");

        // the same override cannot be used twice
        var (s2, r2) = await CreateLotAsync(w.S, LotBody(w.S, number + "B", OtherExpiry, SuggestedRetest, new { overrideId = id }));
        Assert.Equal(409, s2);
        Assert.Equal("EXPIRY_DATE_OVERRIDE_NOT_USABLE", Code(r2));
    }

    [Fact]
    public async Task An_override_for_other_dates_or_of_another_user_is_refused()
    {
        var w = Make();
        Assert.Equal(200, (await w.S.PutPolicyAsync(ExpiryScenario.Policy(date: "Reason"), "\"0\"")).Status);
        var number = TraceScenario.LotNo("F2");
        var id = await OverrideAsync(w, number, OtherExpiry, SuggestedRetest);

        var (s1, r1) = await CreateLotAsync(w.S, LotBody(w.S, number, "2026-11-01", SuggestedRetest, new { overrideId = id }));
        Assert.Equal(409, s1);
        Assert.Equal("LOT_DATE_OVERRIDE_MISMATCH", Code(r1));

        // another user's override (a second user of the same company prepares it)
        var other = Guid.NewGuid();
        var foreignBody = OverrideBody(w.S, number, OtherExpiry, SuggestedRetest);
        var (sf, foreign, _) = await w.S.SendAsync(HttpMethod.Post, "/api/expiry/date-overrides", w.S.Mint(other, ["expiry.date-overrides.create"]),
            foreignBody, (string)foreignBody["idempotencyKey"]!);
        Assert.Equal(201, sf);
        var (s2, r2) = await CreateLotAsync(w.S, LotBody(w.S, number, OtherExpiry, SuggestedRetest,
            new { overrideId = foreign.GetProperty("overrideId").GetString() }));
        Assert.Equal(409, s2);
        Assert.Equal("EXPIRY_DATE_OVERRIDE_NOT_USABLE", Code(r2));

        // an expired override
        _factory.Clock.Advance(TimeSpan.FromHours(25));
        try
        {
            var (s3, r3) = await CreateLotAsync(w.S, LotBody(w.S, number, OtherExpiry, SuggestedRetest, new { overrideId = id }));
            Assert.Equal(409, s3);
            Assert.Equal("EXPIRY_DATE_OVERRIDE_NOT_USABLE", Code(r3));
        }
        finally
        {
            _factory.Clock.Advance(TimeSpan.FromHours(-25));
        }

        Assert.Empty(w.S.Persisted("expiry_exceptions"));
    }

    // ── CT-F7: Draft updateLot follows the same rule; CT-Q17 unchanged for a non-Draft lot ───────────────

    private static async Task<(int Status, JsonElement Body)> PatchAsync(ExpiryScenario s, string lotId, Dictionary<string, object?> patch)
    {
        using var get = await s.Lots.Send(HttpMethod.Get, $"/api/trace/lots/{lotId}", null);
        var etag = get.Headers.ETag!.ToString();
        using var response = await s.Lots.Send(HttpMethod.Patch, $"/api/trace/lots/{lotId}", patch, r => r.Headers.TryAddWithoutValidation("If-Match", etag));
        return ((int)response.StatusCode, await TraceScenario.Read(response));
    }

    [Fact]
    public async Task A_Draft_lot_date_patch_needs_the_override_like_creation()
    {
        var w = Make();
        Assert.Equal(200, (await w.S.PutPolicyAsync(ExpiryScenario.Policy(date: "Reason"), "\"0\"")).Status);
        var number = TraceScenario.LotNo("F2");
        var (status, lot) = await CreateLotAsync(w.S, LotBody(w.S, number, SuggestedExpiry, SuggestedRetest));
        Assert.Equal(201, status);
        var lotId = lot.GetProperty("lotId").GetString()!;

        var (s1, r1) = await PatchAsync(w.S, lotId, new Dictionary<string, object?> { ["expiryDate"] = T(OtherExpiry), ["reason"] = ExpiryScenario.Reason });
        Assert.Equal(422, s1);
        Assert.Equal("LOT_DATE_OVERRIDE_REQUIRED", Code(r1));

        var id = await OverrideAsync(w, number, OtherExpiry, SuggestedRetest);
        var (s2, patched) = await PatchAsync(w.S, lotId, new Dictionary<string, object?>
        {
            ["expiryDate"] = T(OtherExpiry), ["reason"] = ExpiryScenario.Reason, ["dateOverride"] = new { overrideId = id }
        });
        Assert.True(s2 == 200, patched.ToString());
        Assert.Equal(id, patched.GetProperty("expiryOverride").GetProperty("overrideId").GetString());
        Assert.Equal("Consumed", StoredOverride(w.S, id)["Status"].AsString);
        Assert.Single(w.S.Persisted("expiry_exceptions"));
    }

    [Fact]
    public async Task CT_Q17_a_released_lot_retest_moved_later_is_still_LOT_KEY_IMMUTABLE()
    {
        var w = Make();
        var (status, lot) = await CreateLotAsync(w.S, LotBody(w.S, TraceScenario.LotNo("F2"), SuggestedExpiry, SuggestedRetest));
        Assert.Equal(201, status);
        var lotId = lot.GetProperty("lotId").GetString()!;
        await w.S.Lots.ReleaseAsync(lotId);

        var (s1, r1) = await PatchAsync(w.S, lotId, new Dictionary<string, object?> { ["retestDate"] = T("2026-08-01"), ["reason"] = ExpiryScenario.Reason });
        Assert.Equal(400, s1);
        Assert.Equal("LOT_KEY_IMMUTABLE", Code(r1));
        Assert.Empty(w.S.Persisted("expiry_date_overrides"));
    }
}
