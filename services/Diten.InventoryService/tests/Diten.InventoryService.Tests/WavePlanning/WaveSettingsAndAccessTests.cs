using Diten.InventoryService.Persistence.WavePlanning;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;
using static Diten.InventoryService.Tests.WavePlanning.WaveScenario;

namespace Diten.InventoryService.Tests.WavePlanning;

/// <summary>
/// MOD-0180 DL-1 over the REAL host — the warehouse wave setting (AC-15 a…e) and tenant / company isolation with the permission keys (AC-3,
/// AC-5 behaviour side).
/// </summary>
public sealed class WaveSettingsAndAccessTests(WaveApiFactory factory) : IClassFixture<WaveApiFactory>
{
    // ── AC-15 (c) ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC15c_without_a_record_the_defaults_are_answered_with_version_0()
    {
        var s = new WaveScenario(factory);

        var settings = await Expect(s.GetSettingsAsync(), 200);

        Assert.False(settings.GetProperty("waveManaged").GetBoolean());
        Assert.False(settings.TryGetProperty("timeZoneId", out _));
        Assert.Equal(90, settings.GetProperty("riskMinutesBeforeCutoff").GetInt32());
        Assert.Equal(70, settings.GetProperty("riskProgressPercent").GetInt32());
        Assert.Equal(30, settings.GetProperty("pendingTaskMinutes").GetInt32());
        Assert.Equal(0, settings.GetProperty("version").GetInt32());
        Assert.Equal(0, settings.GetProperty("deferredOrderCount").GetInt64());
        Assert.Equal(0, settings.GetProperty("releasingOrderCount").GetInt64());
        Assert.Empty(settings.GetProperty("stuckOrders").EnumerateArray());
        Assert.Empty(s.Stored(WaveCollections.Settings)); // a read creates nothing
    }

    // ── AC-15 (a) ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC15a_reading_needs_read_and_writing_needs_settings_configure_and_a_reason()
    {
        var s = new WaveScenario(factory);
        var planner = s.TokenFor(s.User, Read, Create, Update, Cancel); // everything but settings.configure

        Assert.Equal(200, (int)(await s.GetSettingsAsync(token: planner)).StatusCode);
        Assert.Equal(403, (int)(await s.PutSettingsAsync(s.SettingsBody(), "W/\"0\"", token: planner)).StatusCode); // update is NOT enough (Q-6 = B)
        Assert.Equal(403, (int)(await s.GetSettingsAsync(token: s.TokenFor(s.User, Configure))).StatusCode);          // configure does not read
        Assert.Empty(s.Stored(WaveCollections.Settings));

        Assert.Equal("WAVE_REASON_REQUIRED", await ErrorCode(s.PutSettingsAsync(s.SettingsBody(reason: null), "W/\"0\""), 400));
        Assert.Equal("WAVE_REASON_REQUIRED", await ErrorCode(s.PutSettingsAsync(s.SettingsBody(reason: "kisa"), "W/\"0\""), 400));
        Assert.Empty(s.Stored(WaveCollections.Settings));

        var written = await Expect(s.PutSettingsAsync(s.SettingsBody(riskMinutes: 120), "W/\"0\"", token: s.TokenFor(s.User, Configure)), 200);

        Assert.True(written.GetProperty("waveManaged").GetBoolean());
        Assert.Equal(Istanbul, written.GetProperty("timeZoneId").GetString());
        Assert.Equal(120, written.GetProperty("riskMinutesBeforeCutoff").GetInt32());
        Assert.Equal(1, written.GetProperty("version").GetInt32());
        var stored = Assert.Single(s.Stored(WaveCollections.Settings));
        var updated = Assert.Single(s.Events(stored["_id"].AsString));
        Assert.Equal("SettingsUpdated", updated["Kind"].AsString);
        Assert.Equal("Settings", updated["Stream"].AsString);
        Assert.Equal(Reason, updated["Data"]["reason"].AsString); // the reason lives in the event (pack §8.13)
        Assert.Equal("true", updated["Data"]["after.waveManaged"].AsString);
        Assert.Single(s.CentralRows("UpdateWaveWarehouseSettingsCommand"));
    }

    // ── AC-15 (b) ────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(90, 150, 30)]
    [InlineData(90, 0, 30)]
    [InlineData(0, 70, 30)]
    [InlineData(1441, 70, 30)]
    [InlineData(90, 70, 0)]
    [InlineData(90, 70, 1441)]
    public async Task AC15b_a_threshold_out_of_range_is_400_and_the_record_does_not_change(int riskMinutes, int riskPercent, int pendingMinutes)
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var before = await Expect(s.GetSettingsAsync(), 200);

        Assert.Equal("INVALID_REQUEST",
            await ErrorCode(s.PutSettingsAsync(s.SettingsBody(riskMinutes: riskMinutes, riskPercent: riskPercent, pendingMinutes: pendingMinutes), ETag(before)), 400));

