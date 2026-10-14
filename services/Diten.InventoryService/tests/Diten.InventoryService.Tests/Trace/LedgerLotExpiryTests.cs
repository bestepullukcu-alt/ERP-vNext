using Diten.InventoryService.Domain.Traceability.Repositories;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.InventoryService.Tests.Trace;

/// <summary>
/// The test classes that create lots with a PAST expiry date or drive the expiry sweeper, which reads every tenant: run
/// one after the other, so no sweeper of one class expires another class's lot in the middle of a test.
/// </summary>
[CollectionDefinition(Name)]
public sealed class LotExpiryCollection
{
    public const string Name = "MOD-0174 lot expiry (sweeper reads every tenant)";
}

/// <summary>
/// WP-0174-P2 — <c>LOT_EXPIRED</c> by DATE before any sweeper ran (pack §8 B-3, §13 #7), CT-D4 disposal out of an expired
/// lot, and CT-D1 (a) for the system expiry: the sweeper takes the lot's AVAILABLE stock out of availability in its own
/// transaction; balance rows keep their stock status.
/// </summary>
[Collection(LotExpiryCollection.Name)]
public sealed class LedgerLotExpiryTests : IClassFixture<TraceApiFactory>
{
    private readonly TraceApiFactory _factory;

    public LedgerLotExpiryTests(TraceApiFactory factory) => _factory = factory;

    [Fact]
    public async Task A_lot_expired_by_date_is_LOT_EXPIRED_before_the_sweeper_and_SCRAP_WRITE_OFF_still_pass()
    {
        var s = new LotLedgerScenario(_factory);
        // Expiry 2026-09-15; stock received 2026-09-01; the movements are posted 2026-09-30 (the scenario date).
        var lot = await s.CreateLotAsync(expiryDate: "2026-09-15", manufacturingDate: "2026-01-10");
        await s.ReleaseAsync(lot);
        await s.PostOkAsync(s.Receipt("6", lot, postingDate: "2026-09-01T08:00:00Z"));
        Assert.Equal("Released", (await s.GetTraceAsync($"/api/trace/lots/{lot}")).GetProperty("status").GetString());

        await s.RefusedAsync(s.Issue("1", lot), "LOT_EXPIRED");
        await s.RefusedAsync(s.Receipt("1", lot), "LOT_EXPIRED");
        await s.RefusedAsync(s.Transfer("1", lot), "LOT_EXPIRED");
        await s.PostOkAsync(s.Issue("1", lot, movementType: "SCRAP"));
        await s.PostOkAsync(s.Issue("1", lot, movementType: "WRITE_OFF"));
        await s.PostOkAsync(s.Issue("1", lot, postingDate: "2026-09-15T18:00:00Z")); // the expiry day itself is not expired
        Assert.Equal(3m, s.BalanceOnHandOfLot(lot));
        Assert.Equal(3m, s.LotPositionOnHand(lot));
    }

    [Fact]
    public async Task The_sweeper_expiry_takes_the_lot_out_of_availability_and_keeps_the_rows_stock_status()
    {
        var s = new LotLedgerScenario(_factory);
        var lot = await s.CreateLotAsync(expiryDate: "2026-09-20", manufacturingDate: "2026-01-10");
        var other = await s.CreateLotAsync();
        await s.ReleaseAsync(lot);
        await s.PostOkAsync(s.Receipt("8", lot, postingDate: "2026-09-01T08:00:00Z"));
        await s.PostOkAsync(s.Receipt("5", other));
        var statuses = s.Persisted("inventory_balances").Select(b => b["StockStatus"].AsString).ToList();
        Assert.Equal(13m, await s.AvailableAsync());

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ILotExpirySweeper>().SweepAsync(DateTimeOffset.UtcNow);
        }

        Assert.Equal("Expired", (await s.GetTraceAsync($"/api/trace/lots/{lot}")).GetProperty("status").GetString());
        Assert.Equal(5m, await s.AvailableAsync());
        Assert.Equal(s.ExpectedSkuAvailableOnHand(), s.SkuPositionAvailableOnHand());
        Assert.Equal(5m, s.SkuPositionAvailableOnHand());
        Assert.Equal(statuses, s.Persisted("inventory_balances").Select(b => b["StockStatus"].AsString).ToList());
        await s.ReserveAsync("6", 422); // only 5 left to reserve
        await s.ReserveAsync("5", 201);
    }
}
