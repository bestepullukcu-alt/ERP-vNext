using Diten.PlanningService.Application.Features.DemandPlanning;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed class ForecastEvaluationTests
{
    private static readonly DateOnly Start = new(2026, 1, 5);

    [Fact]
    public void RollingOriginsNeverExposeTheTestWeekToTraining()
    {
        var series = Weeks(28, i => i + 1);
        var calls = 0;

        var result = RollingOriginEvaluator.Evaluate(series, 26, 1, (training, horizon) =>
        {
            Assert.Equal(1, horizon);
            Assert.Equal(26 + calls, training.Count);
            Assert.Equal((decimal)training.Count, training[^1]);
            calls++;
            return [0m];
        });

        Assert.Equal(2, calls);
        Assert.Equal(RollingOriginEvaluator.EvaluationState.Complete, result.State);
        Assert.Equal([26, 27], result.Errors.Select(error => error.TrainingWeekCount));
        Assert.Equal([Start.AddDays(26 * 7), Start.AddDays(27 * 7)],
            result.Errors.Select(error => error.WeekStart));
    }

    [Fact]
    public void ComputesWapeAndMaeOnlyFromHeldOutObservations()
    {
        var series = Weeks(27, i => i == 26 ? 10 : 1);

        var result = RollingOriginEvaluator.Evaluate(series, 26, 1, (_, _) => [5m]);

        Assert.Equal(RollingOriginEvaluator.EvaluationState.Complete, result.State);
        Assert.Equal(5m, result.Mae);
        Assert.Equal(0.5m, result.WapeRatio);
        Assert.Equal(5m, result.Bias);
        Assert.Null(result.Mase);
        var oneWeekAhead = Assert.Single(result.ByWeeksAhead);
        Assert.Equal(1, oneWeekAhead.WeeksAhead);
        Assert.Equal(result.Mae, oneWeekAhead.Mae);
        Assert.Equal(result.WapeRatio, oneWeekAhead.WapeRatio);
        Assert.Equal(result.Bias, oneWeekAhead.Bias);
    }

    [Fact]
    public void RealZeroIsObservedButWapeWithZeroDenominatorIsUndefined()
    {
        var series = Weeks(27, i => i == 26 ? 0 : 1);

        var result = RollingOriginEvaluator.Evaluate(series, 26, 1, (_, _) => [2m]);

        Assert.Equal(RollingOriginEvaluator.EvaluationState.Complete, result.State);
        Assert.Equal(2m, result.Mae);
        Assert.Null(result.WapeRatio);
        Assert.True(result.IsWapeUndefined);
        Assert.Equal(-2m, result.Bias);
        Assert.Null(result.Mase);
        Assert.True(result.IsMaseUndefined);
    }

    [Theory]
    [InlineData(RollingOriginEvaluator.WeekState.Missing)]
    [InlineData(RollingOriginEvaluator.WeekState.Unknown)]
    public void MissingOrUnknownWeekIsNeverConvertedToRealZero(RollingOriginEvaluator.WeekState state)
    {
        var series = Weeks(27, _ => 1);
        series[^1] = new RollingOriginEvaluator.WeeklyPoint(series[^1].WeekStart, state, null);

        var result = RollingOriginEvaluator.Evaluate(series, 26, 1,
            (_, _) => throw new InvalidOperationException("Forecast must not run"));

        Assert.Equal(RollingOriginEvaluator.EvaluationState.IncompleteSeries, result.State);
        Assert.Empty(result.Errors);
        Assert.Null(result.Mae);
        Assert.Null(result.WapeRatio);
        Assert.Null(result.Bias);
        Assert.Null(result.Mase);
        Assert.Empty(result.ByWeeksAhead);
    }

    [Fact]
    public void ShortSeriesDoesNotRunForecast()
    {
        var result = RollingOriginEvaluator.Evaluate(Weeks(26, _ => 1), 26, 1,
            (_, _) => throw new InvalidOperationException("Forecast must not run"));

        Assert.Equal(RollingOriginEvaluator.EvaluationState.InsufficientHistory, result.State);
        Assert.Empty(result.ByWeeksAhead);
    }

    [Fact]
    public void GapInWeeksIsNotInterpretedAsZeroDemand()
    {
        var series = Weeks(27, _ => 1);
        series[10] = series[10] with { WeekStart = series[10].WeekStart.AddDays(7) };

        var result = RollingOriginEvaluator.Evaluate(series, 26, 1,
            (_, _) => throw new InvalidOperationException("Forecast must not run"));

        Assert.Equal(RollingOriginEvaluator.EvaluationState.IncompleteSeries, result.State);
        Assert.Empty(result.ByWeeksAhead);
    }

    [Fact]
    public void RejectsForecastThatDoesNotCoverTheHoldoutHorizon()
    {
        Assert.Throws<InvalidOperationException>(() =>
            RollingOriginEvaluator.Evaluate(Weeks(27, _ => 1), 26, 1, (_, _) => []));
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(15, -5)]
    [InlineData(10, 0)]
    public void BiasUsesActualMinusForecast(int forecast, int expectedBias)
    {
        var series = Weeks(27, i => i == 26 ? 10 : i);

        var result = RollingOriginEvaluator.Evaluate(series, 26, 1, (_, _) => [forecast]);

        Assert.Equal((decimal)expectedBias, result.Bias);
        Assert.Equal((decimal)expectedBias, result.Errors.Single().SignedError);
    }

    [Fact]
    public void NonSeasonalMaseUsesEachOriginsTrainingScaleWithoutFutureLeakage()
    {
        var series = Weeks(28, i => i < 26 ? i : 100);

        var result = RollingOriginEvaluator.Evaluate(series, 26, 1, (_, _) => [0m]);

        Assert.Equal(RollingOriginEvaluator.EvaluationState.Complete, result.State);
        Assert.Equal(1m, result.Errors[0].MaseTrainingScale);
        Assert.Equal(100m / 26m, result.Errors[1].MaseTrainingScale);
        Assert.Equal(63m, result.Mase);
    }

    [Fact]
    public void ApprovedAnnualSeasonalMaseUsesOnly104TrainingWeeksAndLag52()
    {
        var series = Weeks(105, i => i == 104 ? 100 : i % 52 + i / 52);

        var result = RollingOriginEvaluator.Evaluate(series, 104, 1, (_, _) => [90m],
            RollingOriginEvaluator.MaseComparison.ApprovedAnnualSeasonal);

        Assert.Equal(RollingOriginEvaluator.EvaluationState.Complete, result.State);
        Assert.Equal(1m, result.Errors.Single().MaseTrainingScale);
        Assert.Equal(10m, result.Mase);
        Assert.Equal(10m, result.Bias);
    }

    [Fact]
    public void AnnualSeasonalMaseRequires104TrainingWeeks()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RollingOriginEvaluator.Evaluate(Weeks(105, i => i), 26, 1, (_, _) => [0m],
                RollingOriginEvaluator.MaseComparison.ApprovedAnnualSeasonal));

        var result = RollingOriginEvaluator.Evaluate(Weeks(104, i => i), 104, 1,
            (_, _) => throw new InvalidOperationException("Forecast must not run"),
            RollingOriginEvaluator.MaseComparison.ApprovedAnnualSeasonal);

        Assert.Equal(RollingOriginEvaluator.EvaluationState.InsufficientHistory, result.State);
        Assert.Null(result.Mase);
    }

    [Fact]
    public void ZeroTrainingScaleLeavesMaseUndefinedWhileOtherMetricsRemainAvailable()
    {
        var series = Weeks(27, i => i == 26 ? 2 : 1);

        var result = RollingOriginEvaluator.Evaluate(series, 26, 1, (_, _) => [0m]);

        Assert.Equal(RollingOriginEvaluator.EvaluationState.Complete, result.State);
        Assert.Null(result.Errors.Single().MaseTrainingScale);
        Assert.Null(result.Mase);
        Assert.True(result.IsMaseUndefined);
        Assert.Equal(2m, result.Mae);
        Assert.Equal(2m, result.Bias);
    }

    [Fact]
    public void RepeatingAnnualPatternHasUndefinedSeasonalMaseScale()
    {
        var series = Weeks(105, i => i == 104 ? 5 : i % 52);

        var result = RollingOriginEvaluator.Evaluate(series, 104, 1, (_, _) => [0m],
            RollingOriginEvaluator.MaseComparison.ApprovedAnnualSeasonal);

        Assert.Equal(RollingOriginEvaluator.EvaluationState.Complete, result.State);
        Assert.Null(result.Errors.Single().MaseTrainingScale);
        Assert.Null(result.Mase);
        Assert.Equal(5m, result.Mae);
    }

    [Fact]
    public void OneUndefinedOriginLeavesAggregateMaseUndefined()
    {
        var series = Weeks(28, i => i < 26 ? 1 : i - 24);

        var result = RollingOriginEvaluator.Evaluate(series, 26, 1, (_, _) => [0m]);

        Assert.Null(result.Errors[0].MaseTrainingScale);
        Assert.NotNull(result.Errors[1].MaseTrainingScale);
        Assert.Null(result.Mase);
    }

    [Fact]
    public void OverlappingHoldoutsCountTheSameWeekForEachOrigin()
    {
        var result = RollingOriginEvaluator.Evaluate(Weeks(29, i => i), 26, 2,
            (_, horizon) => Enumerable.Repeat(0m, horizon).ToArray());

        Assert.Equal(4, result.Errors.Count);
        Assert.Equal(result.Errors[1].WeekStart, result.Errors[2].WeekStart);
        Assert.Equal([26, 26, 27, 27], result.Errors.Select(error => error.TrainingWeekCount));
    }

    [Fact]
    public void SameCalendarWeekIsMeasuredSeparatelyAtEachForecastDistance()
    {
        var series = Weeks(29, i => i < 26 ? i : (i - 25) * 10);

        var result = RollingOriginEvaluator.Evaluate(series, 26, 2,
            (training, _) => training.Count == 26 ? [8m, 12m] : [15m, 25m]);

        Assert.Equal([1, 2], result.ByWeeksAhead.Select(metric => metric.WeeksAhead));
        Assert.Equal(2, result.ByWeeksAhead[0].Count);
        Assert.Equal(2, result.ByWeeksAhead[1].Count);
        Assert.Equal(result.Errors[1].WeekStart, result.Errors[2].WeekStart);
        Assert.Equal(2, result.Errors[1].WeeksAhead);
        Assert.Equal(1, result.Errors[2].WeeksAhead);

        var oneWeekAhead = result.ByWeeksAhead[0];
        Assert.Equal(3.5m, oneWeekAhead.Mae);
        Assert.Equal(7m / 30m, oneWeekAhead.WapeRatio);
        Assert.Equal(3.5m, oneWeekAhead.Bias);
        Assert.Equal(2.625m, oneWeekAhead.Mase);

        var twoWeeksAhead = result.ByWeeksAhead[1];
        Assert.Equal(6.5m, twoWeeksAhead.Mae);
        Assert.Equal(0.26m, twoWeeksAhead.WapeRatio);
        Assert.Equal(6.5m, twoWeeksAhead.Bias);
        Assert.Equal(5.625m, twoWeeksAhead.Mase);

        Assert.Equal(5m, result.Mae);
        Assert.Equal(0.25m, result.WapeRatio);
        Assert.Equal(5m, result.Bias);
        Assert.Equal(4.125m, result.Mase);
    }

    [Fact]
    public void ZeroActualDenominatorRemainsUndefinedOnlyForAffectedDistance()
    {
        var series = Weeks(29, i => i < 26 ? i : i == 28 ? 10 : 0);

        var result = RollingOriginEvaluator.Evaluate(series, 26, 2, (_, _) => [1m, 1m]);

        Assert.Equal(2, result.ByWeeksAhead.Count);
        Assert.Equal(2, result.ByWeeksAhead[0].Count);
        Assert.Equal(1m, result.ByWeeksAhead[0].Mae);
        Assert.Null(result.ByWeeksAhead[0].WapeRatio);
        Assert.True(result.ByWeeksAhead[0].IsWapeUndefined);
        Assert.Equal(-1m, result.ByWeeksAhead[0].Bias);
        Assert.Equal(1m, result.ByWeeksAhead[1].WapeRatio);
        Assert.False(result.ByWeeksAhead[1].IsWapeUndefined);
    }

    [Fact]
    public void ZeroTrainingScaleKeepsMaseUndefinedForEachDistance()
    {
        var series = Weeks(28, i => i < 26 ? 1 : 0);

        var result = RollingOriginEvaluator.Evaluate(series, 26, 2, (_, _) => [1m, 2m]);

        Assert.All(result.ByWeeksAhead, metric =>
        {
            Assert.Equal(1, metric.Count);
            Assert.Null(metric.WapeRatio);
            Assert.Null(metric.Mase);
            Assert.True(metric.IsMaseUndefined);
        });
        Assert.Equal(1m, result.ByWeeksAhead[0].Mae);
        Assert.Equal(2m, result.ByWeeksAhead[1].Mae);
    }

    private static List<RollingOriginEvaluator.WeeklyPoint> Weeks(int count, Func<int, int> quantity) =>
        Enumerable.Range(0, count)
            .Select(i => new RollingOriginEvaluator.WeeklyPoint(
                Start.AddDays(i * 7), RollingOriginEvaluator.WeekState.Observed, quantity(i)))
            .ToList();
}
