using System.Globalization;
using System.Text.Json;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Persistence.CycleCounting;
using Diten.InventoryService.Tests.TestInfrastructure;
using Diten.InventoryService.Tests.Warehouse;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using static Diten.InventoryService.Tests.CycleCounting.CycleCountScenario;

namespace Diten.InventoryService.Tests.CycleCounting;

/// <summary>
/// MOD-0181 DL-1 — the release over the REAL host and the REAL ledger: the expected-quantity snapshot (AC-2), its immutability (AC-4), the bin
/// claims without a location lock (AC-13) and the one-snapshot consistency of the balance and movement-id reads (AC-52 (a)).
/// </summary>
public sealed class CycleCountReleaseTests(CycleCountApiFactory factory) : IClassFixture<CycleCountApiFactory>
{
    private static decimal Dec(JsonElement e, string name) => decimal.Parse(e.GetProperty(name).GetString()!, CultureInfo.InvariantCulture);

    private static List<Guid> Ids(BsonDocument count, string field) => count[field].AsBsonArray.Select(v => Guid.Parse(v.ToString()!)).ToList();

    // ── AC-2 ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC2_release_takes_one_line_per_AVAILABLE_balance_with_stock_equal_to_the_ledger()
    {
        var s = new CycleCountScenario(factory);
        var second = s.AddSku();
        await s.ReceiveAsync(12, s.BinA);
        await s.ReceiveAsync(4, s.BinA, second);
        await s.ReceiveAsync(7, s.BinB);
        await s.ReceiveAsync(3, s.BinA, status: "QUARANTINE");  // held stock is not counted in v1 (Q-8 = A)
        var empty = s.Node();                                     // a stockable bin without stock: in the scope, no line
        var count = await s.CreatedAsync(s.Form(scope: [s.Storage]));
        var released = await s.ReleasedAsync(count);

        Assert.Equal("Released", released.GetProperty("status").GetString());
        Assert.True(released.TryGetProperty("snapshotAt", out _));
        var bins = released.GetProperty("scopeBinIds").EnumerateArray().Select(b => Guid.Parse(b.GetString()!)).ToHashSet();
        Assert.Superset(new HashSet<Guid> { s.BinA, s.BinB, empty }, bins);
        Assert.DoesNotContain(Guid.Parse(s.Inventory.LocationC), bins);

        var lines = await s.LinesAsync(Id(count));
        Assert.Equal(3, lines.Count);
        Assert.Equal(3, released.GetProperty("lineCount").GetInt32());
        Assert.All(lines, l => Assert.Equal("AVAILABLE", l.GetProperty("stockStatus").GetString()));
        Assert.All(lines, l => Assert.Equal("Snapshot", l.GetProperty("origin").GetString()));
        Assert.All(lines, l => Assert.Equal("Open", l.GetProperty("status").GetString()));
        foreach (var line in lines)
        {
            var bin = Guid.Parse(line.GetProperty("locationId").GetString()!);
            var sku = line.GetProperty("item").GetProperty("skuId").GetString();
            var ledger = s.Stored("inventory_balances").Single(b => b["LocationId"].AsString == bin.ToString("D") && b["SkuId"].AsString == sku
                                                                     && b["StockStatus"].AsString == "Available");
            Assert.Equal(ledger["OnHand"].ToDecimal(), Dec(line, "expectedQuantity"));
        }

        var stored = s.StoredCount(Id(count));
        Assert.NotEmpty(Ids(stored, "SnapshotTransactionIds")); // the receipts are inside the window W
        var released1 = s.Events(Id(count)).Last();
        Assert.Equal("Released", released1["Kind"].AsString);
        Assert.Equal("3", released1["Data"]["lineCount"].AsString);
        Assert.DoesNotContain(released1["Data"].AsBsonDocument.Names, n => n.Contains("expected", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(bins.Count, s.Claims(Id(count)).Count);
    }

    [Fact]
    public async Task AC2_the_SKU_filter_limits_the_lines()
    {
        var s = new CycleCountScenario(factory);
        var second = s.AddSku();
        await s.ReceiveAsync(12, s.BinA);
        await s.ReceiveAsync(4, s.BinA, second);
        var count = await s.CreatedAsync(s.Form(scope: [s.BinA], skus: [s.Inventory.Sku]));
        await s.ReleasedAsync(count);

        var line = Assert.Single(await s.LinesAsync(Id(count)));
        Assert.Equal(s.Inventory.Sku.ToString("D"), line.GetProperty("item").GetProperty("skuId").GetString());
        Assert.Equal(12m, Dec(line, "expectedQuantity"));
    }

    [Fact]
    public async Task AC2_Full_counts_the_whole_warehouse_and_an_empty_scope_is_422()
    {
        var s = new CycleCountScenario(factory);
        await s.ReceiveAsync(2, s.BinA);
        await s.ReceiveAsync(2, Guid.Parse(s.Inventory.LocationC));
        var full = await s.CreatedAsync(s.Form(countType: "Full"));
        await s.ReleasedAsync(full);
        Assert.Single(await s.LinesAsync(Id(full)));

        var dead = s.Node(LocationType.Zone, stockable: false);
        var emptyScope = await s.CreatedAsync(s.Form(scope: [dead]));
        Assert.Equal("COUNT_SCOPE_EMPTY", await ErrorCode(s.ReleaseAsync(Id(emptyScope), ETag(emptyScope)), 422));
        Assert.Equal("Draft", s.StoredCount(Id(emptyScope))["Status"].AsString);
        Assert.Empty(s.Claims(Id(emptyScope)));
    }

    [Fact]
    public async Task AC2_a_scope_over_the_line_limit_is_422_and_writes_nothing()
    {
        var s = new CycleCountScenario(factory);
        var zone = LocationSeed.Node(s.Tenant, s.LegalEntity, LocationType.Zone, "Z-" + Guid.NewGuid().ToString("N")[..8], s.Inventory.StorageNode);
        var bins = Enumerable.Range(0, 2001).Select(i => LocationSeed.Node(s.Tenant, s.LegalEntity, LocationType.Bin, $"ZB-{i:D4}-{Guid.NewGuid():N}"[..20], zone)).ToArray();
        LocationSeed.Insert(factory.Database, [zone, .. bins]);
        var count = await s.CreatedAsync(s.Form(scope: [zone.Id]));

        Assert.Equal("COUNT_SCOPE_TOO_LARGE", await ErrorCode(s.ReleaseAsync(Id(count), ETag(count)), 422));
        Assert.Empty(s.Claims(Id(count)));
        Assert.Empty(s.Stored(CycleCountCollections.Lines));
    }

    [Fact]
    public async Task AC2_release_is_Draft_only()
    {
        var s = new CycleCountScenario(factory);
        await s.ReceiveAsync(1, s.BinA);
        var released = await s.ReleasedAsync(await s.CreatedAsync());
        Assert.Equal("COUNT_INVALID_STATE", await ErrorCode(s.ReleaseAsync(Id(released), ETag(released)), 409));
        Assert.Equal("COUNT_INVALID_STATE", await ErrorCode(s.UpdateAsync(Id(released), s.Form(), ETag(released)), 409));
    }

    // ── AC-4 ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC4_movements_after_release_change_neither_the_expected_quantity_nor_the_snapshot_time()
    {
        var s = new CycleCountScenario(factory);
        await s.ReceiveAsync(10, s.BinA);
        var count = await s.CreatedAsync();
        var released = await s.ReleasedAsync(count);
        var snapshotAt = released.GetProperty("snapshotAt").GetString();

        await s.TransferAsync(2, s.BinA, s.BinB);              // LOCATION_TRANSFER out of the scope bin
        await s.PostAsync("COUNT_GAIN", 1, s.BinA, inbound: true); // a manual MOD-0173 count correction
        await s.Inventory.PostExpectingAsync(s.Inventory.Issue("3", location: s.Inventory.LocationA), 201); // a sale
        Assert.Equal(6m, await s.OnHandAsync(s.BinA));

        var line = Assert.Single(await s.LinesAsync(Id(count)));
        Assert.Equal(10m, Dec(line, "expectedQuantity"));
        var again = await Expect(s.GetAsync($"/api/warehouse/cycle-counts/{Id(count)}"), 200);
        Assert.Equal(snapshotAt, again.GetProperty("snapshotAt").GetString());
        Assert.Equal(10m, s.Stored(CycleCountCollections.Lines).Single()["ExpectedQuantity"].ToDecimal());
    }

    // ── AC-13 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC13_ab_a_stocked_or_an_empty_bin_is_in_at_most_one_open_count_and_no_movement_is_blocked()
    {
        var s = new CycleCountScenario(factory);
        await s.ReceiveAsync(5, s.BinA);
        var empty = s.Node();
        await s.ReleasedAsync(await s.CreatedAsync(s.Form(scope: [s.BinA, empty])));

        var stocked = await s.CreatedAsync(s.Form(scope: [s.BinA]));
        Assert.Equal("COUNT_SCOPE_OVERLAP", await ErrorCode(s.ReleaseAsync(Id(stocked), ETag(stocked)), 409));
        var unstocked = await s.CreatedAsync(s.Form(scope: [empty]));
        Assert.Equal("COUNT_SCOPE_OVERLAP", await ErrorCode(s.ReleaseAsync(Id(unstocked), ETag(unstocked)), 409));
        Assert.Empty(s.Claims(Id(stocked)));
        Assert.Empty(s.Claims(Id(unstocked)));
        Assert.Equal(1, s.Stored(CycleCountCollections.Lines).Count);

        // Q-6 = B: the claimed bins keep moving
        await s.TransferAsync(2, s.BinA, s.BinB);
        await s.ReceiveAsync(1, empty);
        Assert.Equal(3m, await s.OnHandAsync(s.BinA));
    }

    [Fact]
    public async Task AC13_c_two_concurrent_releases_of_one_bin_exactly_one_wins_the_loser_keeps_nothing()
    {
        var s = new CycleCountScenario(factory);
        await s.ReceiveAsync(5, s.BinA);
        var one = await s.CreatedAsync(s.Form(scope: [s.BinA]));
        var two = await s.CreatedAsync(s.Form(scope: [s.Storage]));
        var barrier = new AsyncBarrier(2);
        var calls = 0;
        factory.AfterBalanceRead[s.Tenant] = () => Interlocked.Increment(ref calls) <= 2 ? barrier.SignalAndWaitAsync() : Task.CompletedTask;
        HttpResponseMessage[] answers;
        try
        {
            answers = await Task.WhenAll(s.ReleaseAsync(Id(one), ETag(one)), s.ReleaseAsync(Id(two), ETag(two)));
        }
        finally
        {
            factory.AfterBalanceRead.TryRemove(s.Tenant, out _);
        }

        Assert.Equal([200, 409], answers.Select(a => (int)a.StatusCode).Order());
        var loser = answers[0].StatusCode == System.Net.HttpStatusCode.OK ? Id(two) : Id(one);
        Assert.Equal("COUNT_SCOPE_OVERLAP", JsonDocument.Parse(await answers.Single(a => (int)a.StatusCode == 409).Content.ReadAsStringAsync())
            .RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Empty(s.Claims(loser));
        Assert.DoesNotContain(s.Stored(CycleCountCollections.Lines), l => l["CountId"].AsString == loser);
        Assert.Single(s.Events(loser)); // Created only
    }

    [Fact]
    public async Task AC13_d_a_cancelled_count_frees_its_bins()
    {
        var s = new CycleCountScenario(factory);
        await s.ReceiveAsync(5, s.BinA);
        var first = await s.ReleasedAsync(await s.CreatedAsync());
        await Expect(s.CancelAsync(Id(first), ETag(first)), 200);

        var second = await s.ReleasedAsync(await s.CreatedAsync());
        Assert.Equal("Released", second.GetProperty("status").GetString());
        Assert.All(s.Claims(Id(second)), c => Assert.True(c["Open"].AsBoolean));
    }

    // ── AC-52 (a) ────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A ledger posting injected BETWEEN the release's balance read and its movement-id read (the probe) is in neither read — the snapshot is one
    /// (pack §8.2 rule 4). A posting before the release, inside W, is in both. With separate reads the injected posting would be missing from the
    /// expected quantity but present in the id list.
    /// </summary>
    [Fact]
    public async Task AC52a_a_posting_during_the_release_is_in_both_reads_or_in_neither()
    {
        var s = new CycleCountScenario(factory);
        var before = MovementId(await s.ReceiveAsync(10, s.BinA));
        var count = await s.CreatedAsync();
        Guid? injected = null;
        var probeCalls = 0;
        factory.AfterBalanceRead[s.Tenant] = async () =>
        {
            Interlocked.Increment(ref probeCalls);
            if (injected is not null) return; // a transient retry of the release transaction re-reads; the posting happens once
            injected = MovementId(await s.ReceiveAsync(3, s.BinA));
        };
        try
        {
            await s.ReleasedAsync(count);
        }
        finally
        {
            factory.AfterBalanceRead.TryRemove(s.Tenant, out _);
        }

        Assert.NotNull(injected);
        var stored = s.StoredCount(Id(count));
        var ids = Ids(stored, "SnapshotTransactionIds");
        var expected = Dec(Assert.Single(await s.LinesAsync(Id(count))), "expectedQuantity");

        Assert.Equal(1, probeCalls);              // ONE snapshot transaction (the SKU flags were read before it opened)
        Assert.Contains(before, ids);
        Assert.Equal(10m, expected);              // the injected +3 is not in the expected quantity …
        Assert.DoesNotContain(injected.Value, ids); // … and not in the id list: counted once, later, as a movement since the snapshot (DL-2)
        Assert.Equal(13m, await s.OnHandAsync(s.BinA));
        var movement = s.Stored("inventory_transactions").Single(t => t["_id"].AsString == injected.Value.ToString("D"));
        Assert.True(movement["CreatedAt"].ToUniversalTime() >= stored["SnapshotAt"].ToUniversalTime().AddMinutes(-10)); // inside W: only the snapshot kept it out
    }
}
