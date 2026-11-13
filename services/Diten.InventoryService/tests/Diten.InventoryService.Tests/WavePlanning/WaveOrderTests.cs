using System.Text.Json;
using Diten.InventoryService.Persistence.WavePlanning;
using MongoDB.Bson;
using Xunit;
using static Diten.InventoryService.Tests.WavePlanning.WaveScenario;

namespace Diten.InventoryService.Tests.WavePlanning;

/// <summary>
/// MOD-0180 DL-1 over the REAL host — the wave's orders: eligibility and "one active wave per order" (AC-11), suggestions (AC-12), the business
/// limits and the technical ceiling (AC-13), the planner list and the load tab. MOD-0178's outbound documents are fixtures here (DL-1 cannot
/// yet produce a deferred outbound); the tests also hold that MOD-0180 never changes them (AC-16, behaviour side).
/// </summary>
public sealed class WaveOrderTests(WaveApiFactory factory) : IClassFixture<WaveApiFactory>
{
    private static List<(string OutboundId, string Code)> Rows(JsonElement refused)
        => refused.GetProperty("error").GetProperty("details").GetProperty("orders").EnumerateArray()
            .Select(r => (r.GetProperty("outboundId").GetString()!, r.GetProperty("code").GetString()!)).ToList();

    // ── AC-11 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC11_only_an_allocated_outbound_with_deferred_pick_issuance_enters_whole()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var wave = await s.CreatedAsync();
        var eligible = s.SeedOutbound(lines: 3, binsPerLine: 2);
        var untouched = s.StoredOutbound(eligible);

        var added = await Expect(s.AddAsync(Id(wave), [eligible], ETag(wave), via: "Suggestion"), 200);

        var row = Assert.Single(added.GetProperty("orders").EnumerateArray());
        Assert.Equal("Added", row.GetProperty("outcome").GetString());
        var order = row.GetProperty("order");
        Assert.Equal("Planned", order.GetProperty("state").GetString());
        Assert.Equal("Suggestion", order.GetProperty("addedVia").GetString());
        Assert.Equal(3, order.GetProperty("lineCount").GetInt32());        // the WHOLE order: every line …
        Assert.Equal(3, order.GetProperty("allocationCount").GetInt32()); // … every allocation …
        Assert.Equal(6, order.GetProperty("plannedTaskCount").GetInt32()); // … every planned bin
        Assert.Equal(1, added.GetProperty("wave").GetProperty("orderCount").GetInt32());
        Assert.Equal(3, added.GetProperty("wave").GetProperty("lineCount").GetInt32());
        Assert.Equal(6, added.GetProperty("wave").GetProperty("plannedTaskCount").GetInt32());
        Assert.Equal(2, added.GetProperty("wave").GetProperty("version").GetInt32());
        var stored = Assert.Single(s.Orders(Id(wave)));
        Assert.True(stored["Active"].AsBoolean);
        Assert.Equal(s.Warehouse.ToString("D"), stored["WarehouseId"].AsString);
        var orders = s.Events(Id(wave)).Last();
        Assert.Equal("OrdersAdded", orders["Kind"].AsString);
        Assert.Equal("Suggestion", orders["Data"]["addedVia"].AsString);
        Assert.Equal(untouched, s.StoredOutbound(eligible)); // MOD-0178's document is byte-for-byte what it was (pack §2.3 rule 2)

