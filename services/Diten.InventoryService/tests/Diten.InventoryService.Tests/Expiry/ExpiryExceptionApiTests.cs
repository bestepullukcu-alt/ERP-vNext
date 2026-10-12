using System.Globalization;
using System.Text.Json;
using Xunit;

namespace Diten.InventoryService.Tests.Expiry;

/// <summary>
/// WP-0176-F1 — the FEFO exception case through the real API (pack §8.4 state machine, §12, §13 #8, §16 F1; CT-F10 SoD; KF-E signature
/// by policy behind the test double). The production fail-closed gate is <see cref="ExpiryFailClosedSignatureTests"/>.
/// </summary>
public sealed class ExpiryExceptionApiTests : IClassFixture<ExpiryApiFactory>
{
    private readonly ExpiryApiFactory _factory;

    public ExpiryExceptionApiTests(ExpiryApiFactory factory) => _factory = factory;

    private static string Id(JsonElement c) => c.GetProperty("exceptionId").GetString()!;

    private static string[] Actions(JsonElement c) => c.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("action").GetString()!).ToArray();

    [Fact]
    public async Task A_manual_case_opens_with_number_SLA_origin_user_event_and_audit()
    {
        var s = new ExpiryScenario(_factory);
        var lot = await s.LotAsync();
        var before = DateTimeOffset.UtcNow.AddSeconds(-2);
        var c = await s.OpenAsync(lot);

        Assert.Matches(@"^FX-\d{4}-\d{4,}$", c.GetProperty("number").GetString()!);
        Assert.Equal("FEFO_BYPASS", c.GetProperty("type").GetString());
        Assert.Equal("Manual", c.GetProperty("origin").GetString());
        Assert.Equal("Open", c.GetProperty("status").GetString());
        Assert.Equal(lot, c.GetProperty("lot").GetProperty("lotId").GetString());
        Assert.Equal("2027-01-31", c.GetProperty("lot").GetProperty("expiryDate").GetString());
        Assert.Equal(s.Ledger.Warehouse, c.GetProperty("warehouse").GetProperty("id").GetString());
        Assert.False(string.IsNullOrEmpty(c.GetProperty("warehouse").GetProperty("code").GetString()));
        Assert.Equal(s.Ledger.Item.ToString("D"), c.GetProperty("item").GetProperty("itemId").GetString());
        Assert.Equal(s.Operator.ToString("D"), c.GetProperty("openedBy").GetProperty("userId").GetString());
        var opened = DateTimeOffset.Parse(c.GetProperty("openedAtUtc").GetString()!, CultureInfo.InvariantCulture);
        var due = DateTimeOffset.Parse(c.GetProperty("dueAtUtc").GetString()!, CultureInfo.InvariantCulture);
        Assert.True(opened >= before.AddSeconds(-1));
        Assert.Equal(TimeSpan.FromHours(24), due - opened); // FEFO_BYPASS default SLA (ASSUMPTION-0176-03, CT §19.7)
        Assert.False(c.GetProperty("late").GetBoolean());
        Assert.Equal(["Opened"], Actions(c));

        var stored = Assert.Single(s.Persisted("expiry_exceptions"));
        Assert.Equal(s.Operator.ToString(), stored["OriginUserId"].AsString); // CT-F10
        Assert.Equal(s.Ledger.LegalEntity.ToString(), stored["LegalEntityId"].AsString);
        var audit = Assert.Single(s.Audit("expiry.exception.opened"));
        Assert.Equal(c.GetProperty("number").GetString(), audit["SubjectNumber"].AsString);
    }

    [Fact]
    public async Task The_SLA_comes_from_the_company_policy_at_opening()
    {
        var s = new ExpiryScenario(_factory);
        Assert.Equal(200, (await s.PutPolicyAsync(ExpiryScenario.Policy(onHandSla: 6), "\"0\"")).Status);
        var c = await s.OpenAsync(await s.LotAsync(), "EXPIRED_ON_HAND");
        var opened = DateTimeOffset.Parse(c.GetProperty("openedAtUtc").GetString()!, CultureInfo.InvariantCulture);
        Assert.Equal(TimeSpan.FromHours(6), DateTimeOffset.Parse(c.GetProperty("dueAtUtc").GetString()!, CultureInfo.InvariantCulture) - opened);
    }

    [Fact]
    public async Task One_active_case_per_type_lot_and_warehouse_replay_and_reuse()
    {
        var s = new ExpiryScenario(_factory);
        var lot = await s.LotAsync();
        var key = ExpiryScenario.Key("open");
        var body = ExpiryScenario.Open(lot, s.Ledger.Warehouse, key: key);
        var first = await s.ExpectAsync(HttpMethod.Post, "/api/expiry/exceptions", s.OperatorToken, body, 201, key);

        var replay = await s.ExpectAsync(HttpMethod.Post, "/api/expiry/exceptions", s.OperatorToken, body, 200, key);
        Assert.Equal(Id(first), Id(replay));

        var other = ExpiryScenario.Open(lot, s.Ledger.Warehouse, key: key, description: "Baska bir aciklama - farkli govde");
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", ExpiryScenario.Code(await s.ExpectAsync(HttpMethod.Post, "/api/expiry/exceptions", s.OperatorToken, other, 400, key)));

        var twin = ExpiryScenario.Open(lot, s.Ledger.Warehouse);
        var refused = await s.ExpectAsync(HttpMethod.Post, "/api/expiry/exceptions", s.OperatorToken, twin, 409, (string)twin["idempotencyKey"]!);
        Assert.Equal("EXPIRY_EXCEPTION_ALREADY_OPEN", ExpiryScenario.Code(refused));
        Assert.Equal(Id(first), refused.GetProperty("error").GetProperty("details").GetProperty("exceptionId").GetString());

        // another type, or no warehouse, is another key
        await s.OpenAsync(lot, "EXPIRED_ON_HAND");
        var noWarehouse = ExpiryScenario.Open(lot, null);
        await s.ExpectAsync(HttpMethod.Post, "/api/expiry/exceptions", s.OperatorToken, noWarehouse, 201, (string)noWarehouse["idempotencyKey"]!);
        Assert.Equal(3, s.Persisted("expiry_exceptions").Count);
    }

    [Fact]
    public async Task Open_refusals_unknown_lot_not_a_warehouse_short_description_bad_type()
    {
        var s = new ExpiryScenario(_factory);
        var lot = await s.LotAsync();

        var unknown = ExpiryScenario.Open(Guid.NewGuid().ToString("D"), s.Ledger.Warehouse);
        Assert.Equal("UNKNOWN_LOT", ExpiryScenario.Code(await s.ExpectAsync(HttpMethod.Post, "/api/expiry/exceptions", s.OperatorToken, unknown, 404, (string)unknown["idempotencyKey"]!)));

        var bin = ExpiryScenario.Open(lot, s.Ledger.LocationA); // a bin, not a warehouse
        Assert.Equal("UNKNOWN_LOCATION", ExpiryScenario.Code(await s.ExpectAsync(HttpMethod.Post, "/api/expiry/exceptions", s.OperatorToken, bin, 404, (string)bin["idempotencyKey"]!)));

        var shortText = ExpiryScenario.Open(lot, s.Ledger.Warehouse, description: "kisa");
        Assert.Equal("REASON_REQUIRED", ExpiryScenario.Code(await s.ExpectAsync(HttpMethod.Post, "/api/expiry/exceptions", s.OperatorToken, shortText, 400, (string)shortText["idempotencyKey"]!)));

        var badType = ExpiryScenario.Open(lot, s.Ledger.Warehouse, type: "FefoBypass");
        Assert.Equal("INVALID_REQUEST", ExpiryScenario.Code(await s.ExpectAsync(HttpMethod.Post, "/api/expiry/exceptions", s.OperatorToken, badType, 400, (string)badType["idempotencyKey"]!)));

        var zero = ExpiryScenario.Open(lot, s.Ledger.Warehouse, quantity: 0m);
        Assert.Equal("INVALID_REQUEST", ExpiryScenario.Code(await s.ExpectAsync(HttpMethod.Post, "/api/expiry/exceptions", s.OperatorToken, zero, 400, (string)zero["idempotencyKey"]!)));

        // QA holds no create key
        var body = ExpiryScenario.Open(lot, s.Ledger.Warehouse);
        Assert.Equal(403, (await s.SendAsync(HttpMethod.Post, "/api/expiry/exceptions", s.QaToken, body, (string)body["idempotencyKey"]!)).Status);
        Assert.Empty(s.Persisted("expiry_exceptions"));
    }

    [Fact]
    public async Task Open_take_prepare_resolve_close_is_the_pack_8_4_path_and_frees_the_active_key()
    {
        var s = new ExpiryScenario(_factory);
        var lot = await s.LotAsync();
        var c = await s.OpenAsync(lot);
        var id = Id(c);

        var (st, taken, _) = await s.TakeAsync(id, ExpiryScenario.ETag(c));
        Assert.True(st == 200, taken.ToString());
        Assert.Equal("InReview", taken.GetProperty("status").GetString());
        Assert.Equal(s.Qa.ToString("D"), taken.GetProperty("assignee").GetProperty("userId").GetString());

        var (sp, prepared, _) = await s.PrepareAsync(id, "AcceptedWithReason", ExpiryScenario.ETag(taken));
        Assert.True(sp == 200, prepared.ToString());
        Assert.True(prepared.GetProperty("signatureRequired").GetBoolean());
        Assert.Equal("expiry.exception.accept", prepared.GetProperty("actionCode").GetString());
        Assert.Equal("APPROVE", prepared.GetProperty("meaningCode").GetString()); // CT-F8
        Assert.Equal("expiry", prepared.GetProperty("subject").GetProperty("module").GetString());
        Assert.Equal("expiry.exception", prepared.GetProperty("subject").GetProperty("subjectType").GetString());
        Assert.Equal(id, prepared.GetProperty("subject").GetProperty("subjectId").GetString());

        var signature = _factory.Signature.Issue(s.Qa, id, "expiry.exception.accept");
        var (sr, resolved, _) = await s.ResolveAsync(id, "AcceptedWithReason", ExpiryScenario.ETag(prepared), signature);
        Assert.True(sr == 200, resolved.ToString());
        Assert.Equal("Accepted", resolved.GetProperty("status").GetString());
        Assert.Equal("AcceptedWithReason", resolved.GetProperty("resolution").GetString());
        Assert.Equal(signature, resolved.GetProperty("resolutionSignatureId").GetString());
        Assert.Equal(ExpiryScenario.Reason, resolved.GetProperty("resolutionReason").GetString());
        Assert.False(resolved.TryGetProperty("pendingResolution", out _));

        var (sc, closed, _) = await s.CloseAsync(id, ExpiryScenario.ETag(resolved));
        Assert.True(sc == 200, closed.ToString());
        Assert.Equal("Closed", closed.GetProperty("status").GetString());
        Assert.Equal(["Opened", "Taken", "Resolved", "Closed"], Actions(closed));
        Assert.Equal([1, 2, 3, 4], closed.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("seq").GetInt32()));

        // 0176 keeps the signature id only, and the active key is free again
        var stored = Assert.Single(s.Persisted("expiry_exceptions"));
        Assert.Equal(signature, stored["ResolutionSignatureId"].AsString);
        Assert.True(!stored.Contains("DedupKey") || stored["DedupKey"].IsBsonNull);
        await s.OpenAsync(lot);
        foreach (var action in new[] { "expiry.exception.taken", "expiry.exception.resolution-prepared", "expiry.exception.resolved", "expiry.exception.closed" })
        {
            Assert.Single(s.Audit(action));
        }
    }

    [Fact]
    public async Task A_case_can_be_decided_from_Open_and_a_cancelled_case_is_terminal()
    {
        var s = new ExpiryScenario(_factory);
        var c = await s.OpenAsync(await s.LotAsync());
        var id = Id(c);
        var cancelled = await s.ResolveSignedAsync(id, "Cancelled", ExpiryScenario.ETag(c)); // decision straight from Open (mockup)
        Assert.Equal("Cancelled", cancelled.GetProperty("status").GetString());
        Assert.Equal(["Opened", "Cancelled"], Actions(cancelled));
        Assert.Single(s.Audit("expiry.exception.cancelled"));

        // K3 sabotage (c): allow Cancelled → Closed → this close answers 200
        var (close, refused, _) = await s.CloseAsync(id, ExpiryScenario.ETag(cancelled));
        Assert.Equal(409, close);
        Assert.Equal("EXPIRY_INVALID_TRANSITION", ExpiryScenario.Code(refused));
        Assert.Equal(409, (await s.TakeAsync(id, ExpiryScenario.ETag(cancelled))).Status);
        Assert.Equal(409, (await s.PrepareAsync(id, "Fixed", ExpiryScenario.ETag(cancelled))).Status);
        var comment = new Dictionary<string, object?> { ["idempotencyKey"] = ExpiryScenario.Key("c"), ["text"] = "kapandiktan sonra" };
        Assert.Equal(409, (await s.CommandAsync(id, "comments", s.QaToken, comment, null)).Status);
        Assert.Equal("Cancelled", (await s.GetAsync(id)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Close_only_after_accept_or_fix_and_take_only_from_Open()
    {
        var s = new ExpiryScenario(_factory);
        var c = await s.OpenAsync(await s.LotAsync());
        var id = Id(c);
        var (s1, r1, _) = await s.CloseAsync(id, ExpiryScenario.ETag(c));
        Assert.Equal(409, s1);
        Assert.Equal("EXPIRY_INVALID_TRANSITION", ExpiryScenario.Code(r1));

        var fixedCase = await s.ResolveSignedAsync(id, "Fixed", ExpiryScenario.ETag(c));
        Assert.Equal("Fixed", fixedCase.GetProperty("status").GetString());
        Assert.Equal(409, (await s.TakeAsync(id, ExpiryScenario.ETag(fixedCase))).Status);
        Assert.Equal(409, (await s.PrepareAsync(id, "Cancelled", ExpiryScenario.ETag(fixedCase))).Status);
        Assert.Equal("Closed", (await s.CloseAsync(id, ExpiryScenario.ETag(fixedCase))).Body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task The_opener_cannot_resolve_the_case_CT_F10()
    {
        var s = new ExpiryScenario(_factory);
        var opener = s.Mint(s.Operator, ExpiryScenario.OperatorKeys.Concat(ExpiryScenario.QaKeys));
        var c = await s.OpenAsync(await s.LotAsync(), token: opener);
        var id = Id(c);

        // K3 sabotage (f): drop the SoD comparison → this prepare answers 200
        var (sp, refused, _) = await s.PrepareAsync(id, "AcceptedWithReason", ExpiryScenario.ETag(c), opener);
        Assert.Equal(409, sp);
        Assert.Equal("EXPIRY_SOD_VIOLATION", ExpiryScenario.Code(refused));

        // the opener may still take and comment; another user (QA) prepares; the opener's resolve is refused for SoD first
        var (st, taken, _) = await s.TakeAsync(id, ExpiryScenario.ETag(c), opener);
        Assert.Equal(200, st);
        var (sq, prepared, _) = await s.PrepareAsync(id, "AcceptedWithReason", ExpiryScenario.ETag(taken));
        Assert.Equal(200, sq);
        var signature = _factory.Signature.Issue(s.Operator, id, "expiry.exception.accept");
        var (sr, refused2, _) = await s.ResolveAsync(id, "AcceptedWithReason", ExpiryScenario.ETag(prepared), signature, opener);
        Assert.Equal(409, sr);
        Assert.Equal("EXPIRY_SOD_VIOLATION", ExpiryScenario.Code(refused2));

        // QA resolves its own prepared decision
        var qaSignature = _factory.Signature.Issue(s.Qa, id, "expiry.exception.accept");
        Assert.Equal(200, (await s.ResolveAsync(id, "AcceptedWithReason", ExpiryScenario.ETag(prepared), qaSignature)).Status);
    }

    [Fact]
    public async Task Resolve_needs_the_caller_s_own_unexpired_prepared_decision()
    {
        var s = new ExpiryScenario(_factory);
        var c = await s.OpenAsync(await s.LotAsync());
        var id = Id(c);
        var signature = _factory.Signature.Issue(s.Qa, id, "expiry.exception.accept");

        var (s1, r1, _) = await s.ResolveAsync(id, "AcceptedWithReason", ExpiryScenario.ETag(c), signature);
        Assert.Equal(409, s1);
        Assert.Equal("EXPIRY_PENDING_RESOLUTION_MISMATCH", ExpiryScenario.Code(r1));

        var (_, prepared, _) = await s.PrepareAsync(id, "AcceptedWithReason", ExpiryScenario.ETag(c));
        var (s2, r2, _) = await s.ResolveAsync(id, "Fixed", ExpiryScenario.ETag(prepared), _factory.Signature.Issue(s.Qa, id, "expiry.exception.fix"));
        Assert.Equal("EXPIRY_PENDING_RESOLUTION_MISMATCH", ExpiryScenario.Code(r2));
        var (s3, r3, _) = await s.ResolveAsync(id, "AcceptedWithReason", ExpiryScenario.ETag(prepared), signature, reason: "Baska bir gerekce metni");
        Assert.Equal("EXPIRY_PENDING_RESOLUTION_MISMATCH", ExpiryScenario.Code(r3));

        // another QA user did not prepare it
        var qa2 = Guid.NewGuid();
        var (s4, r4, _) = await s.ResolveAsync(id, "AcceptedWithReason", ExpiryScenario.ETag(prepared), _factory.Signature.Issue(qa2, id, "expiry.exception.accept"),
            s.Mint(qa2, ExpiryScenario.QaKeys));
        Assert.Equal("EXPIRY_PENDING_RESOLUTION_MISMATCH", ExpiryScenario.Code(r4));

        // 15 minutes later the prepared decision is gone
        _factory.Clock.Advance(TimeSpan.FromMinutes(16));
        try
        {
            var (s5, r5, _) = await s.ResolveAsync(id, "AcceptedWithReason", ExpiryScenario.ETag(prepared), signature);
            Assert.Equal(409, s5);
            Assert.Equal("EXPIRY_PENDING_RESOLUTION_MISMATCH", ExpiryScenario.Code(r5));
        }
        finally
        {
            _factory.Clock.Advance(TimeSpan.FromMinutes(-16));
        }

        Assert.Equal([409, 409, 409], new[] { s2, s3, s4 });
        Assert.Equal("Open", (await s.GetAsync(id)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task The_signature_gate_required_missing_mismatch_and_reuse()
    {
        var s = new ExpiryScenario(_factory);
        var c1 = await s.OpenAsync(await s.LotAsync());
        var id1 = Id(c1);
        var (_, prepared, _) = await s.PrepareAsync(id1, "Fixed", ExpiryScenario.ETag(c1));

        var (s1, r1, _) = await s.ResolveAsync(id1, "Fixed", ExpiryScenario.ETag(prepared), null);
        Assert.Equal(400, s1);
        Assert.Equal("EXPIRY_SIGNATURE_REQUIRED", ExpiryScenario.Code(r1));

        var (s2, r2, _) = await s.ResolveAsync(id1, "Fixed", ExpiryScenario.ETag(prepared), "sig-forged-1");
        Assert.Equal(409, s2);
        Assert.Equal("EXPIRY_SIGNATURE_MISMATCH", ExpiryScenario.Code(r2));

        // a signature of the accept action does not resolve "Fixed"
        var (s3, r3, _) = await s.ResolveAsync(id1, "Fixed", ExpiryScenario.ETag(prepared), _factory.Signature.Issue(s.Qa, id1, "expiry.exception.accept"));
        Assert.Equal("EXPIRY_SIGNATURE_MISMATCH", ExpiryScenario.Code(r3));

        var good = _factory.Signature.Issue(s.Qa, id1, "expiry.exception.fix");
        Assert.Equal(200, (await s.ResolveAsync(id1, "Fixed", ExpiryScenario.ETag(prepared), good)).Status);
        var verified = _factory.Signature.Verifications.Last(v => v.SignatureId == good);
        Assert.Equal(s.Qa, verified.SignerUserId);
        Assert.Equal("APPROVE", verified.MeaningCode);
        Assert.Equal(ExpiryScenario.Reason, verified.Reason);

        Assert.Equal(409, s3);
        Assert.Equal("Fixed", (await s.GetAsync(id1)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Policy_not_requiring_a_signature_resolves_without_one()
    {
        using var factory = new ExpiryApiFactory();
        factory.Signature.Required = false;
        var s = new ExpiryScenario(factory);
        var c = await s.OpenAsync(await s.LotAsync());
        var (_, prepared, _) = await s.PrepareAsync(Id(c), "AcceptedWithReason", ExpiryScenario.ETag(c));
        Assert.False(prepared.GetProperty("signatureRequired").GetBoolean());
        var (status, resolved, _) = await s.ResolveAsync(Id(c), "AcceptedWithReason", ExpiryScenario.ETag(prepared), null);
        Assert.True(status == 200, resolved.ToString());
        Assert.False(resolved.TryGetProperty("resolutionSignatureId", out _));
    }

    [Fact]
    public async Task Stale_If_Match_is_refused_and_replays_answer_the_current_case()
    {
        var s = new ExpiryScenario(_factory);
        var c = await s.OpenAsync(await s.LotAsync());
        var id = Id(c);
        var key = ExpiryScenario.Key("take");
        var (s1, taken, _) = await s.TakeAsync(id, ExpiryScenario.ETag(c), key: key);
        Assert.Equal(200, s1);
        var (s2, replay, _) = await s.TakeAsync(id, ExpiryScenario.ETag(c), key: key); // same key: the current case, stale If-Match or not
        Assert.Equal(200, s2);
        Assert.Equal(taken.GetProperty("version").GetInt32(), replay.GetProperty("version").GetInt32());

        // K3 sabotage (b): skip If-Match → this prepare answers 200
        var (s3, stale, _) = await s.PrepareAsync(id, "Fixed", ExpiryScenario.ETag(c));
        Assert.Equal(409, s3);
        Assert.Equal("EXPIRY_CONFLICT", ExpiryScenario.Code(stale));

        var (s4, missing, _) = await s.CommandAsync(id, "take", s.QaToken, new Dictionary<string, object?> { ["idempotencyKey"] = ExpiryScenario.Key("t") }, null);
        Assert.Equal(400, s4);
        Assert.Equal("INVALID_REQUEST", ExpiryScenario.Code(missing));

        Assert.Equal(["Opened", "Taken"], Actions(await s.GetAsync(id)));
    }

    [Fact]
    public async Task Comments_are_appended_with_their_position_and_replay()
    {
        var s = new ExpiryScenario(_factory);
        var c = await s.OpenAsync(await s.LotAsync());
        var id = Id(c);
        var key = ExpiryScenario.Key("c");
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = key, ["text"] = "Depoda fiziksel kontrol yapildi" };
        var (s1, e1, _) = await s.CommandAsync(id, "comments", s.OperatorToken, body, null);
        Assert.Equal(201, s1);
        Assert.Equal(2, e1.GetProperty("seq").GetInt32());
        Assert.Equal("Commented", e1.GetProperty("action").GetString());
        var (s2, e2, _) = await s.CommandAsync(id, "comments", s.OperatorToken, body, null);
        Assert.Equal(200, s2);
        Assert.Equal(2, e2.GetProperty("seq").GetInt32());
        var other = new Dictionary<string, object?> { ["idempotencyKey"] = key, ["text"] = "farkli" };
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", ExpiryScenario.Code((await s.CommandAsync(id, "comments", s.OperatorToken, other, null)).Body));
        var empty = new Dictionary<string, object?> { ["idempotencyKey"] = ExpiryScenario.Key("c"), ["text"] = "  " };
        Assert.Equal(400, (await s.CommandAsync(id, "comments", s.OperatorToken, empty, null)).Status);

        var current = await s.GetAsync(id);
        Assert.Equal(c.GetProperty("version").GetInt32(), current.GetProperty("version").GetInt32()); // a comment does not move the version
        Assert.Equal(["Opened", "Commented"], Actions(current));
        Assert.Single(s.Audit("expiry.exception.commented"));
    }

    [Fact]
    public async Task Another_company_is_refused_and_another_tenant_sees_nothing_pack_13_8()
    {
        var s = new ExpiryScenario(_factory);
        var c = await s.OpenAsync(await s.LotAsync());
        var id = Id(c);
        var otherCompany = s.Mint(s.Qa, ExpiryScenario.QaKeys.Concat(ExpiryScenario.OperatorKeys), Guid.NewGuid());
        var otherTenant = s.Mint(Guid.NewGuid(), ExpiryScenario.QaKeys, Guid.NewGuid(), Guid.NewGuid());

        // K3 sabotage (a): drop the LegalEntityId filter of the repository → the other company reads 200 and lists 1
        var (s1, cross, _) = await s.SendAsync(HttpMethod.Get, $"/api/expiry/exceptions/{id}", otherCompany);
        Assert.Equal(403, s1);
        Assert.Equal("CROSS_LE_FORBIDDEN", ExpiryScenario.Code(cross));
        var (s2, crossTake, _) = await s.TakeAsync(id, ExpiryScenario.ETag(c), otherCompany);
        Assert.Equal(403, s2);
        Assert.Equal("CROSS_LE_FORBIDDEN", ExpiryScenario.Code(crossTake));
        Assert.Equal(0, (await s.ExpectAsync(HttpMethod.Get, "/api/expiry/exceptions", otherCompany, null, 200)).GetProperty("total").GetInt32());

        Assert.Equal(404, (await s.SendAsync(HttpMethod.Get, $"/api/expiry/exceptions/{id}", otherTenant)).Status);
        Assert.Equal(404, (await s.TakeAsync(id, ExpiryScenario.ETag(c), otherTenant)).Status);
        Assert.Equal("Open", (await s.GetAsync(id)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task The_inbox_filters_status_type_late_assigned_and_search()
    {
        var s = new ExpiryScenario(_factory);
        var lot = await s.LotAsync();
        var a = await s.OpenAsync(lot);
        var b = await s.OpenAsync(lot, "EXPIRED_ON_HAND");
        var (_, taken, _) = await s.TakeAsync(Id(b), ExpiryScenario.ETag(b));

        async Task<string[]> Ids(string query)
            => (await s.ExpectAsync(HttpMethod.Get, "/api/expiry/exceptions" + query, s.QaToken, null, 200)).GetProperty("items").EnumerateArray().Select(Id).ToArray();

        Assert.Equal([Id(a)], await Ids("?status=Open"));
        Assert.Equal([Id(b)], await Ids("?type=EXPIRED_ON_HAND"));
        Assert.Equal([Id(b)], await Ids("?assignedToMe=true"));
        Assert.Equal(2, (await Ids("?status=Open&status=InReview")).Length);
        Assert.Equal([Id(a)], await Ids("?q=" + a.GetProperty("number").GetString()));
        Assert.Empty(await Ids("?late=true"));

        // the FEFO_BYPASS SLA (24 h) passes: a is late, b (120 h) is not
        _factory.Clock.Advance(TimeSpan.FromHours(25));
        try
        {
            Assert.Equal([Id(a)], await Ids("?late=true"));
            Assert.Equal([Id(b)], await Ids("?late=false"));
            Assert.True((await s.GetAsync(Id(a))).GetProperty("late").GetBoolean());
        }
        finally
        {
            _factory.Clock.Advance(TimeSpan.FromHours(-25));
        }

        Assert.Equal(400, (await s.SendAsync(HttpMethod.Get, "/api/expiry/exceptions?status=open", s.QaToken)).Status);
        Assert.Equal(400, (await s.SendAsync(HttpMethod.Get, "/api/expiry/exceptions?sort=number", s.QaToken)).Status);
        _ = taken;
    }
}

/// <summary>
/// WP-0176-F1 §4 — the PRODUCTION e-signature gate until F3 is fail-closed: KF-E's default policy requires a signature on all three
/// resolutions and nothing can verify one yet, so no exception is resolved in production — without a signature 400, with one 409.
/// K3 sabotage (g): make the production default "allow" → this test is red.
/// </summary>
public sealed class ExpiryFailClosedSignatureTests : IClassFixture<ProductionSignatureExpiryApiFactory>
{
    private readonly ProductionSignatureExpiryApiFactory _factory;

    public ExpiryFailClosedSignatureTests(ProductionSignatureExpiryApiFactory factory) => _factory = factory;

    [Fact]
    public async Task No_resolution_passes_unsigned_or_unverified_in_production()
    {
        var s = new ExpiryScenario(_factory);
        var c = await s.OpenAsync(await s.LotAsync());
        var id = c.GetProperty("exceptionId").GetString()!;

        foreach (var resolution in new[] { "AcceptedWithReason", "Fixed", "Cancelled" })
        {
            var current = await s.GetAsync(id);
            var (sp, prepared, _) = await s.PrepareAsync(id, resolution, ExpiryScenario.ETag(current));
            Assert.True(sp == 200, prepared.ToString());
            Assert.True(prepared.GetProperty("signatureRequired").GetBoolean());

            var (s1, r1, _) = await s.ResolveAsync(id, resolution, ExpiryScenario.ETag(prepared), null);
            Assert.Equal(400, s1);
            Assert.Equal("EXPIRY_SIGNATURE_REQUIRED", ExpiryScenario.Code(r1));

            var (s2, r2, _) = await s.ResolveAsync(id, resolution, ExpiryScenario.ETag(prepared), "any-signature-id-" + resolution);
            Assert.Equal(409, s2);
            Assert.Equal("EXPIRY_SIGNATURE_MISMATCH", ExpiryScenario.Code(r2));
        }

        var final = await s.GetAsync(id);
        Assert.Equal("Open", final.GetProperty("status").GetString());
        Assert.False(final.TryGetProperty("resolution", out _));
        Assert.Empty(s.Audit("expiry.exception.resolved"));
        Assert.Empty(s.Audit("expiry.exception.cancelled"));
    }
}
