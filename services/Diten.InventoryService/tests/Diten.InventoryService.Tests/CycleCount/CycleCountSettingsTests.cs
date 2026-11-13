using Diten.InventoryService.Api.CycleCounting;
using Diten.InventoryService.Infrastructure.Authorization;
using Diten.InventoryService.Persistence.CycleCounting;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;
using static Diten.InventoryService.Tests.CycleCounting.CycleCountScenario;

namespace Diten.InventoryService.Tests.CycleCounting;

/// <summary>
/// MOD-0181 DL-3 — the company count settings (pack §4.4, §12; AC-49, AC-51's one PUT) and the DL-3 endpoints' isolation and keys (AC-5, AC-7).
/// </summary>
public sealed class CycleCountSettingsTests(CycleCountApiFactory factory) : IClassFixture<CycleCountApiFactory>
{
    [Fact]
    public async Task AC49_the_settings_default_to_0_0_2_2000_validate_their_ranges_and_take_a_compare_and_set()
    {
        var s = new CycleCountScenario(factory);
        var defaults = await Expect(s.SettingsAsync(), 200);
        Assert.Equal("0.000", defaults.GetProperty("toleranceAbsolute").GetString());
        Assert.Equal("0.000", defaults.GetProperty("tolerancePercent").GetString());
        Assert.Equal(2, defaults.GetProperty("maxRecounts").GetInt32());
        Assert.Equal(2000, defaults.GetProperty("maxLinesPerCount").GetInt32());
        Assert.True(defaults.GetProperty("isDefault").GetBoolean());
        Assert.Equal(0, defaults.GetProperty("version").GetInt32());

        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.PutSettingsAsync("W/\"0\"", toleranceAbsolute: "-1"), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.PutSettingsAsync("W/\"0\"", tolerancePercent: "150"), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.PutSettingsAsync("W/\"0\"", maxRecounts: -1), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.PutSettingsAsync("W/\"0\"", maxLinesPerCount: 0), 400));
        Assert.Empty(s.Stored(CycleCountCollections.Settings));

        var key = NewKey();
        var saved = await Expect(s.PutSettingsAsync("W/\"0\"", "1.5", "12.5", 3, 1500, key), 200);
        Assert.False(saved.GetProperty("isDefault").GetBoolean());
        Assert.Equal(1, saved.GetProperty("version").GetInt32());
        Assert.Equal("1.500", saved.GetProperty("toleranceAbsolute").GetString());
        Assert.Equal(s.Approver.ToString("D"), saved.GetProperty("updatedBy").GetString());
        Assert.Equal(1, (await Expect(s.PutSettingsAsync("W/\"0\"", "1.5", "12.5", 3, 1500, key), 200)).GetProperty("version").GetInt32()); // replay
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await ErrorCode(s.PutSettingsAsync("W/\"0\"", "2", "12.5", 3, 1500, key), 400));
        Assert.Equal("COUNT_CONFLICT", await ErrorCode(s.PutSettingsAsync("W/\"0\"", "2", "12.5", 3, 1500), 409)); // stale If-Match
        Assert.Equal("1.500", (await Expect(s.SettingsAsync(), 200)).GetProperty("toleranceAbsolute").GetString());
        Assert.Single(s.Stored(CycleCountCollections.Settings));
        Assert.Single(s.CentralRows("UpdateCycleCountSettingsCommand"));

        // the before / after values live in the local trail (pack §21)
        var settingsId = s.Stored(CycleCountCollections.Settings).Single()["_id"].AsString;
        var row = Assert.Single(s.Events(settingsId));
        Assert.Equal("0.000", row["Data"]["previousToleranceAbsolute"].AsString);
        Assert.Equal("1.500", row["Data"]["toleranceAbsolute"].AsString);
    }

    [Fact]
    public async Task AC49_the_settings_keys_are_enforced_and_another_company_sees_only_its_own_defaults()
    {
        var s = new CycleCountScenario(factory);
        await Expect(s.PutSettingsAsync("W/\"0\"", "3", "10"), 200);
        Assert.Equal(403, (int)(await s.SettingsAsync(s.TokenFor(s.User, Read, VariancesRead))).StatusCode);
        Assert.Equal(403, (int)(await s.PutSettingsAsync("W/\"1\"", "4", "10", token: s.TokenFor(s.User, SettingsRead))).StatusCode);

        var otherCompany = Guid.NewGuid();
        factory.LegalEntities.RegisterName(otherCompany, "OTH", "Other company");
        var other = TestTokens.Mint(s.Tenant, otherCompany, s.Approver, ApproverKeys);
        Assert.True((await Expect(s.SettingsAsync(other), 200)).GetProperty("isDefault").GetBoolean());
        Assert.Equal("3.000", (await Expect(s.SettingsAsync(), 200)).GetProperty("toleranceAbsolute").GetString());
    }

    [Fact]
    public void AC49_the_manifest_API_permissions_are_exactly_the_screenless_settings_pair()
    {
        // DL-3 CT ORTA-1 (owner «Tamam», 2026-11-10): measured on the MANIFEST itself, equality — never Contains, never the test's own constant
        // (WarehouseRegistrationTests.cs:98-100 pattern); a page / action key that stays an API permission turns it red
        var declared = (new CycleCountingManifestProvider().GetManifest().ApiPermissions ?? []).Select(p => p.PermissionKey).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(["cycle-count.settings.configure", "cycle-count.settings.read"], declared);
    }

    /// <summary>AC-5 / AC-7 for the DL-3 endpoints: another tenant or company → 404 on every line / count endpoint; each needs its own key.</summary>
    [Fact]
    public async Task AC5_AC7_the_DL3_endpoints_are_tenant_and_company_bound_and_need_their_own_keys()
    {
        var s = new CycleCountScenario(factory);
        var (countId, line) = await s.SubmittedLineAsync(10, 8);
        var otherTenant = Guid.NewGuid();
        factory.TrackCountTenant(otherTenant);
        var otherCompany = Guid.NewGuid();
        factory.LegalEntities.RegisterName(otherCompany, "OTH", "Other company");
        foreach (var token in new[] { TestTokens.Mint(otherTenant, s.LegalEntity, s.Approver, ApproverKeys), TestTokens.Mint(s.Tenant, otherCompany, s.Approver, ApproverKeys) })
        {
            Assert.Equal("COUNT_LINE_NOT_FOUND", await ErrorCode(s.GetAsync($"/api/warehouse/count-variances/{LineId(line)}", token), 404));
            Assert.Equal("COUNT_LINE_NOT_FOUND", await ErrorCode(s.ApproveAsync(line, token: token), 404));
            Assert.Equal("COUNT_LINE_NOT_FOUND", await ErrorCode(s.RejectAsync(line, token: token), 404));
            Assert.Equal("COUNT_LINE_NOT_FOUND", await ErrorCode(s.RetryAsync(line, token: token), 404));
            Assert.Equal("COUNT_LINE_NOT_FOUND", await ErrorCode(s.ResolveAsync(line, "Reject", token: token), 404));
            Assert.Equal("COUNT_NOT_FOUND", await ErrorCode(s.RecountAsync(countId, "W/\"5\"", [LineId(line)], token: token), 404));
            Assert.Equal(0, (await s.VariancesAsync("?source=count&pageSize=200", token)).GetProperty("total").GetInt64());
        }

        Assert.Equal("AwaitingApproval", (await s.LineAsync(countId, LineId(line))).GetProperty("status").GetString());
        Assert.Equal(0, s.CountMovements());

        // each DL-3 action needs its own key: the counter's keys and variances.read are not enough
        var reader = s.TokenFor(s.Approver, Read, VariancesRead);
        Assert.Equal(403, (int)(await s.ApproveAsync(line, token: reader)).StatusCode);
        Assert.Equal(403, (int)(await s.RejectAsync(line, token: reader)).StatusCode);
        Assert.Equal(403, (int)(await s.RetryAsync(line, token: reader)).StatusCode);
        Assert.Equal(403, (int)(await s.ResolveAsync(line, "Reject", token: reader)).StatusCode);
        Assert.Equal(403, (int)(await s.RecountAsync(countId, "W/\"5\"", [LineId(line)], token: reader)).StatusCode);
        Assert.Equal(403, (int)(await s.GetAsync("/api/warehouse/count-variances", s.TokenFor(s.Approver, Read, VariancesApprove))).StatusCode);
        Assert.Equal("AwaitingApproval", (await s.LineAsync(countId, LineId(line))).GetProperty("status").GetString());
    }
}
