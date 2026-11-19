using Diten.InventoryService.Persistence;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// MOD-0173-FU03 AC-27 — two writers of the SAME reservation started together (<c>Task.WhenAll</c>): the named consumption reads the
/// reservation inside its posting transaction and writes it conditionally on that version, so the two meet as a WriteConflict and the loser
/// re-runs on the winner's commit (the <see cref="TransactionRetryCounter.Posting"/> / <see cref="TransactionRetryCounter.Reservation"/>
/// counters must move — else nothing was forced and the test is rewritten). Each case is repeated: a race that is not provoked is not measured.
/// </summary>
public sealed class ReservationConsumptionConcurrencyTests : IClassFixture<TraceApiFactory>
{
    private readonly TraceApiFactory _factory;

    public ReservationConsumptionConcurrencyTests(TraceApiFactory factory) => _factory = factory;

    private long Retries()
    {
        var counter = _factory.Services.GetRequiredService<TransactionRetryCounter>();
        return counter.Count(TransactionRetryCounter.Posting) + counter.Count(TransactionRetryCounter.Reservation);
    }

    private async Task<(ScopedReservationWorld W, string Lot, string R)> FixtureAsync()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("50", lot);
        var r = await w.ReserveOkAsync("30", w.W, lot);
        return (w, lot, r);
    }

    /// <summary>(a) — S-19: the remaining quantity read outside the transaction and decremented unconditionally → both 201, red.</summary>
    [Fact]
    public async Task Two_parallel_partial_consumptions_one_wins()
    {
        var before = Retries();
        for (var round = 0; round < 3; round++)
        {
            var (w, lot, r) = await FixtureAsync();
            var answers = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
            {
                using var response = await w.Ledger.PostAsync(ScopedReservationWorld.Naming(w.Issue("20", lot), r), token: w.Lots.Token);
                var json = await w.Ledger.ReadAsync(response);
                return ((int)response.StatusCode, (int)response.StatusCode == 422 ? ScopedReservationWorld.Code(json) : null);
            }));

            Assert.Equal(1, answers.Count(a => a.Item1 == 201));
            Assert.Equal(1, answers.Count(a => a.Item1 == 422 && a.Item2 == "RESERVATION_QUANTITY_EXCEEDED"));
            Assert.Equal(1, w.Count("inventory_transactions", d => d.GetValue("MovementType", "").AsString == "GoodsIssueSales"));
            Assert.Equal(1, w.Count("inventory_audit", d => d.GetValue("Action", "").AsString == "InventoryReservationPartiallyConsumed"));
            Assert.Equal(10m, w.ReservationDocument(r)["RemainingQuantity"].ToDecimal());
            w.AssertInvariant($"AC-27 a round {round}");
        }

        Assert.True(Retries() > before, "no posting / reservation transaction re-ran: the race was not forced");
    }

    /// <summary>(b) — a release and a full consumption of the same reservation in parallel: exactly one of them takes it.</summary>
    [Fact]
    public async Task Release_and_consumption_race_one_wins()
    {
        for (var round = 0; round < 3; round++)
        {
            var (w, lot, r) = await FixtureAsync();
            var consume = Task.Run(async () =>
            {
                using var response = await w.Ledger.PostAsync(ScopedReservationWorld.Naming(w.Issue("30", lot), r), token: w.Lots.Token);
                var json = await w.Ledger.ReadAsync(response);
                return ((int)response.StatusCode, (int)response.StatusCode == 422 ? ScopedReservationWorld.Code(json) : null);
            });
            var release = Task.Run(async () =>
            {
                using var response = await w.Ledger.ReleaseAsync(r, "W/\"1\"", w.Lots.Token);
                return (int)response.StatusCode;
            });
            await Task.WhenAll(consume, release);
            var consumeAnswer = await consume;
            var releaseAnswer = await release;

            var consumed = consumeAnswer.Item1 == 201;
            var released = releaseAnswer == 200;
            Assert.True(consumed ^ released, $"consume {consumeAnswer.Item1} / release {releaseAnswer}");
            if (consumed) Assert.Equal(409, releaseAnswer);
            else Assert.Equal((422, "RESERVATION_NOT_ACTIVE"), consumeAnswer);
            w.AssertInvariant($"AC-27 b round {round}");
        }
    }
}
