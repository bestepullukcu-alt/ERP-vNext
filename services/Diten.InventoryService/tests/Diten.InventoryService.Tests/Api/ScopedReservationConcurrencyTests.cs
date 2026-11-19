using Diten.InventoryService.Persistence;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// MOD-0173-FU03 AC-9 — parallel scoped creates on one lot, each started together (<c>Task.WhenAll</c>). What is forced: 20 creates of 5
/// against 50 free at the LOT level only (W free 100, SKU free 150 — §17 fixture rule), all writing the same position documents, so
/// Mongo serialises them by WriteConflict and the losers re-run (<see cref="TransactionRetryCounter.Reservation"/> must move, else the test
/// proved nothing and is rewritten).
/// </summary>
public sealed class ScopedReservationConcurrencyTests : IClassFixture<TraceApiFactory>
{
    private readonly TraceApiFactory _factory;

    public ScopedReservationConcurrencyTests(TraceApiFactory factory) => _factory = factory;

    /// <summary>AC-9 (a, b) — S-4: the UnreservedAtLeast condition dropped at the LOT level (read-then-write) → more than 10 creates, red.</summary>
    [Fact]
    public async Task Parallel_lot_reservations_never_exceed_free()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        var lot2 = await w.LotAsync();
        await w.ReceiveAsync("50", lot);
        await w.ReceiveAsync("50", lot2);
        await w.ReceiveAsync("50", lot2, w.Ledger.LocationC);
        var retries = _factory.Services.GetRequiredService<TransactionRetryCounter>();
        var before = retries.Count(TransactionRetryCounter.Reservation);

        var answers = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            using var response = await w.Ledger.ReserveAsync(w.Reservation("5", w.W, lot), token: w.Lots.Token);
            return (int)response.StatusCode;
        }));

        Assert.Equal(10, answers.Count(a => a == 201));
        Assert.Equal(10, answers.Count(a => a == 422));
        Assert.Equal(50m, w.Scope(w.W, lot).Reserved);
        Assert.True(retries.Count(TransactionRetryCounter.Reservation) > before, "no reservation transaction re-ran: the race was not forced");
        w.AssertInvariant("AC-9 a");
    }

    /// <summary>AC-9 (c) — parallel creates and parallel issues of the same lot: no level ever shows AvailableOnHand − Reserved &lt; 0.</summary>
    [Fact]
    public async Task Parallel_creates_and_issues_keep_every_level_covered()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        var lot2 = await w.LotAsync();
        await w.ReceiveAsync("60", lot);
        await w.ReceiveAsync("50", lot2);
        await w.ReceiveAsync("50", lot2, w.Ledger.LocationC);

        await Task.WhenAll(Enumerable.Range(0, 24).Select(async i =>
        {
            using var response = i % 2 == 0
                ? await w.Ledger.ReserveAsync(w.Reservation("7", w.W, lot), token: w.Lots.Token)
                : await w.Ledger.PostAsync(w.Issue("6", lot), token: w.Lots.Token);
            Assert.True((int)response.StatusCode is 201 or 422, $"unexpected {(int)response.StatusCode}");
        }));

        foreach (var (onHand, reserved) in new[] { w.SkuPosition(), w.Scope(w.W), w.Scope(w.W, lot) })
        {
            Assert.True(onHand - reserved >= 0m, $"a level is over-reserved: {onHand} − {reserved}");
        }

        w.AssertInvariant("AC-9 c");
    }
}
