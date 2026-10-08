using Diten.PlanningService.Application.Features.DemandPlanning;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed class ForecastMethodComparisonTests
{
    private static readonly DateOnly Start = new(2026, 1, 5);

    [Fact]
    public void SideBySide_PreservesInputOrderWhenAggregateAndDistanceFavorDifferentCandidates()
    {
        var series = Weeks(29, index => index < 26
            ? index % 2 == 0 ? 9m : 11m
            : 10m);
        var candidateA = RollingOriginEvaluator.Evaluate(series, 26, 2,
            (training, _) => training.Count == 26 ? [10m, 10m] : [10m, 0m]);
        var candidateB = RollingOriginEvaluator.Evaluate(series, 26, 2,
            (_, _) => [7m, 7m]);

        var comparison = ForecastMethodComparison.SideBySide(
        [
            new("B", candidateB),
            new("A", candidateA)
        ]);

        Assert.Equal(["B", "A"], comparison.Select(row => row.MethodName));
        Assert.Equal(0.30m, comparison[0].WapeRatio);
        Assert.Equal(0.25m, comparison[1].WapeRatio);
        Assert.Equal(3m, comparison[0].Mae);
        Assert.Equal(2.5m, comparison[1].Mae);
        Assert.Equal(3m, comparison[0].Bias);
        Assert.Equal(2.5m, comparison[1].Bias);
        Assert.Equal(candidateA.Mase, comparison[1].Mase);
        Assert.Equal(candidateA.Errors[1].WeekStart, candidateA.Errors[2].WeekStart);

        Assert.Equal([1, 2], comparison[0].ByWeeksAhead.Select(row => row.WeeksAhead));
        Assert.Equal(0.30m, comparison[0].ByWeeksAhead[0].WapeRatio);
        Assert.Equal(0.30m, comparison[0].ByWeeksAhead[1].WapeRatio);
        Assert.Equal(0m, comparison[1].ByWeeksAhead[0].WapeRatio);
        Assert.Equal(0.50m, comparison[1].ByWeeksAhead[1].WapeRatio);
        Assert.Equal(candidateA.ByWeeksAhead[1].Mase, comparison[1].ByWeeksAhead[1].Mase);

        Assert.Null(typeof(ForecastMethodComparison.CandidateMetrics).GetProperty("Winner"));
        Assert.Null(typeof(ForecastMethodComparison.CandidateMetrics).GetProperty("Rank"));
        Assert.Null(typeof(ForecastMethodComparison.CandidateMetrics).GetProperty("Selected"));
    }

    [Fact]
    public void SideBySide_LeavesZeroDenominatorWapeUndefinedWithoutFallback()
    {
        var series = Weeks(27, index => index == 26 ? 0m : index % 2 == 0 ? 9m : 11m);
        var exact = RollingOriginEvaluator.Evaluate(series, 26, 1, (_, _) => [0m]);
        var over = RollingOriginEvaluator.Evaluate(series, 26, 1, (_, _) => [2m]);

        var comparison = ForecastMethodComparison.SideBySide(
        [
            new("exact", exact),
            new("over", over)
        ]);

        Assert.Equal(["exact", "over"], comparison.Select(row => row.MethodName));
        Assert.All(comparison, row =>
        {
            Assert.True(row.IsWapeUndefined);
            Assert.Null(row.WapeRatio);
            Assert.True(row.ByWeeksAhead[0].IsWapeUndefined);
            Assert.Null(row.ByWeeksAhead[0].WapeRatio);
        });
        Assert.Equal(0m, comparison[0].Mae);
        Assert.Equal(2m, comparison[1].Mae);
        Assert.Equal(-2m, comparison[1].Bias);
    }

    [Theory]
    [InlineData(RollingOriginEvaluator.WeekState.Missing)]
    [InlineData(RollingOriginEvaluator.WeekState.Unknown)]
    public void SideBySide_DoesNotTurnIncompleteWeekIntoObservedZero(
        RollingOriginEvaluator.WeekState state)
    {
        var series = Weeks(27, _ => 0m);
        series[^1] = series[^1] with { State = state, Quantity = null };
        var evaluation = RollingOriginEvaluator.Evaluate(series, 26, 1,
            (_, _) => throw new InvalidOperationException("Forecast must not run."));

        var row = Assert.Single(ForecastMethodComparison.SideBySide(
            [new("fixture", evaluation)]));

        Assert.Equal(RollingOriginEvaluator.EvaluationState.IncompleteSeries, row.State);
        Assert.Null(row.WapeRatio);
        Assert.Null(row.Mae);
        Assert.Null(row.Bias);
        Assert.Null(row.Mase);
        Assert.Empty(row.ByWeeksAhead);
    }

    private static List<RollingOriginEvaluator.WeeklyPoint> Weeks(int count,
        Func<int, decimal> quantity) =>
        Enumerable.Range(0, count)
            .Select(index => new RollingOriginEvaluator.WeeklyPoint(
                Start.AddDays(index * 7), RollingOriginEvaluator.WeekState.Observed,
                quantity(index)))
            .ToList();
}
