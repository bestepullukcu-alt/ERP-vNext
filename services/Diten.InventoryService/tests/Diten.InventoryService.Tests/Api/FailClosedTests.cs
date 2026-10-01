using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// AC16 / pack §13.8 — an unreachable upstream master is never a silent pass.
/// <para>MOD-0173-FU02 (AC-22): the location case keeps the pre-FU02 LOCATION stub (<see cref="StubLocationMasterApiFactory"/>)
/// — the in-process directory has no unreachable remote to switch off; its own failure path (a database read failure →
/// the same 503) is the same handler branch. The locations themselves are real documents (the in-transaction K-13
/// check still runs).</para>
/// </summary>
public sealed class FailClosedTests : IClassFixture<StubLocationMasterApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public FailClosedTests(StubLocationMasterApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Product_master_down_is_503_PRODUCT_MASTER_UNAVAILABLE_and_writes_nothing()
    {
        var s = new InventoryScenario(_factory);
        _factory.Products.MarkUnavailable(s.Item, s.Sku);

        var refused = await s.PostExpectingAsync(s.Receipt("1"), 503);

        Assert.Equal("PRODUCT_MASTER_UNAVAILABLE", refused.GetProperty("error").GetProperty("code").GetString());
        AssertNothingWritten(s);
    }

    [Fact]
    public async Task Location_master_down_is_503_LOCATION_MASTER_UNAVAILABLE_and_writes_nothing()
    {
        var s = new InventoryScenario(_factory);
        _factory.Locations.MarkUnavailable(s.LocationA);

        var refused = await s.PostExpectingAsync(s.Receipt("1"), 503);

        Assert.Equal("LOCATION_MASTER_UNAVAILABLE", refused.GetProperty("error").GetProperty("code").GetString());
        AssertNothingWritten(s);
    }

    [Fact]
    public async Task An_idempotent_replay_does_not_need_the_upstream_masters()
    {
        var s = new InventoryScenario(_factory);
        var body = s.Receipt("1");
        await s.PostExpectingAsync(body, 201);
        _factory.Products.MarkUnavailable(s.Item, s.Sku);
        _factory.Locations.MarkUnavailable(s.LocationA);

        var replay = await s.PostExpectingAsync(body, 200);

        Assert.True(replay.GetProperty("idempotentReplay").GetBoolean());
    }

    private static void AssertNothingWritten(InventoryScenario s)
    {
        Assert.Empty(s.Persisted("inventory_transactions"));
        Assert.Empty(s.Persisted("inventory_balances"));
        Assert.Empty(s.Persisted("inventory_audit"));
        Assert.Empty(s.Persisted("inventory_outbox"));
    }
}
