using System.Text.Json;
using Xunit;

namespace Diten.InventoryService.Tests.Expiry;

/// <summary>
/// WP-0176-F1 — the company expiry policy through the real API (pack §4.1, §12, §16 F1; FEFO-BUNDLE getPolicy / updatePolicy /
/// getPolicyHistory). Defaults without a row (KF-A…KF-D + the CT-accepted SLA / threshold), If-Match "0" for the first save, one
/// history row per changed field, reason ≥ 10, ranges, no-change refusal, idempotent replay, company isolation.
/// </summary>
public sealed class ExpiryPolicyApiTests : IClassFixture<ExpiryApiFactory>
{
    private readonly ExpiryApiFactory _factory;

    public ExpiryPolicyApiTests(ExpiryApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Without_a_row_the_company_reads_the_owner_defaults_at_version_0()
    {
        var s = new ExpiryScenario(_factory);
        var (status, policy, raw) = await s.SendAsync(HttpMethod.Get, "/api/expiry/policy", s.AdminToken);

        Assert.Equal(200, status);
        Assert.True(policy.GetProperty("isDefault").GetBoolean());
        Assert.Equal(90, policy.GetProperty("approachingDays").GetInt32()); // KF-A
        Assert.Equal(30, policy.GetProperty("criticalDays").GetInt32()); // KF-A
        Assert.Equal("RequireReason", policy.GetProperty("fefoOutboundMode").GetString()); // KF-B
        Assert.Equal("AutoQuarantine", policy.GetProperty("belowMinRemainingMode").GetString()); // KF-C
        Assert.Equal("ReasonAndSignature", policy.GetProperty("dateOverrideMode").GetString()); // KF-D
        var sla = policy.GetProperty("slaHours");
        Assert.Equal([24, 72, 48, 120], new[] { "FEFO_BYPASS", "BELOW_MIN_REMAINING", "EXPIRY_DATE_OVERRIDE", "EXPIRED_ON_HAND" }.Select(t => sla.GetProperty(t).GetInt32()));
        Assert.Equal(14, policy.GetProperty("expiredOnHandThresholdDays").GetInt32());
        Assert.Equal(0, policy.GetProperty("version").GetInt32());
        Assert.Equal("W/\"0\"", raw.Headers.ETag!.ToString());
        Assert.Equal(s.Ledger.LegalEntity.ToString("D"), policy.GetProperty("legalEntityId").GetString());
        Assert.Empty(s.Persisted("expiry_policies"));
    }

    [Fact]
    public async Task The_first_save_takes_If_Match_0_and_writes_one_history_row_per_changed_field_and_one_audit_row()
    {
        var s = new ExpiryScenario(_factory);
        var (status, saved, raw) = await s.PutPolicyAsync(ExpiryScenario.Policy(critical: 21, fefo: "Block", fefoSla: 12), "\"0\"");

        Assert.True(status == 200, saved.ToString());
        Assert.False(saved.GetProperty("isDefault").GetBoolean());
        Assert.Equal(1, saved.GetProperty("version").GetInt32());
        Assert.Equal("W/\"1\"", raw.Headers.ETag!.ToString());
        Assert.Equal(s.Admin.ToString("D"), saved.GetProperty("updatedBy").GetProperty("userId").GetString());

        var history = await s.ExpectAsync(HttpMethod.Get, "/api/expiry/policy/history", s.AdminToken, null, 200);
        var rows = history.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(3, history.GetProperty("total").GetInt32());
        Assert.Equal(["criticalDays", "fefoOutboundMode", "slaHours.FEFO_BYPASS"], rows.Select(r => r.GetProperty("field").GetString()).OrderBy(x => x, StringComparer.Ordinal));
        var critical = rows.Single(r => r.GetProperty("field").GetString() == "criticalDays");
        Assert.Equal("30", critical.GetProperty("oldValue").GetString());
        Assert.Equal("21", critical.GetProperty("newValue").GetString());
        Assert.Equal(ExpiryScenario.Reason, critical.GetProperty("reason").GetString());
        Assert.All(rows, r => Assert.Equal(1, r.GetProperty("policyVersion").GetInt32()));

        var audit = Assert.Single(s.Audit("expiry.policy.updated"));
        Assert.Equal(s.Admin.ToString(), audit["UserId"].AsString);
        Assert.Equal("criticalDays,fefoOutboundMode,slaHours.FEFO_BYPASS", audit["Detail"]["fields"].AsString);

        // a second save replaces version 1 → 2, one more history row
        var (status2, saved2, _) = await s.PutPolicyAsync(ExpiryScenario.Policy(critical: 20, fefo: "Block", fefoSla: 12), ExpiryScenario.ETag(saved));
        Assert.True(status2 == 200, saved2.ToString());
        Assert.Equal(2, saved2.GetProperty("version").GetInt32());
        Assert.Equal(4, (await s.ExpectAsync(HttpMethod.Get, "/api/expiry/policy/history", s.AdminToken, null, 200)).GetProperty("total").GetInt32());
        Assert.Single(s.Persisted("expiry_policies"));
    }

    [Fact]
    public async Task A_stale_or_missing_If_Match_is_refused_and_nothing_is_written()
    {
        var s = new ExpiryScenario(_factory);
        var (status, saved, _) = await s.PutPolicyAsync(ExpiryScenario.Policy(critical: 21), "\"0\"");
        Assert.Equal(200, status);

        // K3 sabotage (b): skip the If-Match comparison → this answers 200 and writes version 2
        var (stale, refused, _) = await s.PutPolicyAsync(ExpiryScenario.Policy(critical: 22), "\"0\"");
        Assert.Equal(409, stale);
        Assert.Equal("EXPIRY_CONFLICT", ExpiryScenario.Code(refused));

        var (missing, refused2, _) = await s.SendAsync(HttpMethod.Put, "/api/expiry/policy", s.AdminToken, ExpiryScenario.Policy(critical: 22),
            ExpiryScenario.Key("pol"));
        Assert.Equal(400, missing);
        Assert.Equal("INVALID_REQUEST", ExpiryScenario.Code(refused2));

        var current = await s.ExpectAsync(HttpMethod.Get, "/api/expiry/policy", s.AdminToken, null, 200);
        Assert.Equal(1, current.GetProperty("version").GetInt32());
        Assert.Equal(21, current.GetProperty("criticalDays").GetInt32());
        Assert.Single(s.Audit("expiry.policy.updated"));
        _ = saved;
    }

    [Theory]
    [InlineData(90, 90, 24, 14, "EXPIRY_POLICY_INVALID")] // critical must be < approaching
    [InlineData(0, 0, 24, 14, "EXPIRY_POLICY_INVALID")]
    [InlineData(731, 30, 24, 14, "EXPIRY_POLICY_INVALID")]
    [InlineData(90, 30, 0, 14, "EXPIRY_POLICY_INVALID")]
    [InlineData(90, 30, 2161, 14, "EXPIRY_POLICY_INVALID")]
    [InlineData(90, 30, 24, 366, "EXPIRY_POLICY_INVALID")]
    [InlineData(90, 30, 24, -1, "EXPIRY_POLICY_INVALID")]
    public async Task Pack_12_ranges_are_enforced(int approaching, int critical, int sla, int threshold, string code)
    {
        var s = new ExpiryScenario(_factory);
        var (status, refused, _) = await s.PutPolicyAsync(ExpiryScenario.Policy(approaching, critical, fefoSla: sla, threshold: threshold), "\"0\"");
        Assert.Equal(400, status);
        Assert.Equal(code, ExpiryScenario.Code(refused));
        Assert.Empty(s.Persisted("expiry_policies"));
    }

    [Fact]
    public async Task Reason_shape_and_no_change_refusals()
    {
        var s = new ExpiryScenario(_factory);
        var (short1, r1, _) = await s.PutPolicyAsync(ExpiryScenario.Policy(critical: 21, reason: "kisa"), "\"0\"");
        Assert.Equal(400, short1);
        Assert.Equal("REASON_REQUIRED", ExpiryScenario.Code(r1));

        var (none, r2, _) = await s.PutPolicyAsync(ExpiryScenario.Policy(), "\"0\""); // the defaults again → nothing changed
        Assert.Equal(409, none);
        Assert.Equal("EXPIRY_NO_CHANGES", ExpiryScenario.Code(r2));

        var bad = ExpiryScenario.Policy(critical: 21);
        bad["fefoOutboundMode"] = "requireReason"; // exact enum names only
        var (s3, r3, _) = await s.PutPolicyAsync(bad, "\"0\"");
        Assert.Equal(400, s3);
        Assert.Equal("INVALID_REQUEST", ExpiryScenario.Code(r3));

        var scoped = ExpiryScenario.Policy(critical: 21);
        scoped["legalEntityId"] = Guid.NewGuid().ToString("D"); // the scope never comes from a body
        var (s4, r4, _) = await s.PutPolicyAsync(scoped, "\"0\"");
        Assert.Equal(400, s4);
        Assert.Equal("INVALID_REQUEST", ExpiryScenario.Code(r4));

        var missingSla = ExpiryScenario.Policy(critical: 21);
        ((Dictionary<string, int>)missingSla["slaHours"]!).Remove("EXPIRED_ON_HAND");
        var (s5, r5, _) = await s.PutPolicyAsync(missingSla, "\"0\"");
        Assert.Equal(400, s5);
        Assert.Equal("EXPIRY_POLICY_INVALID", ExpiryScenario.Code(r5));
        Assert.Empty(s.Persisted("expiry_policies"));
    }

    [Fact]
    public async Task The_same_key_and_body_answers_the_current_policy_and_another_body_is_refused()
    {
        var s = new ExpiryScenario(_factory);
        var key = ExpiryScenario.Key("pol");
        var (s1, first, _) = await s.PutPolicyAsync(ExpiryScenario.Policy(critical: 21, key: key), "\"0\"");
        Assert.Equal(200, s1);
        var (s2, replay, _) = await s.PutPolicyAsync(ExpiryScenario.Policy(critical: 21, key: key), "\"0\""); // stale If-Match: the replay wins
        Assert.Equal(200, s2);
        Assert.Equal(first.GetProperty("version").GetInt32(), replay.GetProperty("version").GetInt32());
        var (s3, reused, _) = await s.PutPolicyAsync(ExpiryScenario.Policy(critical: 22, key: key), ExpiryScenario.ETag(first));
        Assert.Equal(400, s3);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", ExpiryScenario.Code(reused));
        Assert.Single(s.Audit("expiry.policy.updated"));
        Assert.Single(s.Persisted("expiry_policy_history"));
    }

    [Fact]
    public async Task The_policy_is_the_company_s_and_only_the_policy_keys_open_it()
    {
        var s = new ExpiryScenario(_factory);
        Assert.Equal(200, (await s.PutPolicyAsync(ExpiryScenario.Policy(critical: 21), "\"0\"")).Status);

        // the operator holds no policy key
        Assert.Equal(403, (await s.SendAsync(HttpMethod.Get, "/api/expiry/policy", s.OperatorToken)).Status);
        Assert.Equal(403, (await s.PutPolicyAsync(ExpiryScenario.Policy(critical: 22), "W/\"1\"", s.QaToken)).Status);

        // another company of the same tenant reads ITS defaults (company scope, KF-G)
        var otherCompany = Guid.NewGuid();
        _factory.LegalEntities.Register(s.Tenant, otherCompany);
        var other = await s.ExpectAsync(HttpMethod.Get, "/api/expiry/policy", s.Mint(s.Admin, ExpiryScenario.AdminKeys, otherCompany), null, 200);
        Assert.True(other.GetProperty("isDefault").GetBoolean());
        Assert.Equal(30, other.GetProperty("criticalDays").GetInt32());
        Assert.Equal(0, (await s.ExpectAsync(HttpMethod.Get, "/api/expiry/policy/history", s.Mint(s.Admin, ExpiryScenario.AdminKeys, otherCompany), null, 200))
            .GetProperty("total").GetInt32());

        // no token → 401
        var anonymous = await _factory.CreateClient().GetAsync("/api/expiry/policy");
        Assert.Equal(401, (int)anonymous.StatusCode);
    }

    [Fact]
    public async Task History_paging_is_validated()
    {
        var s = new ExpiryScenario(_factory);
        var (status, refused, _) = await s.SendAsync(HttpMethod.Get, "/api/expiry/policy/history?pageSize=201", s.AdminToken);
        Assert.Equal(400, status);
        Assert.Equal("INVALID_REQUEST", ExpiryScenario.Code(refused));
        Assert.Equal(JsonValueKind.Array, (await s.ExpectAsync(HttpMethod.Get, "/api/expiry/policy/history?page=2&pageSize=5", s.AdminToken, null, 200))
            .GetProperty("items").ValueKind);
    }
}
