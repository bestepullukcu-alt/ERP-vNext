using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed class AcceptedHistoryForecastEvaluationTests
{
    private static readonly DateOnly FirstWeek = new(2023, 10, 9);

    [Fact]
    public async Task TwentySixVerifiedWeeks_AllowGeneralMethodsWithoutInventingBacktest()
    {
        var cycle = Cycle();
        var reader = new Reader(History(cycle, 26));
        var rows = await new AcceptedHistoryForecastEvaluation(reader)
            .EvaluateAsync(cycle, reader.History.SkuId, "Gsku", "WH-1", default);
        Assert.Equal(156, reader.RequestedWeeks);
        Assert.Equal(cycle.HorizonStart, reader.RequestedAsOf);
        Assert.Equal(7, rows.Count);
        var naive = Assert.Single(rows, row => row.View.Method == "Naive");
        Assert.True(naive.View.Eligible);
        Assert.Equal("EligibleBacktestUnavailable", naive.View.Reason);
        Assert.Null(naive.View.WapeRatio);
        Assert.Equal(52, naive.Result!.Forecast52.Count);
        Assert.All(naive.Result.Forecast52, value => Assert.Equal(5m, value));
        Assert.False(Assert.Single(rows, row => row.View.Method == "HoltWinters")
            .View.Eligible);
    }

    [Fact]
    public async Task UnknownLatestWeek_IsNeverConvertedToZeroOrUsedAsTraining()
    {
        var cycle = Cycle();
        var history = History(cycle, 26);
        var weeks = history.Weeks.ToArray();
        weeks[^1] = weeks[^1] with
        {
            State = AcceptedWeekState.Unknown,
            UomId = null,
            Events = []
        };
        var rows = await new AcceptedHistoryForecastEvaluation(
                new Reader(history with { Weeks = weeks }))
            .EvaluateAsync(cycle, history.SkuId, "Gsku", "WH-1", default);
        Assert.All(rows, row => Assert.False(row.View.Eligible));
    }

    [Fact]
    public async Task SameOriginalUnitPendingConversion_RemainsVisibleAndEligible()
    {
        var cycle = Cycle();
        var history = History(cycle, 26);
        var weeks = history.Weeks.ToArray();
        weeks[^1] = weeks[^1] with { UnitVerificationPending = true };
        var rows = await new AcceptedHistoryForecastEvaluation(
                new Reader(history with { Weeks = weeks }))
            .EvaluateAsync(cycle, history.SkuId, "Gsku", "WH-1", default);
        var naive = Assert.Single(rows, row => row.View.Method == "Naive");
        Assert.True(naive.View.Eligible);
        Assert.True(naive.View.UnitVerificationPending);
        Assert.Equal("EA", naive.View.ForecastUomId);
    }

    [Fact]
    public async Task DifferentAcceptedUnits_AreNeverSilentlyTrimmedOrCombined()
    {
        var cycle = Cycle();
        var history = History(cycle, 52);
        var weeks = history.Weeks.ToArray();
        weeks[^30] = weeks[^30] with { UomId = "CASE" };
        var rows = await new AcceptedHistoryForecastEvaluation(
                new Reader(history with { Weeks = weeks }))
            .EvaluateAsync(cycle, history.SkuId, "Gsku", "WH-1", default);
        Assert.All(rows, row =>
        {
            Assert.False(row.View.Eligible);
            Assert.Equal("MixedOrMissingUnit", row.View.Reason);
        });
    }

    [Fact]
    public async Task CrossTenantHistory_IsRejectedBeforeForecast()
    {
        var cycle = Cycle();
        var history = History(cycle, 156) with { TenantId = Guid.NewGuid() };
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new AcceptedHistoryForecastEvaluation(new Reader(history))
                .EvaluateAsync(cycle, history.SkuId, "Gsku", "WH-1", default));
    }

    private static PlanningCycle Cycle()
    {
        var start = FirstWeek.AddDays(156 * 7);
        return new PlanningCycle
        {
            Id = Guid.NewGuid(), TenantId = Guid.NewGuid(),
            LegalEntityId = Guid.NewGuid(), HorizonStart = start,
            HorizonEnd = start.AddDays(363),
            Weeks = Enumerable.Range(0, 52).Select(i => new PlanningWeek
            {
                Number = i + 1, WeekStart = start.AddDays(7 * i),
                WeekEnd = start.AddDays(7 * i + 6)
            }).ToList()
        };
    }

    private static AcceptedSeriesHistory History(PlanningCycle cycle, int reliable)
    {
        var sku = Guid.NewGuid();
        var weeks = Enumerable.Range(0, 156).Select(i =>
        {
            var known = i >= 156 - reliable;
            return new AcceptedWeekObservation(FirstWeek.AddDays(i * 7),
                known ? AcceptedWeekState.VerifiedDemand : AcceptedWeekState.Unknown,
                known ? "EA" : null, false, null,
                known ? [new AcceptedWeekEvent(Guid.NewGuid(),
                    FirstWeek.AddDays(i * 7), 5m)] : []);
        }).ToArray();
        return new AcceptedSeriesHistory(cycle.TenantId, cycle.LegalEntityId,
            sku, "Gsku", "WH-1", cycle.HorizonStart, weeks);
    }

    private sealed class Reader(AcceptedSeriesHistory history)
        : IAcceptedWeeklyHistoryReader
    {
        public AcceptedSeriesHistory History { get; } = history;
        public int RequestedWeeks { get; private set; }
        public DateOnly RequestedAsOf { get; private set; }

        public Task<AcceptedSeriesHistory> ReadAsync(Guid tenantId,
            Guid legalEntityId, Guid skuId, string skuLevel, string warehouseId,
            DateOnly asOfWeekStart, int weekCount, CancellationToken cancellationToken)
        {
            RequestedWeeks = weekCount;
            RequestedAsOf = asOfWeekStart;
            return Task.FromResult(History);
        }
    }
}
