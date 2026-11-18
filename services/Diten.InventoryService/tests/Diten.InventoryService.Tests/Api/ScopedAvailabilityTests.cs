using System.Text.Json;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// MOD-0173-FU03 AC-17 (§8.8, INVENTORY 1.6.0 getAvailability) — the scope figures for one named SKU with a warehouse (and a lot): read from
/// the reservations with the vade filter, the on-hand from the scope position. Fixture (F-4): W holds L 50 (lot reservation 20) and L2 30,
/// so the warehouse and the lot figures differ. <c>reserved</c> / <c>available</c> keep their 1.5.0 meaning (SKU total, floored in a scope).
/// </summary>
public sealed class ScopedAvailabilityTests : IClassFixture<TraceApiFactory>
{
    private readonly TraceApiFactory _factory;

    public ScopedAvailabilityTests(TraceApiFactory factory) => _factory = factory;

    private static async Task<(ScopedReservationWorld World, string Lot)> FixtureAsync(TraceApiFactory factory)
    {
        var w = new ScopedReservationWorld(factory);
        var lot = await w.LotAsync();
        var lot2 = await w.LotAsync();
        await w.ReceiveAsync("50", lot);
        await w.ReceiveAsync("30", lot2);
        await w.ReserveOkAsync("20", w.W, lot);
        return (w, lot);
    }

    private static string Sku(ScopedReservationWorld w) => $"itemId={w.Ledger.Item}&skuId={w.Ledger.Sku}&skuLevel=Lsku";

    private static async Task<JsonElement> GetAsync(ScopedReservationWorld w, string query, int expected = 200)
    {
        using var response = await w.Ledger.GetAsync("/api/inventory/availability?" + query, w.Lots.Token);
        var json = await w.Ledger.ReadAsync(response);
        Assert.True((int)response.StatusCode == expected, $"expected {expected}, got {(int)response.StatusCode}: {json}");
        return json;
    }

    private static (string?, string?) Figures(JsonElement json)
        => (json.TryGetProperty("scopeReserved", out var r) ? r.GetString() : null, json.TryGetProperty("scopeAvailable", out var a) ? a.GetString() : null);

    /// <summary>S-10: lotId ignored, the figures read from the warehouse scope → the lot figures come out as the warehouse's, red.</summary>
    [Fact]
    public async Task Lot_scope_figures_equal_L_position()
    {
        var (w, lot) = await FixtureAsync(_factory);

        var lotScope = await GetAsync(w, $"{Sku(w)}&warehouseId={w.W}&lotId={lot}");
        Assert.Equal(("20.000", "30.000"), Figures(lotScope));
        Assert.Equal((50m, 20m), w.Scope(w.W, lot));

        var warehouse = await GetAsync(w, $"{Sku(w)}&warehouseId={w.W}");
        Assert.Equal(("20.000", "60.000"), Figures(warehouse));

        // the 1.5.0 members keep their meaning: SKU-level reserved; available = AVAILABLE on hand in scope − reserved
        Assert.Equal(("20.000", "60.000"), (warehouse.GetProperty("reserved").GetString(), warehouse.GetProperty("available").GetString()));
        Assert.Equal(("20.000", "60.000"), (lotScope.GetProperty("reserved").GetString(), lotScope.GetProperty("available").GetString()));
        var plain = await GetAsync(w, Sku(w));
        Assert.Equal((null, null), Figures(plain));
        Assert.Equal(("20.000", "60.000"), (plain.GetProperty("reserved").GetString(), plain.GetProperty("available").GetString()));
    }

    [Fact]
    public async Task Scope_figures_need_one_named_sku_and_lotId_has_its_rules()
    {
        var (w, lot) = await FixtureAsync(_factory);

        Assert.Equal((null, null), Figures(await GetAsync(w, $"skuId={w.Ledger.Sku}&warehouseId={w.W}")));
        await GetAsync(w, $"skuId={w.Ledger.Sku}&warehouseId={w.W}&lotId={lot}", 400);
        await GetAsync(w, $"{Sku(w)}&lotId={lot}", 400);
        await GetAsync(w, $"{Sku(w)}&warehouseId={w.W}&locationId={w.Ledger.LocationA}&lotId={lot}", 400);
        await GetAsync(w, $"{Sku(w)}&warehouseId={w.W}&lotId=not-a-uuid", 400);
    }

    /// <summary>
    /// AC-17 (e) — S-39: the scope read counts Status ACTIVE only (or the position's Reserved) → after the wait the lot scope still says 30,
    /// red. The due reservation is created LAST (every create / release / movement of the SKU closes due ones) and nothing touches the SKU after.
    /// </summary>
    [Fact]
    public async Task Due_reservation_is_not_counted_in_scope_figures()
    {
        var (w, lot) = await FixtureAsync(_factory);
        var before = Figures(await GetAsync(w, $"{Sku(w)}&warehouseId={w.W}&lotId={lot}"));
        var due = await w.ReserveOkAsync("10", w.W, lot, expiresAt: ScopedReservationWorld.Later(TimeSpan.FromSeconds(2)));
        Assert.Equal("30.000", Figures(await GetAsync(w, $"{Sku(w)}&warehouseId={w.W}&lotId={lot}")).Item1);

        await Task.Delay(TimeSpan.FromSeconds(2.5));

        Assert.Equal("Active", w.ReservationDocument(due)["Status"].AsString);
        Assert.Equal(30m, w.Scope(w.W, lot).Reserved);
        var lotScope = await GetAsync(w, $"{Sku(w)}&warehouseId={w.W}&lotId={lot}");
        Assert.Equal(before, Figures(lotScope));
        Assert.Equal("20.000", lotScope.GetProperty("reserved").GetString());
        Assert.Equal("20.000", Figures(await GetAsync(w, $"{Sku(w)}&warehouseId={w.W}")).Item1);
        Assert.Equal(30m, await new PortWorld(_factory, w).Use(p => p.GetScopeFreeAsync(w.Ledger.Item, w.Ledger.Sku, w.Ledger.Level, w.W, Guid.Parse(lot))));
    }
}
