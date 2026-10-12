using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// AC-R9 (pack ASSUMPTION-0173-13): after a random mix of postings and reservation changes — incl. concurrent ones —
/// for every Tenant + LE + SKU: <c>Σ OnHand of AVAILABLE rows == AvailableOnHand</c> and
/// <c>Σ remaining of ACTIVE reservations == Reserved</c>; and the rebuild produces exactly the same position.
/// The oracle is computed independently from the raw documents, not through the service.
/// </summary>
public sealed class ReservationInvariantTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public ReservationInvariantTests(InventoryApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData(17)]
    [InlineData(4242)]
    [InlineData(90210)]
    public async Task Random_postings_and_reservations_keep_the_position_equal_to_its_sources_and_rebuild_agrees(int seed)
    {
        var random = new Random(seed);
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("500"), 201);
        var open = new List<(string Id, string Etag, string Doc)>();

        for (var step = 0; step < 40; step++)
        {
            var batch = Enumerable.Range(0, random.Next(1, 4)).Select(_ => Step(random, s, open)).ToList();
            await Task.WhenAll(batch); // concurrent steps on the same SKU
        }

        AssertInvariant(s);
        var before = Position(s);

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISkuPositionRebuilder>().RebuildAllAsync();
        }

        var after = Position(s);
        Assert.Equal(before["AvailableOnHand"].AsDecimal, after["AvailableOnHand"].AsDecimal);
        Assert.Equal(before["Reserved"].AsDecimal, after["Reserved"].AsDecimal);
        AssertInvariant(s);
    }

    [Fact]
    public async Task Rebuild_restores_a_corrupted_position_and_is_idempotent()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("100"), 201);
        await s.ReserveExpectingAsync(s.Reservation("25"), 201);
        var positions = _factory.Collection("inventory_sku_positions");
        var filter = new BsonDocument("TenantId", s.Tenant.ToString());
        await positions.UpdateOneAsync(filter, new BsonDocument("$set", new BsonDocument
        {
            { "AvailableOnHand", new BsonDecimal128(999m) }, { "Reserved", new BsonDecimal128(0m) }
        }));

        using (var scope = _factory.Services.CreateScope())
        {
            var rebuilder = scope.ServiceProvider.GetRequiredService<ISkuPositionRebuilder>();
            await rebuilder.RebuildAllAsync();
            await rebuilder.RebuildAllAsync();
        }

        var position = Position(s);
        Assert.Equal(100m, position["AvailableOnHand"].AsDecimal);
        Assert.Equal(25m, position["Reserved"].AsDecimal);
        Assert.Single(s.Persisted("inventory_sku_positions"));
    }

    [Fact]
    public async Task Rebuild_creates_the_position_of_a_ledger_that_predates_S2()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("40"), 201);
        await _factory.Collection("inventory_sku_positions").DeleteManyAsync(new BsonDocument("TenantId", s.Tenant.ToString()));
        Assert.Empty(s.Persisted("inventory_sku_positions"));

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISkuPositionRebuilder>().RebuildAllAsync();
        }

        Assert.Equal(40m, Position(s)["AvailableOnHand"].AsDecimal);
        await s.ReserveExpectingAsync(s.Reservation("40"), 201);
    }

    private static async Task Step(Random random, InventoryScenario s, List<(string Id, string Etag, string Doc)> open)
    {
        int choice;
        lock (random) choice = random.Next(7);
        var qty = (random.Next(1, 60)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        switch (choice)
        {
            case 0:
                using (await s.PostAsync(s.Receipt(qty))) { }
                break;
            case 1:
                using (await s.PostAsync(s.IssueFor(qty, "MOD-OTHER", "DO-" + Guid.NewGuid().ToString("N")[..6], "1"))) { }
                break;
            case 2:
            case 3:
            {
                var doc = "SO-" + Guid.NewGuid().ToString("N")[..6];
                using var response = await s.ReserveAsync(s.Reservation(qty, doc, "1"));
                if ((int)response.StatusCode == 201)
                {
                    var body = await s.ReadAsync(response);
                    lock (open) open.Add((body.GetProperty("reservationId").GetString()!, body.GetProperty("etag").GetString()!, doc));
                }

                break;
            }
            case 4:
            {
                (string Id, string Etag, string Doc) pick;
                lock (open)
                {
                    if (open.Count == 0) return;
                    pick = open[random.Next(open.Count)];
                    open.Remove(pick);
                }

                using (await s.ReleaseAsync(pick.Id, pick.Etag)) { }
                break;
            }
            case 5:
            {
                (string Id, string Etag, string Doc) pick;
                lock (open)
                {
                    if (open.Count == 0) return;
                    pick = open[random.Next(open.Count)];
                }

                using (await s.PostAsync(s.IssueFor(qty, "MOD-0172", pick.Doc, "1"))) { }
                break;
            }
            default:
            {
                var move = s.Movement("STATUS_TRANSFER", qty);
                move["fromWarehouseId"] = s.Warehouse;
                move["fromLocationId"] = s.LocationA;
                move["toWarehouseId"] = s.Warehouse;
                move["toLocationId"] = s.LocationA;
                move["toStockStatus"] = random.Next(2) == 0 ? "QUALITY_INSPECTION" : "AVAILABLE";
                if ((string)move["toStockStatus"]! == "AVAILABLE") move["fromStockStatus"] = "QUALITY_INSPECTION";
                using (await s.PostAsync(move)) { }
                break;
            }
        }
    }

    private static void AssertInvariant(InventoryScenario s)
    {
        var availableRows = s.Persisted("inventory_balances").Where(r => r["StockStatus"].AsString == "Available").Sum(r => r["OnHand"].AsDecimal);
        var activeReserved = s.Persisted("inventory_reservations").Where(r => r["Status"].AsString == "Active").Sum(r => r["RemainingQuantity"].AsDecimal);
        var position = Position(s);

        Assert.Equal(availableRows, position["AvailableOnHand"].AsDecimal);
        Assert.Equal(activeReserved, position["Reserved"].AsDecimal);
        Assert.True(position["AvailableOnHand"].AsDecimal - position["Reserved"].AsDecimal >= 0m, "reserved exceeds available on hand");
        Assert.All(s.Persisted("inventory_balances"), r => Assert.True(r["OnHand"].AsDecimal >= 0m));
    }

    private static BsonDocument Position(InventoryScenario s) => Assert.Single(s.Persisted("inventory_sku_positions"));
}
