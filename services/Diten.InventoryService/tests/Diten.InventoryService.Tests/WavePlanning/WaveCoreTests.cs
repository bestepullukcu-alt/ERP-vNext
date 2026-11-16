using System.Text.Json;
using Diten.InventoryService.Domain.WavePlanning.Repositories;
using Diten.InventoryService.Persistence.WavePlanning;
using MongoDB.Bson;
using Xunit;
using static Diten.InventoryService.Tests.WavePlanning.WaveScenario;

namespace Diten.InventoryService.Tests.WavePlanning;

/// <summary>
/// MOD-0180 DL-1 over the REAL host — the wave core: create and its timeline (AC-2), scope members in a body (AC-4), idempotent writes (AC-6),
/// the optimistic lock (AC-7), no delete / cancel (AC-8), the central record (AC-9), the record code (AC-10) and the cutoff time zone (AC-14).
/// </summary>
public sealed class WaveCoreTests(WaveApiFactory factory) : IClassFixture<WaveApiFactory>
{
    // ── AC-2 ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC2_create_with_the_eight_fields_answers_201_Draft_with_number_cutoff_and_one_Created_event()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var responsible = Guid.NewGuid();
        factory.ActiveUsers[(s.Tenant, responsible)] = true;
        var form = s.Form("Customer", "2026-11-20T14:30", shipToFilter: "  Ankara ", maxOrders: 50, maxLines: 400, responsible: responsible, note: " ilk dalga ");
        // 2026-11-20 lies in the past for a later run of this suite only if the clock says so: pin MOD-0180's clock to a day before it
        factory.WaveClock.Advance(new DateTimeOffset(2026, 11, 19, 8, 0, 0, TimeSpan.Zero) - factory.WaveClock.GetUtcNow());

        var wave = await Expect(s.CreateAsync(form), 201);

        Assert.Equal("Draft", wave.GetProperty("status").GetString());
        Assert.Matches("^GMG-WAV-\\d{6}$", wave.GetProperty("number").GetString());
        Assert.Equal("Customer", wave.GetProperty("waveType").GetString());
        Assert.Equal("2026-11-20T14:30", wave.GetProperty("cutoffLocal").GetString());
        Assert.Equal("2026-11-20T11:30:00.000Z", wave.GetProperty("cutoffAtUtc").GetString()); // Europe/Istanbul is UTC+3, from the SERVER (pack §8.4)
        Assert.Equal(Istanbul, wave.GetProperty("timeZoneId").GetString());
        Assert.Equal(s.Inventory.WarehouseNode.Code, wave.GetProperty("warehouseCode").GetString());
        Assert.Equal("Ankara", wave.GetProperty("shipToFilter").GetString());
        Assert.Equal(50, wave.GetProperty("maxOrders").GetInt32());
        Assert.Equal(400, wave.GetProperty("maxLines").GetInt32());
        Assert.Equal(responsible.ToString("D"), wave.GetProperty("responsibleUserId").GetString());
        Assert.Equal("ilk dalga", wave.GetProperty("note").GetString());
        Assert.Equal(0, wave.GetProperty("orderCount").GetInt32());
        Assert.Equal(1, wave.GetProperty("version").GetInt32());
        Assert.False(wave.TryGetProperty("cutoffAmbiguousResolved", out _));
        Assert.False(wave.TryGetProperty("limitWarnings", out _));

        var stored = s.StoredWave(Id(wave));
        Assert.Equal(s.Tenant.ToString("D"), stored["TenantId"].AsString);
        Assert.Equal(s.LegalEntity.ToString("D"), stored["LegalEntityId"].AsString);
        Assert.False(stored["IsDeleted"].AsBoolean);
        var created = Assert.Single(s.Events(Id(wave)));
        Assert.Equal("Created", created["Kind"].AsString);
        Assert.Equal("Wave", created["Stream"].AsString);
        Assert.Equal(1, created["Seq"].AsInt32);
        Assert.Equal(wave.GetProperty("number").GetString(), created["Data"]["number"].AsString);
        Assert.Equal("createWave", created["RequestOp"].AsString);

