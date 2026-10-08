using Diten.PlanningService.Application.Features.DemandPlanning;
using Eligibility = Diten.PlanningService.Application.Features.DemandPlanning.ForecastMethodEligibility;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed class FixtureForecastEngineTests
{
    private static readonly DateOnly Start = new(2023, 1, 2);
    private static readonly Eligibility.FixturePolicy Policy = Eligibility.FixturePolicy.Mvp("fixture-policy-v1");

    [Fact]
    public void Naive_UsesLastVerifiedObservationForAllFiftyTwoWeeks()
    {
        var history = History(78, index => index + 1);

        var row = Assert.Single(FixtureForecastEngine.Compare(history, Policy,
            Start.AddDays(78 * 7), [Eligibility.Method.Naive]));

        Assert.Equal(52, row.Forecast52.Count);
        Assert.Equal(history.Series, row.Series);
        Assert.All(row.Forecast52, value => Assert.Equal(78m, value));
        Assert.Equal("fixture-policy-v1", row.PolicyVersion);
        Assert.Empty(row.Parameters);
        Assert.Equal(RollingOriginEvaluator.EvaluationState.Complete, row.Evaluation.State);
        Assert.Equal(52, row.Evaluation.ByWeeksAhead.Count);
    }

    [Fact]
    public void SeasonalNaive_RepeatsLastVerifiedYearWithoutFutureObservations()
    {
        var history = History(156, index => index % 52 + 1);

        var row = Assert.Single(FixtureForecastEngine.Compare(history, Policy,
            Start.AddDays(156 * 7), [Eligibility.Method.SeasonalNaive]));

        Assert.Equal(Enumerable.Range(1, 52).Select(value => (decimal)value), row.Forecast52);
        Assert.Equal("52", row.Parameters["annualLagWeeks"]);
        Assert.Equal(RollingOriginEvaluator.EvaluationState.Complete, row.Evaluation.State);
        Assert.Equal(52, row.Evaluation.ByWeeksAhead.Count);
    }

    [Fact]
    public void Candidates_RemainInRequestedOrderWithoutRankingOrWinner()
    {
        var history = History(156, index => index % 52 + 1);

        var rows = FixtureForecastEngine.Compare(history, Policy,
            Start.AddDays(156 * 7),
            [Eligibility.Method.SeasonalNaive, Eligibility.Method.Naive]);

        Assert.Equal([Eligibility.Method.SeasonalNaive, Eligibility.Method.Naive],
            rows.Select(row => row.Method).ToArray());
        var metrics = FixtureForecastEngine.SideBySide(rows);
        Assert.Equal(["SeasonalNaive", "Naive"],
            metrics.Select(row => row.MethodName).ToArray());
        Assert.All(metrics, row => Assert.Equal(52, row.ByWeeksAhead.Count));
        Assert.Null(typeof(FixtureForecastEngine.CandidateResult).GetProperty("Winner"));
        Assert.Null(typeof(FixtureForecastEngine.CandidateResult).GetProperty("Rank"));
        Assert.Null(typeof(FixtureForecastEngine.CandidateResult).GetProperty("Selected"));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void UntrustedWeek_IsNotConvertedToObservedZero(int evidenceCode)
    {
        var history = History(78, _ => 0,
            exceptionalWeek: (Eligibility.WeekEvidence)evidenceCode);

        Assert.Throws<InvalidOperationException>(() => FixtureForecastEngine.Compare(
            history, Policy, Start.AddDays(78 * 7), [Eligibility.Method.Naive]));
    }

    [Fact]
    public void VerifiedZeroDemand_ProducesZeroButDoesNotInventWapeOrMase()
    {
        var history = History(78, _ => 0);

        var row = Assert.Single(FixtureForecastEngine.Compare(history, Policy,
            Start.AddDays(78 * 7), [Eligibility.Method.Naive]));

        Assert.All(row.Forecast52, value => Assert.Equal(0m, value));
        Assert.Null(row.Evaluation.WapeRatio);
        Assert.Null(row.Evaluation.Mase);
    }

    [Fact]
    public void InsufficientHistory_RejectsMethodInsteadOfSilentlyFallingBack()
    {
        Assert.Throws<InvalidOperationException>(() => FixtureForecastEngine.Compare(
            History(25, _ => 1), Policy, Start.AddDays(25 * 7),
            [Eligibility.Method.Naive]));
        Assert.Throws<InvalidOperationException>(() => FixtureForecastEngine.Compare(
            History(103, _ => 1), Policy, Start.AddDays(103 * 7),
            [Eligibility.Method.SeasonalNaive]));
    }

    [Fact]
    public void AsOfBoundary_RejectsFutureOrOmittedHistoryWeek()
    {
        var history = History(78, _ => 1);

        Assert.Throws<ArgumentException>(() => FixtureForecastEngine.Compare(
            history, Policy, Start.AddDays(77 * 7), [Eligibility.Method.Naive]));
        Assert.Throws<ArgumentException>(() => FixtureForecastEngine.Compare(
            history, Policy, Start.AddDays(79 * 7), [Eligibility.Method.Naive]));
    }

    [Fact]
    public void HoldoutChange_DoesNotAlterEarlierOriginForecast()
    {
        var first = FixtureForecastEngine.Compare(History(78, index => index + 1),
            Policy, Start.AddDays(78 * 7), [Eligibility.Method.Naive])[0];
        var changed = FixtureForecastEngine.Compare(History(78,
                index => index == 77 ? 900 : index + 1),
            Policy, Start.AddDays(78 * 7), [Eligibility.Method.Naive])[0];

        Assert.Equal(first.Evaluation.Errors[0].Forecast,
            changed.Evaluation.Errors[0].Forecast);
        Assert.NotEqual(first.Forecast52[0], changed.Forecast52[0]);
    }

    [Fact]
    public void MovingAverage_UsesOnlyLastObservedWindowForAllFiftyTwoFutureWeeks()
    {
        // Independent arithmetic: (10 + 20 + 30) / 3 = 20.
        var history = History(26, index => index switch
        {
            23 => 10,
            24 => 20,
            25 => 30,
            _ => 5
        });
        var row = Assert.Single(FixtureForecastEngine.Compare(history, Policy,
            Start.AddDays(26 * 7), [Eligibility.Method.MovingAverage],
            [Spec(Eligibility.Method.MovingAverage, "trailing-flat", window: 3)]));
        Assert.Equal(52, row.Forecast52.Count);
        Assert.All(row.Forecast52, value => Assert.Equal(20m, value));
        Assert.Equal("3", row.Parameters["windowWeeks"]);
        Assert.Equal("method-fixture-v1", row.Parameters["methodPolicyVersion"]);
    }

    [Fact]
    public void Ses_UsesExplicitAlphaAndFirstObservationInitialization()
    {
        // Independent arithmetic: 10 -> 0.2*12+0.8*10=10.4
        // -> 0.2*15+0.8*10.4=11.32.
        var history = History(26, index => index == 24 ? 12 : index == 25 ? 15 : 10);
        var row = Assert.Single(FixtureForecastEngine.Compare(history, Policy,
            Start.AddDays(26 * 7), [Eligibility.Method.SimpleExponentialSmoothing],
            [Spec(Eligibility.Method.SimpleExponentialSmoothing, "level-flat", alpha: 0.2m)]));
        Assert.All(row.Forecast52, value => Assert.Equal(11.32m, value));
        Assert.Equal("0.2", row.Parameters["alpha"]);
    }

    [Fact]
    public void Holt_UsesExplicitAdditiveUndampedEquations()
    {
        // Independently calculated after a 10,12,...,58 line and final 70:
        // final level=64, trend=3.2, h1=67.2, h2=70.4, h52=230.4.
        var history = History(26, index => index == 25 ? 70 : 10 + 2 * index);
        var row = Assert.Single(FixtureForecastEngine.Compare(history, Policy,
            Start.AddDays(26 * 7), [Eligibility.Method.HoltTrend],
            [Spec(Eligibility.Method.HoltTrend, "additive-undamped",
                alpha: 0.4m, beta: 0.3m)]));
        Assert.Equal(52, row.Forecast52.Count);
        Assert.Equal(67.2m, row.Forecast52[0]);
        Assert.Equal(70.4m, row.Forecast52[1]);
        Assert.Equal(230.4m, row.Forecast52[51]);
        Assert.Equal("0.3", row.Parameters["betaStar"]);
    }

    [Fact]
    public void ParameterizedMethod_RequiresCompleteExplicitVersionedSpecification()
    {
        var history = History(26, _ => 10);
        var asOf = Start.AddDays(26 * 7);
        Assert.Throws<ArgumentException>(() => FixtureForecastEngine.Compare(history, Policy,
            asOf, [Eligibility.Method.MovingAverage]));
        Assert.Throws<ArgumentException>(() => FixtureForecastEngine.Compare(history, Policy,
            asOf, [Eligibility.Method.MovingAverage],
            [Spec(Eligibility.Method.MovingAverage, "trailing-flat")]));
        Assert.Throws<ArgumentOutOfRangeException>(() => FixtureForecastEngine.Compare(history, Policy,
            asOf, [Eligibility.Method.HoltTrend],
            [Spec(Eligibility.Method.HoltTrend, "additive-undamped", alpha: 0.4m)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => FixtureForecastEngine.Compare(history, Policy,
            asOf, [Eligibility.Method.SimpleExponentialSmoothing],
            [Spec(Eligibility.Method.SimpleExponentialSmoothing, "level-flat", alpha: 0m)]));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void NewGeneralMethods_RequireTwentySixVerifiedWeeks(int methodCode)
    {
        var method = (Eligibility.Method)methodCode;
        var spec = method switch
        {
            Eligibility.Method.MovingAverage => Spec(method, "trailing-flat", window: 3),
            Eligibility.Method.SimpleExponentialSmoothing =>
                Spec(method, "level-flat", alpha: 0.2m),
            _ => Spec(method, "additive-undamped", alpha: 0.4m, beta: 0.3m)
        };
        Assert.Throws<InvalidOperationException>(() => FixtureForecastEngine.Compare(
            History(25, _ => 10), Policy, Start.AddDays(25 * 7), [method], [spec]));
        var row = Assert.Single(FixtureForecastEngine.Compare(
            History(26, _ => 10), Policy, Start.AddDays(26 * 7), [method], [spec]));
        Assert.Equal(52, row.Forecast52.Count);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void ParameterizedMethods_RejectUntrustedWeekRatherThanTreatingItAsZero(int evidenceCode)
    {
        var history = History(78, _ => 0, (Eligibility.WeekEvidence)evidenceCode);
        foreach (var method in new[]
            { Eligibility.Method.MovingAverage, Eligibility.Method.SimpleExponentialSmoothing,
                Eligibility.Method.HoltTrend })
        {
            var spec = method switch
            {
                Eligibility.Method.MovingAverage => Spec(method, "trailing-flat", window: 3),
                Eligibility.Method.SimpleExponentialSmoothing =>
                    Spec(method, "level-flat", alpha: 0.2m),
                _ => Spec(method, "additive-undamped", alpha: 0.4m, beta: 0.3m)
            };
            Assert.Throws<InvalidOperationException>(() => FixtureForecastEngine.Compare(
                history, Policy, Start.AddDays(78 * 7), [method], [spec]));
        }
    }

    [Fact]
    public void Ses_RollingOriginDoesNotUseFutureHoldoutObservation()
    {
        var spec = Spec(Eligibility.Method.SimpleExponentialSmoothing, "level-flat", alpha: 0.2m);
        var first = FixtureForecastEngine.Compare(History(78, _ => 10), Policy,
            Start.AddDays(78 * 7), [spec.Method], [spec])[0];
        var changed = FixtureForecastEngine.Compare(History(78,
                index => index == 77 ? 1000 : 10), Policy,
            Start.AddDays(78 * 7), [spec.Method], [spec])[0];
        Assert.Equal(first.Evaluation.Errors[0].Forecast,
            changed.Evaluation.Errors[0].Forecast);
        Assert.NotEqual(first.Forecast52[0], changed.Forecast52[0]);
    }

    [Fact]
    public void NoMethodIsChosenByMetricsOrImplicitFallback()
    {
        var history = History(78, _ => 0);
        var rows = FixtureForecastEngine.Compare(history, Policy,
            Start.AddDays(78 * 7),
            [Eligibility.Method.SimpleExponentialSmoothing, Eligibility.Method.MovingAverage],
            [Spec(Eligibility.Method.SimpleExponentialSmoothing, "level-flat", alpha: 0.2m),
             Spec(Eligibility.Method.MovingAverage, "trailing-flat", window: 3)]);
        Assert.Equal([Eligibility.Method.SimpleExponentialSmoothing,
            Eligibility.Method.MovingAverage], rows.Select(row => row.Method).ToArray());
        Assert.All(rows, row => Assert.Null(row.Evaluation.WapeRatio));
        Assert.All(rows, row => Assert.All(row.Forecast52, value => Assert.Equal(0m, value)));
        Assert.Null(typeof(FixtureForecastEngine.CandidateResult).GetProperty("Winner"));
    }

    [Fact]
    public void AdditiveHoltWinters_UsesTwoYearsAndPreservesRealSeasonalZero()
    {
        // Independent two-year initializer: annual means = 10 and 10;
        // trend = 0, second-year seasonal terms = -10, +10, then 0.
        var spec = Spec(Eligibility.Method.HoltWinters, "additive-52-undamped",
            alpha: 0.5m, beta: 0.5m, gamma: 0.2m);
        var row = Assert.Single(FixtureForecastEngine.Compare(
            History(104, SeasonalQuantity), Policy, Start.AddDays(104 * 7),
            [spec.Method], [spec]));
        Assert.Equal(52, row.Forecast52.Count);
        Assert.Equal(0m, row.Forecast52[0]);
        Assert.Equal(20m, row.Forecast52[1]);
        Assert.All(row.Forecast52.Skip(2), value => Assert.Equal(10m, value));
        Assert.Equal("52", row.Parameters["seasonWeeks"]);
        Assert.Equal("method-fixture-v1", row.Parameters["methodPolicyVersion"]);
    }

    [Fact]
    public void AdditiveHoltWinters_UpdatesLevelTrendAndSeasonWithoutFutureLeakage()
    {
        // For week 105, observed 4 rather than seasonal 0:
        // prior base=10, level=12, trend=1, season=-9.2; next week=12+1+10=23.
        var spec = Spec(Eligibility.Method.HoltWinters, "additive-52-undamped",
            alpha: 0.5m, beta: 0.5m, gamma: 0.2m);
        var row = Assert.Single(FixtureForecastEngine.Compare(
            History(105, index => index == 104 ? 4 : SeasonalQuantity(index)),
            Policy, Start.AddDays(105 * 7), [spec.Method], [spec]));
        Assert.Equal(23m, row.Forecast52[0]);
        Assert.Equal(54.8m, row.Forecast52[51]);

        var first = FixtureForecastEngine.Compare(History(156, SeasonalQuantity),
            Policy, Start.AddDays(156 * 7), [spec.Method], [spec])[0];
        var changed = FixtureForecastEngine.Compare(
            History(156, index => index == 155 ? 50 : SeasonalQuantity(index)),
            Policy, Start.AddDays(156 * 7), [spec.Method], [spec])[0];
        Assert.Equal(first.Evaluation.Errors[0].Forecast,
            changed.Evaluation.Errors[0].Forecast);
        Assert.NotEqual(first.Forecast52[0], changed.Forecast52[0]);
    }

    [Fact]
    public void AdditiveHoltWinters_RejectsShortUntrustedAndNegativeProjection()
    {
        var spec = Spec(Eligibility.Method.HoltWinters, "additive-52-undamped",
            alpha: 0.5m, beta: 0.5m, gamma: 0.2m);
        Assert.Throws<InvalidOperationException>(() => FixtureForecastEngine.Compare(
            History(103, SeasonalQuantity), Policy, Start.AddDays(103 * 7),
            [spec.Method], [spec]));
        Assert.Throws<InvalidOperationException>(() => FixtureForecastEngine.Compare(
            History(104, SeasonalQuantity, Eligibility.WeekEvidence.Unknown),
            Policy, Start.AddDays(104 * 7), [spec.Method], [spec]));
        Assert.Throws<InvalidOperationException>(() => FixtureForecastEngine.Compare(
            History(104, index => index < 52 ? 100 : 0),
            Policy, Start.AddDays(104 * 7), [spec.Method], [spec]));
    }

    [Fact]
    public void SbaCroston_UsesTwoExplicitSmoothingCoefficientsAndFirstInterval()
    {
        // Independent arithmetic for events at weeks 1,3,5,7 (one-based):
        // first interval=1; successive gaps=2 give smoothed intervals
        // 1.5, 1.75, 1.875. Size remains 15; SBA=(1-0.5/2)*15/1.875=6.
        var spec = Spec(Eligibility.Method.BiasCorrectedCroston,
            "sba-naive-initialization", alpha: 0.5m, beta: 0.5m);
        var row = Assert.Single(FixtureForecastEngine.Compare(
            History(26, index => index is 0 or 2 or 4 or 6 ? 15 : 0),
            Policy, Start.AddDays(26 * 7), [spec.Method], [spec]));
        Assert.Equal(52, row.Forecast52.Count);
        Assert.All(row.Forecast52, value => Assert.Equal(6m, value));
        Assert.Equal("0.5", row.Parameters["demandAlpha"]);
        Assert.Equal("0.5", row.Parameters["intervalAlpha"]);
        Assert.Equal("method-fixture-v1", row.Parameters["methodPolicyVersion"]);
    }

    [Fact]
    public void SbaCroston_RejectsThreeEventsAndUntrustedWeek()
    {
        var spec = Spec(Eligibility.Method.BiasCorrectedCroston,
            "sba-naive-initialization", alpha: 0.5m, beta: 0.5m);
        Assert.Throws<InvalidOperationException>(() => FixtureForecastEngine.Compare(
            History(26, index => index is 0 or 2 or 4 ? 15 : 0),
            Policy, Start.AddDays(26 * 7), [spec.Method], [spec]));
        Assert.Throws<InvalidOperationException>(() => FixtureForecastEngine.Compare(
            History(26, index => index is 0 or 2 or 4 or 6 ? 15 : 0,
                Eligibility.WeekEvidence.StockoutUnresolved),
            Policy, Start.AddDays(26 * 7), [spec.Method], [spec]));
    }

    [Fact]
    public void SbaCroston_RollingOriginNeverSeesTheLastHoldoutWeek()
    {
        var spec = Spec(Eligibility.Method.BiasCorrectedCroston,
            "sba-naive-initialization", alpha: 0.5m, beta: 0.5m);
        int Quantity(int index) => index is 0 or 2 or 4 or 6 ? 15 : 0;
        var first = FixtureForecastEngine.Compare(History(78, Quantity), Policy,
            Start.AddDays(78 * 7), [spec.Method], [spec])[0];
        var changed = FixtureForecastEngine.Compare(
            History(78, index => index == 77 ? 30 : Quantity(index)), Policy,
            Start.AddDays(78 * 7), [spec.Method], [spec])[0];
        Assert.Equal(first.Evaluation.Errors[0].Forecast,
            changed.Evaluation.Errors[0].Forecast);
        Assert.NotEqual(first.Forecast52[0], changed.Forecast52[0]);
    }

    [Fact]
    public void SbaCroston_BacktestBeginsOnlyAfterFourthRealEvent()
    {
        var spec = Spec(Eligibility.Method.BiasCorrectedCroston,
            "sba-naive-initialization", alpha: 0.5m, beta: 0.5m);
        var row = Assert.Single(FixtureForecastEngine.Compare(
            History(130, index => index is 0 or 2 or 4 or 75 ? 15 : 0),
            Policy, Start.AddDays(130 * 7), [spec.Method], [spec]));
        Assert.Equal(RollingOriginEvaluator.EvaluationState.Complete, row.Evaluation.State);
        Assert.Equal(76, row.Evaluation.Errors[0].TrainingWeekCount);
    }

    [Fact]
    public void UnapprovedVariantsAndInvalidCoefficientsAreRejected()
    {
        var history = History(104, SeasonalQuantity);
        var asOf = Start.AddDays(104 * 7);
        Assert.Throws<ArgumentException>(() => FixtureForecastEngine.Compare(
            history, Policy, asOf, [Eligibility.Method.HoltWinters],
            [Spec(Eligibility.Method.HoltWinters, "multiplicative-52",
                alpha: 0.5m, beta: 0.5m, gamma: 0.2m)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => FixtureForecastEngine.Compare(
            history, Policy, asOf, [Eligibility.Method.HoltWinters],
            [Spec(Eligibility.Method.HoltWinters, "additive-52-undamped",
                alpha: 0.8m, beta: 0.5m, gamma: 0.3m)]));
        Assert.Throws<ArgumentException>(() => FixtureForecastEngine.Compare(
            history, Policy, asOf, [Eligibility.Method.BiasCorrectedCroston],
            [Spec(Eligibility.Method.BiasCorrectedCroston, "sbj-naive-initialization",
                alpha: 0.5m, beta: 0.5m)]));
        Assert.Throws<ArgumentException>(() => FixtureForecastEngine.Compare(
            history, Policy, asOf, [Eligibility.Method.BiasCorrectedCroston]));
    }

    private static int SeasonalQuantity(int index) => (index % 52) switch
    {
        0 => 0,
        1 => 20,
        _ => 10
    };

    private static FixtureForecastEngine.MethodSpecification Spec(Eligibility.Method method,
        string variant, int? window = null, decimal? alpha = null, decimal? beta = null,
        decimal? gamma = null) =>
        new(method, variant, "method-fixture-v1", window, alpha, beta, gamma);

    private static Eligibility.VerifiedHistoryFixture History(int weeks,
        Func<int, int> quantity, Eligibility.WeekEvidence? exceptionalWeek = null)
    {
        var series = new Eligibility.SeriesKey(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "WH-1");
        var points = Enumerable.Range(0, weeks).Select(index =>
        {
            var start = Start.AddDays(index * 7);
            var evidence = index == weeks - 1 && exceptionalWeek is not null
                ? exceptionalWeek.Value : quantity(index) == 0
                    ? Eligibility.WeekEvidence.VerifiedZeroDemand
                    : Eligibility.WeekEvidence.VerifiedDemand;
            var events = evidence == Eligibility.WeekEvidence.VerifiedDemand
                ? new[] { new Eligibility.FixtureDemandEvent(Guid.NewGuid(), series,
                    start, quantity(index)) }
                : Array.Empty<Eligibility.FixtureDemandEvent>();
            return new Eligibility.FixtureWeek(start, evidence, events);
        }).ToArray();
        return new Eligibility.VerifiedHistoryFixture(series, points);
    }
}