        var listed = await Expect(s.GetAsync($"/api/warehouse/waves/{Id(wave)}/orders?state=Planned"), 200);
        Assert.Equal(eligible.ToString("D"), Assert.Single(listed.GetProperty("items").EnumerateArray()).GetProperty("outboundId").GetString());
    }

    [Fact]
    public async Task AC11_an_ineligible_row_refuses_the_whole_request_with_every_row_code()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var wave = await s.CreatedAsync();
        var good = s.SeedOutbound();
        var allocating = s.SeedOutbound(status: "Allocating", pickIssuance: null);
        var picked = s.SeedOutbound(status: "Picked", pickIssuance: "Issued");
        var cancelledButDeferred = s.SeedOutbound(status: "Cancelled"); // a cancelled outbound keeps its Deferred mark (pack §4.5)
        var issuedAtAllocation = s.SeedOutbound(pickIssuance: null);     // tasks opened at allocation (today's 0178 behaviour)
        var alreadyIssued = s.SeedOutbound(pickIssuance: "Issued");
        var otherWarehouse = s.SeedOutbound(warehouse: s.Warehouse2);
        var otherCompany = s.SeedOutbound(legalEntity: Guid.NewGuid());
        var unknown = Guid.NewGuid();

        var refused = await Expect(s.AddAsync(Id(wave),
            [good, allocating, picked, cancelledButDeferred, issuedAtAllocation, alreadyIssued, otherWarehouse, otherCompany, unknown], ETag(wave)), 422);

        Assert.Equal("WAVE_ORDER_NOT_ELIGIBLE", refused.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(
            new[]
            {
                (allocating, "NotAllocated"), (picked, "NotAllocated"), (cancelledButDeferred, "NotAllocated"), (issuedAtAllocation, "NotDeferred"),
                (alreadyIssued, "NotDeferred"), (otherWarehouse, "OtherWarehouse"), (otherCompany, "NotFound"), (unknown, "NotFound")
            }.Select(x => (x.Item1.ToString("D"), x.Item2)).OrderBy(x => x.Item1),
            Rows(refused).OrderBy(x => x.OutboundId));
        Assert.Empty(s.Orders(Id(wave))); // all or nothing: the good row was not written either
        Assert.Single(s.Events(Id(wave)));
        Assert.Equal(1, (await s.WaveAsync(Id(wave))).GetProperty("version").GetInt32());
        Assert.Empty(s.CentralRows("AddWaveOrdersCommand"));

        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.AddAsync(Id(wave), [], ETag(wave)), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.AddAsync(Id(wave), [good, good], ETag(wave)), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.AddAsync(Id(wave), [good], ETag(wave), via: "Auto"), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.AddAsync(Id(wave), Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()), ETag(wave)), 400));
    }

    [Fact]
    public async Task AC11_an_order_is_in_at_most_one_active_wave()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var first = await s.CreatedAsync();
        var second = await s.CreatedAsync();
        var (shared, free) = (s.SeedOutbound(), s.SeedOutbound());
        first = await s.AddedAsync(first, shared);

        // every refused row is in another wave → 409
        var conflict = await Expect(s.AddAsync(Id(second), [shared], ETag(second)), 409);
        Assert.Equal("WAVE_ORDER_IN_OTHER_WAVE", conflict.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal([(shared.ToString("D"), "InOtherWave")], Rows(conflict));

        // a row refused for another reason turns the request into 422 — with all row codes
        var mixed = await Expect(s.AddAsync(Id(second), [shared, free, s.SeedOutbound(status: "Picked")], ETag(second)), 422);
        Assert.Equal("WAVE_ORDER_NOT_ELIGIBLE", mixed.GetProperty("error").GetProperty("code").GetString());
        Assert.Contains(Rows(mixed), r => r.Code == "InOtherWave");
        Assert.Contains(Rows(mixed), r => r.Code == "NotAllocated");

        // the same wave again → AlreadyInWave (422)
        var again = await Expect(s.AddAsync(Id(first), [shared], ETag(first)), 422);
        Assert.Equal([(shared.ToString("D"), "AlreadyInWave")], Rows(again));
        Assert.Empty(s.Orders(Id(second)));

        // once removed from the first wave the order is free again — and its old membership is kept as Removed
        first = (await Expect(s.RemoveAsync(Id(first), [shared], ETag(first), reason: "Yanlis dalga"), 200)).GetProperty("wave");
        Assert.Equal(0, first.GetProperty("orderCount").GetInt32());
        await s.AddedAsync(second, shared);
        var history = s.Orders(Id(first));
        Assert.Equal("Removed", Assert.Single(history)["State"].AsString);
        Assert.Equal("Yanlis dalga", history[0]["CloseReason"].AsString);
        Assert.Equal(s.User.ToString("D"), history[0]["ClosedBy"].AsString);
        Assert.Equal("Yanlis dalga", s.Events(Id(first)).Last()["Data"]["reason"].AsString);
    }

    /// <summary>S-11: of two concurrent adds of the SAME order into two waves exactly one wins; the loser wrote nothing.</summary>
    [Fact]
    public async Task AC11_of_two_concurrent_adds_of_one_order_exactly_one_wins()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var a = await s.CreatedAsync();
        var b = await s.CreatedAsync();
        var contested = s.SeedOutbound();
        var both = new Barrier(2);
        factory.BeforeWaveTransaction[s.Tenant] = () => Task.Run(() => both.SignalAndWait(TimeSpan.FromSeconds(20)));

        var calls = await Task.WhenAll(s.AddAsync(Id(a), [contested], ETag(a)), s.AddAsync(Id(b), [contested], ETag(b)));
        factory.BeforeWaveTransaction.TryRemove(s.Tenant, out _);

        Assert.Equal([200, 409], calls.Select(c => (int)c.StatusCode).Order());
        var loser = calls.Single(c => (int)c.StatusCode == 409);
        Assert.Contains("WAVE_ORDER_IN_OTHER_WAVE", await loser.Content.ReadAsStringAsync());
        var memberships = s.Stored(WaveCollections.Orders).Where(o => o["OutboundId"].AsString == contested.ToString("D")).ToList();
        Assert.Single(memberships);
        var (winnerWave, loserWave) = memberships[0]["WaveId"].AsString == Id(a) ? (a, b) : (b, a);
        Assert.Equal(2, (await s.WaveAsync(Id(winnerWave))).GetProperty("version").GetInt32());
        Assert.Equal(1, (await s.WaveAsync(Id(loserWave))).GetProperty("version").GetInt32()); // the loser's wave, timeline and central record are untouched
        Assert.Single(s.Events(Id(loserWave)));
        Assert.Single(s.CentralRows("AddWaveOrdersCommand"));
    }

    [Fact]
    public async Task An_add_into_a_warehouse_that_is_no_longer_wave_managed_is_422_and_removing_still_works()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var wave = await s.AddedAsync(await s.CreatedAsync(), s.SeedOutbound());
        var member = Guid.Parse(s.Orders(Id(wave)).Single()["OutboundId"].AsString);
        var gateBefore = s.Stored(WaveCollections.Settings).Single()["GateSeq"].AsInt32;
        Assert.Equal(1, gateBefore); // the add passed the gate once, in its own transaction (pack §8.19 "Sıralama")
        await s.ConfiguredAsync(false, Istanbul);

        Assert.Equal("WAVE_SETTINGS_REQUIRED", await ErrorCode(s.AddAsync(Id(wave), [s.SeedOutbound()], ETag(wave)), 422));
        Assert.Equal("WAVE_SETTINGS_REQUIRED", await ErrorCode(s.CreateAsync(), 422));
        Assert.Single(s.Orders(Id(wave)));

        // remove, edit and cancel stay allowed (pack §8.2 rule 1)
        var removed = await Expect(s.RemoveAsync(Id(wave), [member], ETag(wave)), 200);
        Assert.Equal("Removed", removed.GetProperty("orders")[0].GetProperty("outcome").GetString());
        var edited = await Expect(s.UpdateAsync(Id(wave), s.Form(note: "kapali depoda"), ETag(removed.GetProperty("wave"))), 200);
        Assert.Equal(200, (int)(await s.CancelAsync(Id(wave), ETag(edited))).StatusCode);
    }

    // ── AC-12 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC12_suggest_returns_the_eligible_matching_orders_oldest_first_up_to_the_room_and_writes_nothing()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var t0 = DateTime.UtcNow.AddHours(-10);
        var oldest = s.SeedOutbound(city: "Ankara", createdAt: t0, lines: 2);
        var middle = s.SeedOutbound(shipToName: "ANKARA Tip Merkezi", city: "Izmir", createdAt: t0.AddMinutes(1), lines: 2);
        var newest = s.SeedOutbound(city: "Bursa", postalCode: "ankara-16", createdAt: t0.AddMinutes(2), lines: 2);
        s.SeedOutbound(city: "Istanbul", postalCode: "34000", createdAt: t0.AddMinutes(-5));               // does not match the filter
        s.SeedOutbound(city: "Ankara", status: "Allocating", pickIssuance: null, createdAt: t0.AddMinutes(-9)); // not eligible
        s.SeedOutbound(city: "Ankara", warehouse: s.Warehouse2, createdAt: t0.AddMinutes(-8));              // another warehouse
        var taken = s.SeedOutbound(city: "Ankara", createdAt: t0.AddMinutes(-7));                           // in ANOTHER active wave
        await s.AddedAsync(await s.CreatedAsync(), taken);
        var wave = await s.CreatedAsync(s.Form(shipToFilter: "ankara"));
        var eventsBefore = s.Stored(WaveCollections.Events).Count;
        var ordersBefore = s.Stored(WaveCollections.Orders).Count;

        var all = await Expect(s.GetAsync($"/api/warehouse/waves/{Id(wave)}/suggestions"), 200);
        Assert.Equal(new[] { oldest, middle, newest }.Select(x => x.ToString("D")),
            all.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("outboundId").GetString()));
        Assert.Equal("Default", all.GetProperty("limitedBy").GetString());
        Assert.All(all.GetProperty("items").EnumerateArray(), i => Assert.True(i.GetProperty("eligible").GetBoolean()));

        // the room under MaxOrders: 2 − 1 already in the wave = 1
        var limited = await s.CreatedAsync(s.Form(shipToFilter: "ankara", maxOrders: 2));
        limited = await s.AddedAsync(limited, oldest);
        var one = await Expect(s.GetAsync($"/api/warehouse/waves/{Id(limited)}/suggestions"), 200);
        Assert.Equal([middle.ToString("D")], one.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("outboundId").GetString()));
        Assert.Equal("MaxOrders", one.GetProperty("limitedBy").GetString());

        // the room under MaxLines: 3 lines fit one 2-line order, not two
        var byLines = await s.CreatedAsync(s.Form(shipToFilter: "ankara", maxLines: 3));
        var lines = await Expect(s.GetAsync($"/api/warehouse/waves/{Id(byLines)}/suggestions"), 200);
        Assert.Equal([middle.ToString("D")], lines.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("outboundId").GetString()));
        Assert.Equal("MaxLines", lines.GetProperty("limitedBy").GetString());

        // suggesting wrote nothing: no membership, no event, no version
        Assert.Equal(ordersBefore + 1, s.Stored(WaveCollections.Orders).Count); // only the explicit add above
        Assert.Equal(eventsBefore + 3, s.Stored(WaveCollections.Events).Count); // two creates + that add
        Assert.Equal(1, (await s.WaveAsync(Id(wave))).GetProperty("version").GetInt32());
        Assert.Empty(s.Orders(Id(wave)));
    }

    // ── AC-13 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC13_an_add_beyond_the_business_limits_is_written_and_answers_the_warnings()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var wave = await s.CreatedAsync(s.Form(maxOrders: 1, maxLines: 3));

        var added = await Expect(s.AddAsync(Id(wave), [s.SeedOutbound(lines: 2), s.SeedOutbound(lines: 3)], ETag(wave)), 200);

        var answered = added.GetProperty("wave");
        Assert.Equal(2, answered.GetProperty("orderCount").GetInt32());
        Assert.Equal(5, answered.GetProperty("lineCount").GetInt32());
        var warnings = answered.GetProperty("limitWarnings").EnumerateArray()
            .Select(w => (w.GetProperty("kind").GetString(), w.GetProperty("limit").GetInt32(), w.GetProperty("actual").GetInt32())).ToList();
        Assert.Equal([("MaxOrders", 1, 2), ("MaxLines", 3, 5)], warnings);
        Assert.Equal(2, s.Orders(Id(wave)).Count); // written — the limit is a warning (D-7)
        Assert.Equal("[MaxOrders:2/1,MaxLines:5/3]", s.Events(Id(wave)).Last()["Data"]["limitWarnings"].AsString);
        Assert.Equal(2, (await s.WaveAsync(Id(wave))).GetProperty("limitWarnings").GetArrayLength()); // every read computes it
    }

    [Fact]
    public async Task AC13_the_technical_ceiling_refuses_the_add()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var wave = await s.AddedAsync(await s.CreatedAsync(), s.SeedOutbound(lines: 1, binsPerLine: 1200));

        // 1.200 + 801 planned tasks > 2.000
        Assert.Equal("WAVE_TOO_LARGE", await ErrorCode(s.AddAsync(Id(wave), [s.SeedOutbound(lines: 1, binsPerLine: 801)], ETag(wave)), 422));
        Assert.Single(s.Orders(Id(wave)));
        // exactly 2.000 is still allowed
        Assert.Equal(200, (int)(await s.AddAsync(Id(wave), [s.SeedOutbound(lines: 1, binsPerLine: 800)], ETag(wave))).StatusCode);

        // more than 500 orders: 3 requests of 200 / 200 / 101
        var big = await s.CreatedAsync();
        var orders = Enumerable.Range(0, 501).Select(_ => s.SeedOutbound()).ToList();
        big = (await Expect(s.AddAsync(Id(big), orders.Take(200), ETag(big)), 200)).GetProperty("wave");
        big = (await Expect(s.AddAsync(Id(big), orders.Skip(200).Take(200), ETag(big)), 200)).GetProperty("wave");
        Assert.Equal("WAVE_TOO_LARGE", await ErrorCode(s.AddAsync(Id(big), orders.Skip(400), ETag(big)), 422));
        Assert.Equal(400, s.Orders(Id(big)).Count);
        Assert.Equal(200, (int)(await s.AddAsync(Id(big), orders.Skip(400).Take(100), ETag(big))).StatusCode); // exactly 500
    }

    // ── planner list and load ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_planner_list_shows_eligible_orders_and_grey_allocating_ones_without_street_lines()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var t0 = DateTime.UtcNow.AddHours(-5);
        var eligible = s.SeedOutbound(city: "Konya", createdAt: t0, orderRef: "SO-7781");
        var allocating = s.SeedOutbound(status: "Allocating", pickIssuance: null, city: null, postalCode: null, createdAt: t0.AddMinutes(1));
        s.SeedOutbound(status: "Picked", pickIssuance: "Issued", createdAt: t0.AddMinutes(2));
        s.SeedOutbound(status: "Cancelled", createdAt: t0.AddMinutes(3));
        var inWave = s.SeedOutbound(createdAt: t0.AddMinutes(4));
        await s.AddedAsync(await s.CreatedAsync(), inWave);

        var page = await Expect(s.GetAsync($"/api/warehouse/waves/candidates?warehouseId={s.Warehouse:D}"), 200);

        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([eligible.ToString("D"), allocating.ToString("D")], items.Select(i => i.GetProperty("outboundId").GetString()));
        Assert.True(items[0].GetProperty("eligible").GetBoolean());
        Assert.Equal("SO-7781", items[0].GetProperty("orderRef").GetString());
        Assert.Equal("Konya", items[0].GetProperty("shipTo").GetProperty("city").GetString());
        Assert.False(items[0].GetProperty("shipTo").TryGetProperty("line1", out _)); // the street is never on this wire
        Assert.False(items[1].GetProperty("eligible").GetBoolean());
        Assert.Equal("AllocationPending", items[1].GetProperty("ineligibleReason").GetString());
        Assert.False(items[1].GetProperty("shipTo").TryGetProperty("city", out _));

        var filtered = await Expect(s.GetAsync($"/api/warehouse/waves/candidates?warehouseId={s.Warehouse:D}&shipTo=kony&q=so-77"), 200);
        Assert.Equal(1, filtered.GetProperty("total").GetInt64());
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.GetAsync("/api/warehouse/waves/candidates"), 400));
        factory.InventoryDown[s.Tenant] = true;
        Assert.Equal("WAVE_INVENTORY_UNAVAILABLE", await ErrorCode(s.GetAsync($"/api/warehouse/waves/candidates?warehouseId={s.Warehouse:D}"), 503));
        factory.InventoryDown.TryRemove(s.Tenant, out _);
    }

    [Fact]
    public async Task The_load_tab_counts_planned_bins_per_zone_and_aisle()
    {
        var s = await WaveScenario.ManagedAsync(factory);
        var binInZone = Guid.Parse(s.Inventory.LocationA);
        var wave = await s.CreatedAsync();
        Assert.Empty((await Expect(s.GetAsync($"/api/warehouse/waves/{Id(wave)}/load"), 200)).GetProperty("zones").EnumerateArray());

        wave = await s.AddedAsync(wave, s.SeedOutbound(lines: 2, binsPerLine: 2, bin: binInZone), s.SeedOutbound(lines: 1, binsPerLine: 1, bin: binInZone));
        var load = await Expect(s.GetAsync($"/api/warehouse/waves/{Id(wave)}/load"), 200);

        var only = Assert.Single(load.GetProperty("zones").EnumerateArray());
        Assert.Equal(s.Inventory.StorageNode.Code, only.GetProperty("zoneKey").GetString()); // no Zone on the bin's path → the storage location's code
        Assert.Equal(5, only.GetProperty("plannedTaskCount").GetInt32());
        Assert.Equal(2, only.GetProperty("orderCount").GetInt32());
        Assert.False(only.TryGetProperty("aisle", out _)); // the fixture's bin code is not in the bulk format
        Assert.Empty(load.GetProperty("pickers").EnumerateArray());
    }
}