        // a second wave of the tenant takes the next number (tenant-wide unique)
        var second = await s.CreatedAsync(s.Form(cutoffLocal: "2026-11-21T09:00"));
        Assert.NotEqual(wave.GetProperty("number").GetString(), second.GetProperty("number").GetString());
    }

    /// <summary>S-2: the Created event is written in the SAME transaction — when it cannot be written there is no wave.</summary>
    [Fact]
    public async Task AC2_the_Created_event_and_the_wave_are_one_transaction()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        factory.FailNextEventInsert[s.Tenant] = true;

        using var failed = await s.CreateAsync();

        Assert.Equal(500, (int)failed.StatusCode);
        Assert.Empty(s.Stored(WaveCollections.Waves));
        Assert.Empty(s.Stored(WaveCollections.Events).Where(e => e["Stream"].AsString == "Wave"));
        Assert.Empty(s.CentralRows("CreateWaveCommand"));
    }

    [Fact]
    public async Task Create_refuses_a_malformed_form_an_unknown_warehouse_and_an_invalid_responsible_user()
    {
        var s = await WaveScenario.ManagedAsync(factory);

        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.CreateAsync(s.Form(waveType: "Express")), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.CreateAsync(s.Form(cutoffLocal: "2026-11-20 14:30")), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.CreateAsync(s.Form(maxOrders: 1001)), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.CreateAsync(s.Form(maxLines: 0)), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.CreateAsync(s.Form(shipToFilter: new string('x', 101))), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.CreateAsync(s.Form(note: new string('n', 501))), 400));
        Assert.Equal("UNKNOWN_LOCATION", await ErrorCode(s.CreateAsync(s.Form(warehouse: Guid.NewGuid())), 422));
        Assert.Equal("UNKNOWN_LOCATION", await ErrorCode(s.CreateAsync(s.Form(warehouse: s.Inventory.StorageNode.Id)), 422)); // not a Warehouse node
        Assert.Equal("WAVE_RESPONSIBLE_INVALID", await ErrorCode(s.CreateAsync(s.Form(responsible: Guid.NewGuid())), 422));
        factory.AuthDown[s.Tenant] = true;
        Assert.Equal("WAVE_RESPONSIBLE_DIRECTORY_UNAVAILABLE", await ErrorCode(s.CreateAsync(s.Form(responsible: Guid.NewGuid())), 503));
        factory.AuthDown.TryRemove(s.Tenant, out _);
        factory.InventoryDown[s.Tenant] = true;
        Assert.Equal("WAVE_INVENTORY_UNAVAILABLE", await ErrorCode(s.CreateAsync(), 503));
        factory.InventoryDown.TryRemove(s.Tenant, out _);

        Assert.Empty(s.Stored(WaveCollections.Waves));
    }

    // ── AC-4 ─────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("tenantId")]
    [InlineData("legalEntityId")]
    public async Task AC4_a_scope_member_in_the_body_or_the_query_is_400(string member)
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var body = s.Form();
        body[member] = Guid.NewGuid().ToString("D");

        var refused = await Expect(s.CreateAsync(body), 400);

        Assert.Equal("INVALID_REQUEST", refused.GetProperty("error").GetProperty("code").GetString());
        Assert.Contains("never accepted from a body", refused.GetProperty("error").GetProperty("message").GetString());
        Assert.Empty(s.Stored(WaveCollections.Waves));
        Assert.Equal(400, (int)(await s.GetAsync($"/api/warehouse/waves?warehouseId={s.Warehouse:D}&{member}={Guid.NewGuid():D}")).StatusCode);
    }

    // ── AC-6 ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC6_create_replays_the_same_key_and_refuses_the_key_for_another_body()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var key = NewKey();
        var form = s.Form(note: "bir");

        var first = await Expect(s.CreateAsync(form, key), 201);
        var replay = await Expect(s.CreateAsync(form, key), 200);

        Assert.Equal(Id(first), Id(replay));
        Assert.Equal(first.GetProperty("number").GetString(), replay.GetProperty("number").GetString());
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await ErrorCode(s.CreateAsync(s.Form(note: "iki"), key), 400));
        Assert.Single(s.Stored(WaveCollections.Waves));
        Assert.Single(s.Events(Id(first)));
        Assert.Single(s.CentralRows("CreateWaveCommand"));
    }

    [Fact]
    public async Task AC6_update_and_cancel_replay_the_same_key_and_refuse_it_for_another_body()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var wave = await s.CreatedAsync();
        var key = NewKey();

        var updated = await Expect(s.UpdateAsync(Id(wave), s.Form(note: "duzenlendi"), ETag(wave), key), 200);
        // the replay carries the OLD If-Match: the key answers before If-Match (draft §1 rule 5)
        var replay = await Expect(s.UpdateAsync(Id(wave), s.Form(note: "duzenlendi"), ETag(wave), key), 200);
        Assert.Equal(2, updated.GetProperty("version").GetInt32());
        Assert.Equal(2, replay.GetProperty("version").GetInt32());
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await ErrorCode(s.UpdateAsync(Id(wave), s.Form(note: "baska"), ETag(wave), key), 400));
        Assert.Equal(2, s.Events(Id(wave)).Count);
        Assert.Single(s.CentralRows("UpdateWaveCommand"));

        var cancelKey = NewKey();
        var cancelled = await Expect(s.CancelAsync(Id(wave), ETag(updated), key: cancelKey), 200);
        var cancelReplay = await Expect(s.CancelAsync(Id(wave), ETag(updated), key: cancelKey), 200);
        Assert.Equal("Cancelled", cancelReplay.GetProperty("status").GetString());
        Assert.Equal(cancelled.GetProperty("version").GetInt32(), cancelReplay.GetProperty("version").GetInt32());
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await ErrorCode(s.CancelAsync(Id(wave), ETag(updated), "Baska bir gerekce metni", cancelKey), 400));
        Assert.Equal(3, s.Events(Id(wave)).Count);
        Assert.Single(s.CentralRows("CancelWaveCommand"));
    }

    [Fact]
    public async Task AC6_add_and_remove_replay_the_same_key_and_refuse_it_for_another_body()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var wave = await s.CreatedAsync();
        var (o1, o2) = (s.SeedOutbound(), s.SeedOutbound());
        var key = NewKey();

        var added = await Expect(s.AddAsync(Id(wave), [o1], ETag(wave), key: key), 200);
        var replay = await Expect(s.AddAsync(Id(wave), [o1], ETag(wave), key: key), 200);
        Assert.Equal(2, replay.GetProperty("wave").GetProperty("version").GetInt32());
        Assert.Equal("Added", Assert.Single(replay.GetProperty("orders").EnumerateArray()).GetProperty("outcome").GetString());
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await ErrorCode(s.AddAsync(Id(wave), [o2], ETag(wave), key: key), 400));
        Assert.Single(s.Orders(Id(wave)));
        Assert.Equal(2, s.Events(Id(wave)).Count);
        Assert.Single(s.CentralRows("AddWaveOrdersCommand"));

        var removeKey = NewKey();
        var etag = ETag(added.GetProperty("wave"));
        var removed = await Expect(s.RemoveAsync(Id(wave), [o1], etag, key: removeKey), 200);
        var removeReplay = await Expect(s.RemoveAsync(Id(wave), [o1], etag, key: removeKey), 200);
        Assert.Equal(3, removed.GetProperty("wave").GetProperty("version").GetInt32());
        Assert.Equal(3, removeReplay.GetProperty("wave").GetProperty("version").GetInt32());
        Assert.Equal("Removed", Assert.Single(removeReplay.GetProperty("orders").EnumerateArray()).GetProperty("outcome").GetString());
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await ErrorCode(s.RemoveAsync(Id(wave), [o2], etag, key: removeKey), 400));
        Assert.Equal(3, s.Events(Id(wave)).Count);
        Assert.Single(s.CentralRows("RemoveWaveOrdersCommand"));
    }

    // ── AC-7 ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC7_a_stale_If_Match_is_409_on_every_wave_write_and_a_missing_one_is_400()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var wave = await s.CreatedAsync();
        var outbound = s.SeedOutbound();
        var current = await Expect(s.UpdateAsync(Id(wave), s.Form(note: "v2"), ETag(wave)), 200);
        var stale = ETag(wave);

        Assert.Equal("WAVE_CONFLICT", await ErrorCode(s.UpdateAsync(Id(wave), s.Form(note: "v3"), stale), 409));
        Assert.Equal("WAVE_CONFLICT", await ErrorCode(s.AddAsync(Id(wave), [outbound], stale), 409));
        Assert.Equal("WAVE_CONFLICT", await ErrorCode(s.RemoveAsync(Id(wave), [outbound], stale), 409));
        Assert.Equal("WAVE_CONFLICT", await ErrorCode(s.CancelAsync(Id(wave), stale), 409));
        Assert.Equal("WAVE_CONFLICT", await ErrorCode(s.UpdateAsync(Id(wave), s.Form(note: "v3"), "\"abc\""), 409));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.Send(HttpMethod.Post, $"/api/warehouse/waves/{Id(wave)}/cancel",
            new Dictionary<string, object?> { ["idempotencyKey"] = "key-without-if-match", ["reason"] = Reason }, "key-without-if-match", null), 400));

        var after = await s.WaveAsync(Id(wave));
        Assert.Equal(current.GetProperty("version").GetInt32(), after.GetProperty("version").GetInt32());
        Assert.Equal("v2", after.GetProperty("note").GetString());
        Assert.Equal(2, s.Events(Id(wave)).Count);
    }

    [Fact]
    public async Task AC7_of_two_concurrent_edits_exactly_one_wins()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var wave = await s.CreatedAsync();
        var both = new Barrier(2);
        factory.BeforeWaveTransaction[s.Tenant] = () => Task.Run(() => both.SignalAndWait(TimeSpan.FromSeconds(20)));

        var calls = await Task.WhenAll(
            s.UpdateAsync(Id(wave), s.Form(note: "A"), ETag(wave)),
            s.UpdateAsync(Id(wave), s.Form(note: "B"), ETag(wave)));
        factory.BeforeWaveTransaction.TryRemove(s.Tenant, out _);

        Assert.Equal([200, 409], calls.Select(c => (int)c.StatusCode).Order());
        Assert.Equal(2, (await s.WaveAsync(Id(wave))).GetProperty("version").GetInt32());
        Assert.Equal(2, s.Events(Id(wave)).Count);
    }

    /// <summary>S-7: the STORE refuses a write whose expected version is not the stored one — measured on the production repository.</summary>
    [Fact]
    public async Task AC7_the_store_replaces_a_wave_only_at_its_expected_version()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var wave = await s.CreatedAsync(s.Form(note: "saklanan"));
        var waveId = Guid.Parse(Id(wave));

        var replaced = await s.WithRepositoryAsync(async repository =>
        {
            var stored = (await repository.FindAsync(waveId))!;
            stored.Note = "uzerine yazildi";
            stored.Version = 99;
            var matched = false;
            await repository.InTransactionAsync(async (session, ct) =>
            {
                matched = await session.ReplaceWaveAsync(stored, expectedVersion: 7, ct);
                return new WaveWriteOutcome(0, null);
            });
            return matched;
        });

        Assert.False(replaced);
        Assert.Equal("saklanan", s.StoredWave(Id(wave))["Note"].AsString);
        Assert.Equal(1, s.StoredWave(Id(wave))["Version"].AsInt32);
    }

    // ── AC-8 ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC8_cancel_needs_a_reason_keeps_the_wave_and_closes_its_memberships()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var wave = await s.AddedAsync(await s.CreatedAsync(), s.SeedOutbound(), s.SeedOutbound());

        Assert.Equal("WAVE_REASON_REQUIRED", await ErrorCode(s.CancelAsync(Id(wave), ETag(wave), reason: null), 400));
        Assert.Equal("WAVE_REASON_REQUIRED", await ErrorCode(s.CancelAsync(Id(wave), ETag(wave), reason: "kisa"), 400));
        Assert.Equal("WAVE_REASON_REQUIRED", await ErrorCode(s.CancelAsync(Id(wave), ETag(wave), reason: new string('r', 501)), 400));

        var cancelled = await Expect(s.CancelAsync(Id(wave), ETag(wave)), 200);

        Assert.Equal("Cancelled", cancelled.GetProperty("status").GetString());
        Assert.Equal(Reason, cancelled.GetProperty("cancelReason").GetString());
        Assert.Equal(s.User.ToString("D"), cancelled.GetProperty("cancelledBy").GetString());
        Assert.Equal(0, cancelled.GetProperty("orderCount").GetInt32());
        var stored = s.StoredWave(Id(wave));
        Assert.False(stored["IsDeleted"].AsBoolean); // never deleted
        Assert.Equal("Cancelled", (await s.WaveAsync(Id(wave))).GetProperty("status").GetString()); // GET still answers it
        Assert.All(s.Orders(Id(wave)), o =>
        {
            Assert.Equal("Removed", o["State"].AsString);
            Assert.False(o["Active"].AsBoolean);
            Assert.Equal(Reason, o["CloseReason"].AsString);
        });
        Assert.Equal(2, s.Orders(Id(wave)).Count);
        var last = s.Events(Id(wave)).Last();
        Assert.Equal("Cancelled", last["Kind"].AsString);
        Assert.Equal(Reason, last["Data"]["reason"].AsString);
        Assert.Equal("2", last["Data"]["removedOrderCount"].AsString);

        // a cancelled wave is terminal: every write is 409 WAVE_INVALID_STATE
        var etag = ETag(cancelled);
        Assert.Equal("WAVE_INVALID_STATE", await ErrorCode(s.CancelAsync(Id(wave), etag), 409));
        Assert.Equal("WAVE_INVALID_STATE", await ErrorCode(s.UpdateAsync(Id(wave), s.Form(), etag), 409));
        Assert.Equal("WAVE_INVALID_STATE", await ErrorCode(s.AddAsync(Id(wave), [s.SeedOutbound()], etag), 409));
        Assert.Equal("WAVE_INVALID_STATE", await ErrorCode(s.RemoveAsync(Id(wave), [Guid.NewGuid()], etag), 409));
        Assert.Equal("WAVE_INVALID_STATE", await ErrorCode(s.GetAsync($"/api/warehouse/waves/{Id(wave)}/suggestions"), 409));
    }

    /// <summary>S-8b: only a Draft is cancelled — a released wave's cancel is 409 and changes nothing (its way out is the recall, D-9).</summary>
    [Fact]
    public async Task AC8_a_released_wave_cannot_be_cancelled()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var wave = await s.CreatedAsync();
        s.ForceStatus(Id(wave), "Released"); // DL-1 cannot release through the API (DL-2)

        Assert.Equal("WAVE_INVALID_STATE", await ErrorCode(s.CancelAsync(Id(wave), ETag(wave)), 409));

        Assert.Equal("Released", s.StoredWave(Id(wave))["Status"].AsString);
        Assert.Single(s.Events(Id(wave)));
        Assert.Empty(s.CentralRows("CancelWaveCommand"));
    }

    // ── AC-9 ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC9_a_writing_command_leaves_one_central_record_a_replay_and_a_refusal_none()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var settingsRows = s.CentralRows("UpdateWaveWarehouseSettingsCommand");
        Assert.Single(settingsRows); // the scenario's own setting write
        Assert.Equal("WAVE-PLANNING", settingsRows[0]["SourceModule"].AsString);

        var wave = await s.CreatedAsync();
        var created = Assert.Single(s.CentralRows("CreateWaveCommand"));
        Assert.Equal("WAVE-PLANNING", created["SourceModule"].AsString);
        Assert.Equal("Create", created["Operation"].AsString);
        Assert.Equal("Wave", created["EntityType"].AsString);
        Assert.Equal(Id(wave), created["EntityId"].AsString);

        // refusals write nothing
        Assert.Equal("WAVE_CONFLICT", await ErrorCode(s.UpdateAsync(Id(wave), s.Form(note: "x"), "W/\"9\""), 409));
        Assert.Equal("WAVE_REASON_REQUIRED", await ErrorCode(s.CancelAsync(Id(wave), ETag(wave), reason: null), 400));
        Assert.Empty(s.CentralRows("UpdateWaveCommand"));
        Assert.Empty(s.CentralRows("CancelWaveCommand"));

        // a remove that removes nothing writes nothing — no event, no version, no central record
        var nothing = await Expect(s.RemoveAsync(Id(wave), [Guid.NewGuid()], ETag(wave)), 200);
        Assert.Equal("NotMember", Assert.Single(nothing.GetProperty("orders").EnumerateArray()).GetProperty("outcome").GetString());
        Assert.Equal(1, nothing.GetProperty("wave").GetProperty("version").GetInt32());
        Assert.Empty(s.CentralRows("RemoveWaveOrdersCommand"));
        Assert.Single(s.Events(Id(wave)));

        var cancelled = await Expect(s.CancelAsync(Id(wave), ETag(wave)), 200);
        var row = Assert.Single(s.CentralRows("CancelWaveCommand"));
        Assert.Equal("LifecycleTransition", row["Operation"].AsString);
        Assert.Equal(Id(cancelled), row["EntityId"].AsString);
    }

    // ── AC-10 ────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("number", "GMG-WAV-999999")]
    [InlineData("status", "Released")]
    [InlineData("cutoffAtUtc", "2026-11-20T11:30:00.000Z")]
    [InlineData("timeZoneId", "Europe/Berlin")]
    public async Task AC10_a_server_member_in_the_body_is_400(string member, string value)
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var wave = await s.CreatedAsync();
        var body = s.Form();
        body[member] = value;

        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.CreateAsync(body), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.UpdateAsync(Id(wave), body, ETag(wave)), 400));
        Assert.Single(s.Stored(WaveCollections.Waves));
    }

    [Fact]
    public async Task AC10_the_number_never_changes_concurrent_creates_get_different_numbers_and_a_dead_counter_is_503()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var wave = await s.CreatedAsync();
        var updated = await Expect(s.UpdateAsync(Id(wave), s.Form(note: "ayni numara"), ETag(wave)), 200);
        Assert.Equal(wave.GetProperty("number").GetString(), updated.GetProperty("number").GetString());

        var twins = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => s.CreatedAsync()));
        Assert.Equal(6, twins.Select(t => t.GetProperty("number").GetString()).Distinct().Count());

        factory.CounterDown[s.Tenant] = true;
        var before = s.Stored(WaveCollections.Waves).Count;
        Assert.Equal("WAVE_RECORD_CODE_UNAVAILABLE", await ErrorCode(s.CreateAsync(), 503));
        factory.CounterDown.TryRemove(s.Tenant, out _);
        Assert.Equal(before, s.Stored(WaveCollections.Waves).Count);

        // 0180 keeps no counter of its own: the numbers come from MOD-0178's counter collection, type WAV
        var counter = Assert.Single(s.Stored("wms_number_counters"), c => c.ToJson().Contains("WAV"));
        Assert.DoesNotContain(WaveCollections.All, name => name.Contains("counter", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(counter);
    }

    [Fact]
    public async Task A_company_code_that_is_not_two_to_five_capitals_is_422()
    {
        var s = await WaveScenario.ManagedAsync(factory, companyCode: "gm1");

        Assert.Equal("WAVE_COMPANY_CODE_INVALID", await ErrorCode(s.CreateAsync(), 422));
        Assert.Empty(s.Stored(WaveCollections.Waves));
    }

    // ── AC-14 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC14_the_cutoff_is_converted_in_the_warehouse_time_zone_never_the_servers()
    {
        // pinned clock: 2026-03-01 — before the US spring-forward gap (2026-03-08) and the autumn overlap (2026-11-01)
        factory.WaveClock.Advance(new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero) - factory.WaveClock.GetUtcNow());
        var istanbul = await WaveScenario.ManagedAsync(factory);
        var newYork = await WaveScenario.ManagedAsync(factory, "America/New_York");
        var tokyo = await WaveScenario.ManagedAsync(factory, "Asia/Tokyo");

        var tr = await istanbul.CreatedAsync(istanbul.Form(cutoffLocal: "2026-11-20T14:30"));
        Assert.Equal("2026-11-20T11:30:00.000Z", tr.GetProperty("cutoffAtUtc").GetString());

        var jp = await tokyo.CreatedAsync(tokyo.Form(cutoffLocal: "2026-11-20T14:30"));
        Assert.Equal("2026-11-20T05:30:00.000Z", jp.GetProperty("cutoffAtUtc").GetString());
        Assert.Equal("Asia/Tokyo", jp.GetProperty("timeZoneId").GetString());

        var us = await newYork.CreatedAsync(newYork.Form(cutoffLocal: "2026-07-01T14:30"));
        Assert.Equal("2026-07-01T18:30:00.000Z", us.GetProperty("cutoffAtUtc").GetString()); // EDT, UTC-4

        // the spring-forward gap: 02:30 does not exist on 2026-03-08 in New York
        Assert.Equal("WAVE_CUTOFF_INVALID", await ErrorCode(newYork.CreateAsync(newYork.Form(cutoffLocal: "2026-03-08T02:30")), 400));

        // the autumn overlap: 01:30 happens twice on 2026-11-01 — the EARLIER instant (EDT, UTC-4) is taken and the answer says so
        var ambiguous = await newYork.CreatedAsync(newYork.Form(cutoffLocal: "2026-11-01T01:30"));
        Assert.Equal("2026-11-01T05:30:00.000Z", ambiguous.GetProperty("cutoffAtUtc").GetString());
        Assert.True(ambiguous.GetProperty("cutoffAmbiguousResolved").GetBoolean());
        Assert.True((await newYork.WaveAsync(Id(ambiguous))).GetProperty("cutoffAmbiguousResolved").GetBoolean()); // a persisted member

        // an edit converts in the WAVE's own zone and clears the mark when the new hour is not ambiguous
        var edited = await Expect(newYork.UpdateAsync(Id(ambiguous), newYork.Form(cutoffLocal: "2026-11-01T03:30"), ETag(ambiguous)), 200);
        Assert.Equal("2026-11-01T08:30:00.000Z", edited.GetProperty("cutoffAtUtc").GetString()); // EST, UTC-5
        Assert.False(edited.TryGetProperty("cutoffAmbiguousResolved", out _));

        // more than one hour in the past
        Assert.Equal("WAVE_CUTOFF_INVALID", await ErrorCode(istanbul.CreateAsync(istanbul.Form(cutoffLocal: "2026-03-01T09:30")), 400)); // 06:30Z < 07:00Z
        Assert.Equal(201, (int)(await istanbul.CreateAsync(istanbul.Form(cutoffLocal: "2026-03-01T10:30"))).StatusCode); // 07:30Z, within the hour
    }

    /// <summary>
    /// CT decision 2026-11-13 (pack §8.4 to the letter): a Draft whose cutoff lies more than one hour in the past cannot be EDITED unless the
    /// edit moves the cutoff forward — 400 <c>WAVE_CUTOFF_INVALID</c>, nothing written. CANCELLING it has no cutoff check.
    /// </summary>
    [Fact]
    public async Task AC14_a_draft_past_its_cutoff_is_edited_only_with_a_new_cutoff_and_can_always_be_cancelled()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var wave = await s.CreatedAsync(s.Form(note: "eski kesim"));
        var stale = s.Form(note: "yalniz not", cutoffLocal: wave.GetProperty("cutoffLocal").GetString());
        factory.WaveClock.Advance(TimeSpan.FromDays(4)); // the cutoff (2 days ahead) is now 2 days behind

        Assert.Equal("WAVE_CUTOFF_INVALID", await ErrorCode(s.UpdateAsync(Id(wave), stale, ETag(wave)), 400));
        Assert.Equal("eski kesim", (await s.WaveAsync(Id(wave))).GetProperty("note").GetString());
        Assert.Single(s.Events(Id(wave)));

        var moved = await Expect(s.UpdateAsync(Id(wave), s.Form(note: "kesim ileri alindi"), ETag(wave)), 200); // the form's default cutoff follows the clock
        Assert.Equal("kesim ileri alindi", moved.GetProperty("note").GetString());

        var other = await s.CreatedAsync();
        factory.WaveClock.Advance(TimeSpan.FromDays(4));
        var cancelled = await Expect(s.CancelAsync(Id(other), ETag(other)), 200); // no cutoff rule on cancel
        Assert.Equal("Cancelled", cancelled.GetProperty("status").GetString());
    }

    [Fact]
    public async Task AC14_without_the_warehouse_setting_create_is_422()
    {
        var s = new WaveScenario(factory); // no setting record at all

        Assert.Equal("WAVE_SETTINGS_REQUIRED", await ErrorCode(s.CreateAsync(), 422));

        await s.ConfiguredAsync(false, Istanbul); // a record, not wave managed
        Assert.Equal("WAVE_SETTINGS_REQUIRED", await ErrorCode(s.CreateAsync(), 422));
        Assert.Empty(s.Stored(WaveCollections.Waves));
    }

    // ── reads ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_list_needs_the_warehouse_filters_and_sorts_by_cutoff_and_the_timeline_pages()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var day = s.Day(3);
        var late = await s.CreatedAsync(s.Form("Region", $"{day}T18:00"));
        var early = await s.CreatedAsync(s.Form("Cargo", $"{day}T08:00"));
        await Expect(s.CancelAsync(Id(late), ETag(late)), 200);

        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.GetAsync("/api/warehouse/waves"), 400)); // warehouseId is required (pack §8.1)
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.GetAsync($"/api/warehouse/waves?warehouseId={s.Warehouse:D}&status=Open"), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.GetAsync($"/api/warehouse/waves?warehouseId={s.Warehouse:D}&pageSize=201"), 400));

        var all = await Expect(s.GetAsync($"/api/warehouse/waves?warehouseId={s.Warehouse:D}"), 200);
        Assert.Equal([Id(early), Id(late)], all.GetProperty("items").EnumerateArray().Select(Id));
        var drafts = await Expect(s.GetAsync($"/api/warehouse/waves?warehouseId={s.Warehouse:D}&status=Draft"), 200);
        Assert.Equal([Id(early)], drafts.GetProperty("items").EnumerateArray().Select(Id));
        var regions = await Expect(s.GetAsync($"/api/warehouse/waves?warehouseId={s.Warehouse:D}&waveType=Region&status=Cancelled&status=Draft"), 200);
        Assert.Equal([Id(late)], regions.GetProperty("items").EnumerateArray().Select(Id));
        var byNumber = await Expect(s.GetAsync($"/api/warehouse/waves?warehouseId={s.Warehouse:D}&q={early.GetProperty("number").GetString()}"), 200);
        Assert.Equal(1, byNumber.GetProperty("total").GetInt64());
        Assert.Equal(0, (await Expect(s.GetAsync($"/api/warehouse/waves?warehouseId={s.Warehouse2:D}"), 200)).GetProperty("total").GetInt64());

        var events = await Expect(s.GetAsync($"/api/warehouse/waves/{Id(late)}/events?pageSize=1&page=2"), 200);
        Assert.Equal(2, events.GetProperty("total").GetInt64());
        var second = Assert.Single(events.GetProperty("items").EnumerateArray());
        Assert.Equal("Cancelled", second.GetProperty("kind").GetString());
        Assert.Equal(s.User.ToString("D"), second.GetProperty("actorUserId").GetString());
        Assert.Equal(Reason, second.GetProperty("data").GetProperty("reason").GetString());

        Assert.Equal("WAVE_NOT_FOUND", await ErrorCode(s.GetAsync($"/api/warehouse/waves/{Guid.NewGuid():D}"), 404));
    }

    [Fact]
    public async Task Update_keeps_the_warehouse_and_writes_the_changed_fields_with_the_optional_reason()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var wave = await s.CreatedAsync(s.Form(note: "eski", maxOrders: 10));

        Assert.Equal("WAVE_WAREHOUSE_IMMUTABLE", await ErrorCode(s.UpdateAsync(Id(wave), s.Form(warehouse: s.Warehouse2), ETag(wave)), 400));

        var form = s.Form("Manual", note: "yeni");
        form["reason"] = "Musteri talebi";
        var updated = await Expect(s.UpdateAsync(Id(wave), form, ETag(wave)), 200);

        Assert.Equal("Manual", updated.GetProperty("waveType").GetString());
        Assert.False(updated.TryGetProperty("maxOrders", out _)); // the whole form: an absent optional member is cleared
        var data = s.Events(Id(wave)).Last()["Data"].AsBsonDocument;
        Assert.Equal("eski", data["before.note"].AsString);
        Assert.Equal("yeni", data["after.note"].AsString);
        Assert.Equal("10", data["before.maxOrders"].AsString);
        Assert.Equal(BsonNull.Value, data["after.maxOrders"]);
        Assert.Equal("Musteri talebi", data["reason"].AsString);
        Assert.False(data.Contains("before.cutoffLocal"));
    }
}