        var after = await Expect(s.GetSettingsAsync(), 200);
        Assert.Equal(before.GetRawText(), after.GetRawText());
        Assert.Single(s.Events(s.Stored(WaveCollections.Settings).Single()["_id"].AsString));
    }

    [Fact]
    public async Task The_setting_refuses_a_bad_time_zone_a_managed_warehouse_without_one_and_system_members()
    {
        var s = new WaveScenario(factory);

        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.PutSettingsAsync(s.SettingsBody(timeZoneId: "Mars/Olympus"), "W/\"0\""), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.PutSettingsAsync(s.SettingsBody(timeZoneId: "Turkey Standard Time"), "W/\"0\""), 400)); // not an IANA id
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.PutSettingsAsync(s.SettingsBody(managed: true, timeZoneId: null), "W/\"0\""), 400));
        foreach (var system in new[] { "gateSeq", "pendingReleaseStuck", "deferredOrderCount", "releasingOrderCount", "stuckOrders", "version", "tenantId" })
        {
            var body = s.SettingsBody();
            body[system] = 1;
            Assert.Equal("INVALID_REQUEST", await ErrorCode(s.PutSettingsAsync(body, "W/\"0\""), 400));
        }

        Assert.Empty(s.Stored(WaveCollections.Settings));
        // not managed needs no time zone
        Assert.Equal(200, (int)(await s.PutSettingsAsync(s.SettingsBody(managed: false, timeZoneId: null), "W/\"0\"")).StatusCode);
    }

    // ── AC-15 (d) ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC15d_another_companys_warehouse_setting_is_404()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var foreign = Guid.Parse(s.Inventory.AddForeignCompanyBin().WarehouseId);

        Assert.Equal("UNKNOWN_LOCATION", await ErrorCode(s.GetSettingsAsync(foreign), 404));
        Assert.Equal("UNKNOWN_LOCATION", await ErrorCode(s.PutSettingsAsync(s.SettingsBody(), "W/\"0\"", foreign), 404));
        Assert.Equal("UNKNOWN_LOCATION", await ErrorCode(s.GetSettingsAsync(Guid.NewGuid()), 404));
        Assert.Equal("UNKNOWN_LOCATION", await ErrorCode(s.GetSettingsAsync(s.Inventory.StorageNode.Id), 404)); // not a Warehouse node
        Assert.Equal("UNKNOWN_LOCATION", await ErrorCode(s.GetAsync("/api/warehouse/waves/settings/not-a-uuid"), 404));
        Assert.Single(s.Stored(WaveCollections.Settings));
    }

    // ── AC-15 (e) ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC15e_switching_on_and_changing_thresholds_is_always_accepted_under_the_optimistic_lock()
    {
        var s = new WaveScenario(factory);
        var off = await Expect(s.PutSettingsAsync(s.SettingsBody(managed: false), "W/\"0\""), 200);
        var on = await Expect(s.PutSettingsAsync(s.SettingsBody(managed: true), ETag(off)), 200);       // false → true
        var tuned = await Expect(s.PutSettingsAsync(s.SettingsBody(riskMinutes: 45, riskPercent: 60, pendingMinutes: 15), ETag(on)), 200);
        var offAgain = await Expect(s.PutSettingsAsync(s.SettingsBody(managed: false), ETag(tuned)), 200); // true → false is not refused (Y-1 = A)

        Assert.Equal(4, offAgain.GetProperty("version").GetInt32());
        Assert.False(offAgain.TryGetProperty("pendingRelease", out _)); // DL-1: no pending-order round yet (DL-2)
        Assert.Equal("WAVE_CONFLICT", await ErrorCode(s.PutSettingsAsync(s.SettingsBody(), ETag(on)), 409));         // stale
        Assert.Equal("WAVE_CONFLICT", await ErrorCode(s.PutSettingsAsync(s.SettingsBody(), "W/\"0\""), 409));        // the record exists now
        var record = s.Stored(WaveCollections.Settings).Single();
        var events = s.Events(record["_id"].AsString);
        Assert.Equal([1, 2, 3, 4], events.Select(e => e["Seq"].AsInt32));
        Assert.Equal("90", events[2]["Data"]["before.riskMinutesBeforeCutoff"].AsString);
        Assert.Equal("45", events[2]["Data"]["after.riskMinutesBeforeCutoff"].AsString);
        Assert.Equal(4, s.CentralRows("UpdateWaveWarehouseSettingsCommand").Count);

        // idempotent: the same key replays, another body with it is refused
        var key = NewKey();
        var body = s.SettingsBody(riskPercent: 55);
        var first = await Expect(s.PutSettingsAsync(body, ETag(offAgain), key: key), 200);
        var replay = await Expect(s.PutSettingsAsync(body, ETag(offAgain), key: key), 200);
        Assert.Equal(first.GetProperty("version").GetInt32(), replay.GetProperty("version").GetInt32());
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await ErrorCode(s.PutSettingsAsync(s.SettingsBody(riskPercent: 56), ETag(offAgain), key: key), 400));
        Assert.Equal(5, s.Events(record["_id"].AsString).Count);
    }

    [Fact]
    public async Task Of_two_concurrent_first_setting_writes_exactly_one_creates_the_record()
    {
        var s = new WaveScenario(factory);
        var both = new Barrier(2);
        factory.BeforeWaveTransaction[s.Tenant] = () => Task.Run(() => both.SignalAndWait(TimeSpan.FromSeconds(20)));

        var calls = await Task.WhenAll(s.PutSettingsAsync(s.SettingsBody(riskMinutes: 10), "W/\"0\""), s.PutSettingsAsync(s.SettingsBody(riskMinutes: 20), "W/\"0\""));
        factory.BeforeWaveTransaction.TryRemove(s.Tenant, out _);

        Assert.Equal([200, 409], calls.Select(c => (int)c.StatusCode).Order());
        Assert.Single(s.Stored(WaveCollections.Settings));
        Assert.Single(s.CentralRows("UpdateWaveWarehouseSettingsCommand"));
    }

    // ── AC-3 ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Every DL-1 endpoint of A's wave, called by another TENANT that claims the SAME company id (so only the tenant predicate separates them)
    /// and by ANOTHER company of the same tenant (only the company predicate separates them): 404 each, nothing written, lists empty.
    /// </summary>
    [Fact]
    public async Task AC3_another_tenant_or_another_company_sees_404_on_every_endpoint_and_lists_nothing()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var outbound = s.SeedOutbound();
        var wave = await s.AddedAsync(await s.CreatedAsync(), outbound);
        var id = Id(wave);
        var otherTenant = Guid.NewGuid();
        factory.TrackWaveTenant(otherTenant);
        var otherCompany = Guid.NewGuid();
        factory.LegalEntities.RegisterName(otherCompany, "OTH", "Other company");

        foreach (var token in new[] { TestTokens.Mint(otherTenant, s.LegalEntity, s.User, AllKeys), TestTokens.Mint(s.Tenant, otherCompany, s.User, AllKeys) })
        {
            Assert.Equal("WAVE_NOT_FOUND", await ErrorCode(s.GetAsync($"/api/warehouse/waves/{id}", token), 404));
            Assert.Equal("WAVE_NOT_FOUND", await ErrorCode(s.GetAsync($"/api/warehouse/waves/{id}/orders", token), 404));
            Assert.Equal("WAVE_NOT_FOUND", await ErrorCode(s.GetAsync($"/api/warehouse/waves/{id}/events", token), 404));
            Assert.Equal("WAVE_NOT_FOUND", await ErrorCode(s.GetAsync($"/api/warehouse/waves/{id}/load", token), 404));
            Assert.Equal("WAVE_NOT_FOUND", await ErrorCode(s.GetAsync($"/api/warehouse/waves/{id}/suggestions", token), 404));
            Assert.Equal("WAVE_NOT_FOUND", await ErrorCode(s.UpdateAsync(id, s.Form(), ETag(wave), token: token), 404));
            Assert.Equal("WAVE_NOT_FOUND", await ErrorCode(s.AddAsync(id, [s.SeedOutbound()], ETag(wave), token: token), 404));
            Assert.Equal("WAVE_NOT_FOUND", await ErrorCode(s.RemoveAsync(id, [outbound], ETag(wave), token: token), 404));
            Assert.Equal("WAVE_NOT_FOUND", await ErrorCode(s.CancelAsync(id, ETag(wave), token: token), 404));
            Assert.Equal("UNKNOWN_LOCATION", await ErrorCode(s.GetSettingsAsync(token: token), 404));
            Assert.Equal("UNKNOWN_LOCATION", await ErrorCode(s.PutSettingsAsync(s.SettingsBody(managed: false), "W/\"1\"", token: token), 404));
            Assert.Equal("UNKNOWN_LOCATION", await ErrorCode(s.CreateAsync(token: token), 422)); // A's warehouse does not exist for them
            Assert.Equal(0, (await Expect(s.GetAsync($"/api/warehouse/waves?warehouseId={s.Warehouse:D}", token), 200)).GetProperty("total").GetInt64());
            Assert.Equal(0, (await Expect(s.GetAsync($"/api/warehouse/waves/candidates?warehouseId={s.Warehouse:D}", token), 200)).GetProperty("total").GetInt64());
        }

        var mine = await s.WaveAsync(id);
        Assert.Equal("Draft", mine.GetProperty("status").GetString());
        Assert.Equal(2, mine.GetProperty("version").GetInt32());
        Assert.Equal(1, mine.GetProperty("orderCount").GetInt32());
        Assert.Equal(2, s.Events(id).Count);
        Assert.True((await Expect(s.GetSettingsAsync(), 200)).GetProperty("waveManaged").GetBoolean());
        Assert.Equal(1, (await Expect(s.GetAsync($"/api/warehouse/waves?warehouseId={s.Warehouse:D}"), 200)).GetProperty("total").GetInt64());
        // the list is the warehouse asked for: A's other warehouse holds no wave
        Assert.Equal(0, (await Expect(s.GetAsync($"/api/warehouse/waves?warehouseId={s.Warehouse2:D}"), 200)).GetProperty("total").GetInt64());
    }

    // ── AC-5 (behaviour) ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>The keys are enforced: without the action's key the endpoint is 403 (the attribute, not the handler).</summary>
    [Fact]
    public async Task Each_endpoint_needs_its_own_key()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var wave = await s.CreatedAsync();
        var readOnly = s.TokenFor(s.User, Read);
        var noRead = s.TokenFor(s.User, Create, Update, Configure, Cancel);

        Assert.Equal(403, (int)(await s.CreateAsync(token: readOnly)).StatusCode);
        Assert.Equal(403, (int)(await s.UpdateAsync(Id(wave), s.Form(), ETag(wave), token: readOnly)).StatusCode);
        Assert.Equal(403, (int)(await s.AddAsync(Id(wave), [Guid.NewGuid()], ETag(wave), token: readOnly)).StatusCode);
        Assert.Equal(403, (int)(await s.RemoveAsync(Id(wave), [Guid.NewGuid()], ETag(wave), token: readOnly)).StatusCode);
        Assert.Equal(403, (int)(await s.CancelAsync(Id(wave), ETag(wave), token: readOnly)).StatusCode);
        Assert.Equal(403, (int)(await s.GetAsync($"/api/warehouse/waves/{Id(wave)}/suggestions", readOnly)).StatusCode); // suggestions are update's (pack §14)
        Assert.Equal(403, (int)(await s.CancelAsync(Id(wave), ETag(wave), token: s.TokenFor(s.User, Read, Update))).StatusCode); // cancel is its own Tier-3 key
        foreach (var path in new[]
                 {
                     $"/api/warehouse/waves?warehouseId={s.Warehouse:D}", $"/api/warehouse/waves/{Id(wave)}", $"/api/warehouse/waves/{Id(wave)}/orders",
                     $"/api/warehouse/waves/{Id(wave)}/events", $"/api/warehouse/waves/{Id(wave)}/load", $"/api/warehouse/waves/candidates?warehouseId={s.Warehouse:D}"
                 })
        {
            Assert.Equal(403, (int)(await s.GetAsync(path, noRead)).StatusCode);
            Assert.Equal(200, (int)(await s.GetAsync(path, readOnly)).StatusCode);
        }

        Assert.Equal(401, (int)(await s.GetAsync($"/api/warehouse/waves/{Id(wave)}", token: string.Empty)).StatusCode);
        Assert.Single(s.Stored(WaveCollections.Waves));
        Assert.Single(s.Events(Id(wave)));
    }

    /// <summary>S-3a / S-3b at the store: the production repository of ANOTHER scope does not find A's wave, membership, events or setting.</summary>
    [Fact]
    public async Task AC3_the_store_is_bound_to_tenant_and_company()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var outbound = s.SeedOutbound();
        var waveId = Guid.Parse(Id(await s.AddedAsync(await s.CreatedAsync(), outbound)));
        Assert.NotNull(await s.WithRepositoryAsync(r => r.FindAsync(waveId)));

        foreach (var (tenant, company) in new[] { (Guid.NewGuid(), s.LegalEntity), (s.Tenant, Guid.NewGuid()) })
        {
            Assert.Null(await s.WithRepositoryAsync(r => r.FindAsync(waveId), tenant, company));
            Assert.Null(await s.WithRepositoryAsync(r => r.FindSettingsAsync(s.Warehouse), tenant, company));
            Assert.Empty(await s.WithRepositoryAsync(r => r.ActiveOrdersAsync(waveId), tenant, company));
            Assert.Empty(await s.WithRepositoryAsync(r => r.ActiveMembershipsAsync([outbound]), tenant, company));
            Assert.Empty(await s.WithRepositoryAsync(r => r.ActiveOutboundIdsAsync(s.Warehouse), tenant, company));
            Assert.Equal(0, (await s.WithRepositoryAsync(r => r.EventsAsync(waveId, Diten.InventoryService.Domain.WavePlanning.Enums.WaveEventStream.Wave, 1, 50), tenant, company)).Total);
        }
    }
}
