using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// MOD-0173-FU03 AC-11 (§8.2 "Yeniden kurma") — after random postings and scoped reservation changes (concurrent ones included), on EVERY
/// level: Σ AVAILABLE rows == AvailableOnHand and Σ ACTIVE containing remaining == Reserved, measured by the independent oracle of
/// <see cref="ScopedReservationWorld.AssertInvariant"/>; the production rebuilder writes the same numbers, repairs a corrupted lot position and
/// builds the scope positions of a ledger that predates FU03. Sabotage: the scope positions taken out of the rebuilder → red.
/// </summary>
public sealed class ReservationScopeInvariantTests : IClassFixture<TraceApiFactory>
{
    private readonly TraceApiFactory _factory;

    public ReservationScopeInvariantTests(TraceApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData(17)]
    [InlineData(4242)]
    [InlineData(90210)]
    public async Task Random_scoped_postings_and_reservations_keep_every_level_equal_to_its_sources(int seed)
    {
        var random = new Random(seed);
        var w = new ScopedReservationWorld(_factory);
        string[] lots = [await w.LotAsync(), await w.LotAsync()];
        foreach (var lot in lots)
        {
            await w.ReceiveAsync("200", lot);
            await w.ReceiveAsync("100", lot, w.Ledger.LocationC);
        }

        var open = new List<(string Id, string Doc)>();
        for (var step = 0; step < 30; step++)
        {
            var batch = Enumerable.Range(0, random.Next(1, 4)).Select(_ => StepAsync(random, w, lots, open)).ToList();
            await Task.WhenAll(batch);
        }

        w.AssertInvariant($"seed {seed} live");
        var live = Levels(w, lots);
        await RebuildAsync();
        Assert.Equal(live, Levels(w, lots));
        w.AssertInvariant($"seed {seed} rebuilt");
    }

    [Fact]
    public async Task Rebuild_restores_a_corrupted_lot_position_and_is_idempotent()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("100", lot);
        await w.ReserveOkAsync("25", w.W, lot);
        await _factory.Collection("inventory_reservation_positions").UpdateOneAsync(
            new BsonDocument { { "TenantId", w.Ledger.Tenant.ToString() }, { "Level", "Lot" } },
            new BsonDocument("$set", new BsonDocument { { "AvailableOnHand", new BsonDecimal128(999m) }, { "Reserved", new BsonDecimal128(0m) } }));

        await RebuildAsync();
        var once = Levels(w, [lot]);
        await RebuildAsync();

        Assert.Equal((100m, 25m), w.Scope(w.W, lot));
        Assert.Equal(once, Levels(w, [lot]));
        w.AssertInvariant("rebuilt twice");
    }

    [Fact]
    public async Task Rebuild_creates_the_scope_positions_of_a_ledger_that_predates_FU03()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("40", lot);
        await w.ReceiveAsync("15", lot, w.Ledger.LocationC);
        await _factory.Collection("inventory_reservation_positions").DeleteManyAsync(new BsonDocument("TenantId", w.Ledger.Tenant.ToString()));
        Assert.Empty(w.Persisted("inventory_reservation_positions"));

        await RebuildAsync();

        Assert.Equal((40m, 0m), w.Scope(w.W, lot));
        Assert.Equal((40m, 0m), w.Scope(w.W));
        Assert.Equal((15m, 0m), w.Scope(w.W2, lot));
        await w.ReserveOkAsync("40", w.W, lot);
        w.AssertInvariant("predates FU03");
    }

    private async Task RebuildAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISkuPositionRebuilder>().RebuildAllAsync();
    }

    private static List<(decimal, decimal)> Levels(ScopedReservationWorld w, IEnumerable<string> lots)
        => new[] { w.SkuPosition(), w.Scope(w.W), w.Scope(w.W2) }
            .Concat(lots.SelectMany(l => new[] { w.Scope(w.W, l), w.Scope(w.W2, l) })).ToList();

    private static async Task StepAsync(Random random, ScopedReservationWorld w, string[] lots, List<(string Id, string Doc)> open)
    {
        int choice, pick;
        string qty;
        lock (random)
        {
            choice = random.Next(7);
            pick = random.Next(lots.Length);
            qty = random.Next(1, 40).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var lot = lots[pick];
        var token = w.Lots.Token;
        switch (choice)
        {
            case 0:
                using (await w.Ledger.PostAsync(w.Lots.Receipt(qty, lot), token: token)) { }
                break;
            case 1:
                using (await w.Ledger.PostAsync(w.Issue(qty, lot), token: token)) { }
                break;
            case 2:
            {
                var doc = "OB-" + Guid.NewGuid().ToString("N")[..6];
                using var response = await w.Ledger.ReserveAsync(w.Reservation(qty, w.W, lot, sourceModule: "MOD-0183", sourceDocumentId: doc, sourceLineId: "1"), token: token);
                if ((int)response.StatusCode == 201)
                {
                    var id = (await w.Ledger.ReadAsync(response)).GetProperty("reservationId").GetString()!;
                    lock (open) open.Add((id, doc));
                }

                break;
            }
            case 3:
                using (await w.Ledger.ReserveAsync(w.Reservation(qty, w.W), token: token)) { }
                break;
            case 4:
            {
                (string Id, string Doc) target;
                lock (open)
                {
                    if (open.Count == 0) return;
                    target = open[random.Next(open.Count)];
                    open.Remove(target);
                }

                using (await w.Ledger.ReleaseAsync(target.Id, "W/\"1\"", token)) { }
                break;
            }
            case 5:
            {
                (string Id, string Doc) target;
                lock (open)
                {
                    if (open.Count == 0) return;
                    target = open[random.Next(open.Count)];
                }

                using (await w.Ledger.PostAsync(w.Issue(qty, lot, sourceModule: "MOD-0183", sourceDocumentId: target.Doc, sourceLineId: "1"), token: token)) { }
                break;
            }
            default:
                using (await w.Ledger.PostAsync(w.WarehouseTransfer(qty, lot), token: token)) { }
                break;
        }
    }
}
