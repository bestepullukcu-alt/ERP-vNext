using System.Text.Json;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-0177-R1 item 1 — the company recall settings through the real API (pack §4.14, KR-G, CT-R16): defaults without a row, If-Match + reason,
/// one history row per changed field, idempotent replay, range / no-change refusals, company isolation.
/// </summary>
public sealed class RecallSettingsApiTests : IClassFixture<RecallApiFactory>
{
    private readonly RecallApiFactory _factory;

    public RecallSettingsApiTests(RecallApiFactory factory) => _factory = factory;

    private static Dictionary<string, object?> Body(int target = 4, int frequency = 180, decimal t1 = 100, decimal t2 = 95, decimal t3 = 90, int depth = 20,
        int nodes = 5000, string? reason = "Ic denetim bulgusu IA-2026-07", string? key = null, string? signatureId = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = key ?? RecallScenario.Key("set"),
            ["drillTargetHours"] = target,
            ["drillFrequencyWarningDays"] = frequency,
            ["effectivenessTargetPercent"] = new Dictionary<string, decimal> { ["1"] = t1, ["2"] = t2, ["3"] = t3 },
            ["traceMaxDepth"] = depth,
            ["traceMaxNodes"] = nodes,
            ["reason"] = reason
        };
        if (signatureId is not null) body["signatureId"] = signatureId;
        return body;
    }

    private static Task<(int Status, JsonElement Body, HttpResponseMessage Raw)> PutAsync(RecallScenario s, Dictionary<string, object?> body, string? ifMatch, string? token = null)
        => s.SendAsync(HttpMethod.Put, "/api/recall/settings", token ?? s.AdminToken, body, (string)body["idempotencyKey"]!, ifMatch);

    [Fact]
    public async Task Defaults_are_the_owner_decision_KR_G_without_a_stored_row()
    {
        var s = new RecallScenario(_factory);
        var (status, settings, raw) = await s.SendAsync(HttpMethod.Get, "/api/recall/settings", s.AdminToken);

        Assert.Equal(200, status);
        Assert.True(settings.GetProperty("isDefault").GetBoolean());
        Assert.Equal(4, settings.GetProperty("drillTargetHours").GetInt32());
        Assert.Equal(180, settings.GetProperty("drillFrequencyWarningDays").GetInt32());
        var targets = settings.GetProperty("effectivenessTargetPercent");
        Assert.Equal(100m, targets.GetProperty("1").GetDecimal());
        Assert.Equal(95m, targets.GetProperty("2").GetDecimal());
        Assert.Equal(90m, targets.GetProperty("3").GetDecimal());
        Assert.Equal(20, settings.GetProperty("traceMaxDepth").GetInt32());
        Assert.Equal(5000, settings.GetProperty("traceMaxNodes").GetInt32());
        Assert.Equal(0, settings.GetProperty("version").GetInt32());
        Assert.Equal("W/\"0\"", raw.Headers.ETag!.ToString());
        Assert.Equal(s.Ledger.LegalEntity.ToString("D"), settings.GetProperty("legalEntityId").GetString());
        Assert.Empty(s.Persisted("recall_settings"));
    }

    [Fact]
    public async Task A_save_needs_If_Match_and_a_reason_and_writes_one_history_row_per_changed_field()
    {
        var s = new RecallScenario(_factory);

        Assert.Equal(400, (await PutAsync(s, Body(frequency: 120), null)).Status); // no If-Match
        var (shortReason, refusal, _) = await PutAsync(s, Body(frequency: 120, reason: "kisa"), "W/\"0\"");
        Assert.Equal(400, shortReason);
        Assert.Equal("REASON_REQUIRED", RecallScenario.Code(refusal));
        // K3 sabotage (b): skip If-Match → a stale version saves
        var (stale, staleBody, _) = await PutAsync(s, Body(frequency: 120), "W/\"7\"");
        Assert.Equal(409, stale);
        Assert.Equal("RECALL_CONFLICT", RecallScenario.Code(staleBody));

        var key = RecallScenario.Key("set");
        var (s1, saved, raw) = await PutAsync(s, Body(frequency: 120, t3: 85.5m, key: key), "W/\"0\"");
        Assert.Equal(200, s1);
        Assert.False(saved.GetProperty("isDefault").GetBoolean());
        Assert.Equal(1, saved.GetProperty("version").GetInt32());
        Assert.Equal("W/\"1\"", raw.Headers.ETag!.ToString());
        Assert.Equal(120, saved.GetProperty("drillFrequencyWarningDays").GetInt32());
        Assert.Equal(s.Admin.ToString("D"), saved.GetProperty("updatedBy").GetProperty("userId").GetString());

        var history = await s.ExpectAsync(HttpMethod.Get, "/api/recall/settings/history", s.AdminToken, null, 200);
        var fields = history.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("field").GetString()).OrderBy(x => x).ToList();
        Assert.Equal(["drillFrequencyWarningDays", "effectivenessTargetPercent.3"], fields);
        var frequencyRow = history.GetProperty("items").EnumerateArray().Single(e => e.GetProperty("field").GetString() == "drillFrequencyWarningDays");
        Assert.Equal("180", frequencyRow.GetProperty("oldValue").GetString());
        Assert.Equal("120", frequencyRow.GetProperty("newValue").GetString());
        Assert.Equal("Ic denetim bulgusu IA-2026-07", frequencyRow.GetProperty("reason").GetString());
        Assert.Single(s.Audit("recall.settings.updated"));

        // replay: same key + same body → the current settings; same key + another body → 400
        var (replay, replayed, _) = await PutAsync(s, Body(frequency: 120, t3: 85.5m, key: key), "W/\"0\"");
        Assert.Equal(200, replay);
        Assert.Equal(1, replayed.GetProperty("version").GetInt32());
        var (reused, reusedBody, _) = await PutAsync(s, Body(frequency: 90, key: key), "W/\"1\"");
        Assert.Equal(400, reused);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", RecallScenario.Code(reusedBody));
        Assert.Single(s.Audit("recall.settings.updated"));

        var (same, sameBody, _) = await PutAsync(s, Body(frequency: 120, t3: 85.5m), "W/\"1\"");
        Assert.Equal(409, same);
        Assert.Equal("RECALL_NO_CHANGES", RecallScenario.Code(sameBody));
    }

    [Theory]
    [InlineData(0, 180, 100, 20, 5000)]
    [InlineData(73, 180, 100, 20, 5000)]
    [InlineData(4, 29, 100, 20, 5000)]
    [InlineData(4, 731, 100, 20, 5000)]
    [InlineData(4, 180, 100.5, 20, 5000)]
    [InlineData(4, 180, 100, 1, 5000)]
    [InlineData(4, 180, 100, 20, 99)]
    public async Task Out_of_range_settings_are_refused(int target, int frequency, double t1, int depth, int nodes)
    {
        var s = new RecallScenario(_factory);
        var (status, body, _) = await PutAsync(s, Body(target, frequency, (decimal)t1, depth: depth, nodes: nodes), "W/\"0\"");
        Assert.Equal(400, status);
        Assert.Equal("RECALL_SETTINGS_INVALID", RecallScenario.Code(body));
    }

    [Fact]
    public async Task Settings_are_per_company_and_need_their_keys()
    {
        var s = new RecallScenario(_factory);
        Assert.Equal(200, (await PutAsync(s, Body(target: 6), "W/\"0\"")).Status);

        var otherCompany = s.Mint(s.Admin, RecallScenario.AdminKeys, Guid.NewGuid());
        var other = await s.ExpectAsync(HttpMethod.Get, "/api/recall/settings", otherCompany, null, 200);
        Assert.True(other.GetProperty("isDefault").GetBoolean());
        Assert.Equal(4, other.GetProperty("drillTargetHours").GetInt32());

        Assert.Equal(403, (await PutAsync(s, Body(target: 8), "W/\"1\"", s.Qa1Token)).Status); // QA has no settings.manage
        Assert.Equal(403, (await s.SendAsync(HttpMethod.Get, "/api/recall/settings", s.ManagementToken)).Status);
    }

    [Fact]
    public async Task CT_R16_a_settings_save_needs_no_signature_by_default_but_a_sent_one_must_verify()
    {
        var s = new RecallScenario(_factory);
        var (status, body, _) = await PutAsync(s, Body(target: 5, signatureId: "sig-not-issued"), "W/\"0\"");
        Assert.Equal(409, status);
        Assert.Equal("RECALL_SIGNATURE_MISMATCH", RecallScenario.Code(body));
        Assert.Empty(s.Persisted("recall_settings"));
    }
}
