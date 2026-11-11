using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Traceability.Repositories;
using Diten.InventoryService.Tests.TestInfrastructure;
using Diten.InventoryService.Tests.Trace;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// MOD-0173-FU03 AC-34 (§8.2 table, paths 3–5) — the other writers of the projection move the warehouse / lot positions too, each its own
/// fixture, each ending equal to the oracle and to the production rebuilder: (3) a lot status change and the lot expiry sweeper
/// (<c>LotHeldStock.MoveAsync</c>, CT-07), (4) the retest hold (same body), (5a) the typed-lot migration to a free lot, (5b) to a held lot.
/// Runs in the lot-expiry collection: the expiry sweeper reads every tenant.
/// </summary>
[Collection(LotExpiryCollection.Name)]
public sealed class LotReservationPositionPathTests : IClassFixture<TraceApiFactory>
{
    private readonly TraceApiFactory _factory;

    public LotReservationPositionPathTests(TraceApiFactory factory) => _factory = factory;

    /// <summary>(3) — sabotage: the scope update removed from <c>LotHeldStock.MoveAsync</c> → W: / L: keep the blocked stock, red.</summary>
    [Fact]
    public async Task Blocking_releasing_and_expiring_a_lot_move_its_scope_positions_in_every_warehouse()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        var other = await w.LotAsync();
        await w.Lots.ReleaseAsync(lot);
        await w.ReceiveAsync("30", lot);
        await w.ReceiveAsync("20", lot, w.Ledger.LocationC);
        await w.ReceiveAsync("10", other);

        await w.Lots.BlockAsync(lot);
        Assert.Equal((10m, 0m, 0m, 0m), (w.Scope(w.W).OnHand, w.Scope(w.W, lot).OnHand, w.Scope(w.W2).OnHand, w.Scope(w.W2, lot).OnHand));
        await AssertRebuildAgreesAsync(w, "blocked");

        await w.Lots.ReleaseAsync(lot);
        Assert.Equal((40m, 30m, 20m, 20m), (w.Scope(w.W).OnHand, w.Scope(w.W, lot).OnHand, w.Scope(w.W2).OnHand, w.Scope(w.W2, lot).OnHand));
        await AssertRebuildAgreesAsync(w, "released again");

        var dated = await w.Lots.CreateLotAsync(expiryDate: "2026-09-20", manufacturingDate: "2026-01-10");
        await w.Lots.ReleaseAsync(dated);
        await w.Lots.PostOkAsync(w.Lots.Receipt("8", dated, postingDate: "2026-09-01T08:00:00Z"));
        Assert.Equal((48m, 8m), (w.Scope(w.W).OnHand, w.Scope(w.W, dated).OnHand));
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ILotExpirySweeper>().SweepAsync(DateTimeOffset.UtcNow);
        }

        Assert.Equal((40m, 0m), (w.Scope(w.W).OnHand, w.Scope(w.W, dated).OnHand));
        await AssertRebuildAgreesAsync(w, "expired by the sweeper");
    }

    /// <summary>(4) — the same sabotage: the retest hold (same <c>MoveAsync</c> body) leaves W: / L: unchanged, red (its own run).</summary>
    [Fact]
    public async Task Retest_hold_moves_its_scope_positions()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.Lots.CreateLotAsync(expiryDate: "2028-01-31T00:00:00Z", retestDate: LotLedgerScenario.Midnight(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60)));
        await w.Lots.ReleaseAsync(lot);
        await w.ReceiveAsync("12", lot);
        await w.ReceiveAsync("5", await w.LotAsync());

        await w.Lots.SetRetestDateAsync(lot, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3));
        Assert.Equal(1, (await w.Lots.SweepRetestAsync()).Held);

        Assert.Equal((5m, 0m), (w.Scope(w.W).OnHand, w.Scope(w.W, lot).OnHand));
        await AssertRebuildAgreesAsync(w, "retest held");
    }

    /// <summary>(5a) — sabotage: the free-target branch of the re-key adds nothing to L: → the lot position stays empty, red.</summary>
    [Fact]
    public async Task Migration_to_a_free_lot_adds_the_moved_stock_to_its_lot_position()
    {
        var m = new MigrationScenario(_factory);
        var w = Wrap(m);
        var lot = await m.CreateLotAsync("MG-1");
        await m.ReceiveTypedAsync("MG-1", "6");
        await m.ReceiveTypedAsync("MG-1", "4", m.Ledger.LocationC);
        var warehouseBefore = (w.Scope(w.W), w.Scope(w.W2));

        var groups = await m.GroupsAsync();
        await m.CommitAsync(MigrationScenario.ApplySuggestions(groups), groups.Count, 200);

        Assert.Equal((6m, 4m), (w.Scope(w.W, lot).OnHand, w.Scope(w.W2, lot).OnHand));
        Assert.Equal(warehouseBefore, (w.Scope(w.W), w.Scope(w.W2)));
        await AssertRebuildAgreesAsync(w, "migrated to a free lot");
    }

    /// <summary>(5b) — S-29: the held-target branch drops nothing from W: (or adds the held lot to L:) → red.</summary>
    [Fact]
    public async Task Migration_to_held_lot_moves_scope_positions()
    {
        var m = new MigrationScenario(_factory);
        var w = Wrap(m);
        var lot = await m.CreateLotAsync("MB-1");
        await m.Lots.ReleaseAsync(lot);
        await m.Lots.BlockAsync(lot);
        await m.ReceiveTypedAsync("MB-1", "4");
        await m.ReceiveTypedAsync("OTHER-1", "6");
        await w.ReserveOkAsync("5", w.W);
        Assert.Equal((10m, 5m), w.Scope(w.W));

        var groups = await m.GroupsAsync();
        await m.CommitAsync(MigrationScenario.ApplySuggestions(groups), groups.Count, 200);

        Assert.Equal((6m, 5m), w.Scope(w.W));
        Assert.Equal((0m, 0m), w.Scope(w.W, lot));
        Assert.Equal(6m, w.SkuPosition().OnHand);
        await AssertRebuildAgreesAsync(w, "migrated to a held lot");
    }

    private ScopedReservationWorld Wrap(MigrationScenario m) => ScopedReservationWorld.Of(m.Lots);

    /// <summary>The live numbers equal the independent oracle, and the production rebuilder writes the same numbers.</summary>
    private async Task AssertRebuildAgreesAsync(ScopedReservationWorld w, string because)
    {
        w.AssertInvariant(because + " (live)");
        var live = Levels(w);
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISkuPositionRebuilder>().RebuildAllAsync();
        }

        Assert.Equal(live, Levels(w));
        w.AssertInvariant(because + " (rebuilt)");
    }

    private static List<string> Levels(ScopedReservationWorld w)
        => w.Persisted("inventory_reservation_positions").Concat(w.Persisted("inventory_sku_positions"))
            .Select(d => $"{d.GetValue("Level", "Sku")}|{d.GetValue("WarehouseId", "")}|{d.GetValue("LotId", "")}|{d["AvailableOnHand"].ToDecimal()}|{d["Reserved"].ToDecimal()}")
            .OrderBy(x => x, StringComparer.Ordinal).ToList();
}
