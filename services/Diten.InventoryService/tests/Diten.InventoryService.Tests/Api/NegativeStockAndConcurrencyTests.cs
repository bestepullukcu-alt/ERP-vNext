using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// AC5 / AC6 / pack §13.6-7 (DEC-INV-12: negative stock prohibited, no override in S1). GUARD UNDER TEST: the coverage
/// condition in InventoryLedgerRepository.DecreaseAsync, evaluated atomically inside the posting transaction.
/// Sabotage target of D4.
/// </summary>
public sealed class NegativeStockAndConcurrencyTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public NegativeStockAndConcurrencyTests(InventoryApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Issue_above_balance_is_422_INSUFFICIENT_STOCK_with_available_and_requested_and_writes_nothing()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("80"), 201);

        var refused = await s.PostExpectingAsync(s.Issue("100"), 422);

        var error = refused.GetProperty("error");
        Assert.Equal("INSUFFICIENT_STOCK", error.GetProperty("code").GetString());
        Assert.Equal("80.000", error.GetProperty("details").GetProperty("available").GetString());
        Assert.Equal("100.000", error.GetProperty("details").GetProperty("requested").GetString());

        Assert.Single(s.Persisted("inventory_transactions"));
        Assert.Single(s.Persisted("inventory_audit"));
        Assert.Single(s.Persisted("inventory_outbox"));
        Assert.Equal(80m, await s.OnHandAsync());
    }

    [Fact]
    public async Task Issue_from_a_row_that_does_not_exist_is_422_with_available_zero()
    {
        var s = new InventoryScenario(_factory);

        var refused = await s.PostExpectingAsync(s.Issue("1"), 422);

        Assert.Equal("0.000", refused.GetProperty("error").GetProperty("details").GetProperty("available").GetString());
        Assert.Empty(s.Persisted("inventory_transactions"));
        Assert.Empty(s.Persisted("inventory_balances"));
    }

    [Fact]
    public async Task Issue_of_exactly_the_balance_is_allowed_and_leaves_zero()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("100"), 201);

        await s.PostExpectingAsync(s.Issue("100"), 201);

        Assert.Equal(0m, await s.OnHandAsync());
    }

    [Fact]
    public async Task A_rejected_transfer_moves_nothing_on_either_side()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("10"), 201);
        var transfer = s.Movement("LOCATION_TRANSFER", "11");
        transfer["fromWarehouseId"] = s.Warehouse;
        transfer["fromLocationId"] = s.LocationA;
        transfer["toWarehouseId"] = s.Warehouse;
        transfer["toLocationId"] = s.LocationB;

        await s.PostExpectingAsync(transfer, 422);

        Assert.Equal(10m, await s.OnHandAsync(s.LocationA));
        Assert.Equal(0m, await s.OnHandAsync(s.LocationB));
        Assert.Single(s.Persisted("inventory_balances"));
    }

    [Fact]
    public async Task Two_parallel_issues_of_60_against_100_end_at_40_never_minus_20()
    {
        for (var round = 0; round < 5; round++)
        {
            var s = new InventoryScenario(_factory);
            await s.PostExpectingAsync(s.Receipt("100"), 201);

            var results = await Task.WhenAll(s.PostAsync(s.Issue("60")), s.PostAsync(s.Issue("60")));
            var statuses = results.Select(r => (int)r.StatusCode).OrderBy(x => x).ToArray();

            Assert.Equal(new[] { 201, 422 }, statuses);
            Assert.Equal(40m, await s.OnHandAsync());
            Assert.Equal(2, s.Persisted("inventory_transactions").Count);
            var row = Assert.Single(s.Persisted("inventory_balances"));
            Assert.Equal(40m, row["OnHand"].AsDecimal);
            Assert.Equal(40m, row["Available"].AsDecimal);
        }
    }

    [Fact]
    public async Task Many_parallel_issues_never_overdraw()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("100"), 201);

        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => s.PostAsync(s.Issue("10"))));

        Assert.Equal(10, results.Count(r => (int)r.StatusCode == 201));
        Assert.Equal(2, results.Count(r => (int)r.StatusCode == 422));
        Assert.Equal(0m, await s.OnHandAsync());
    }
}
