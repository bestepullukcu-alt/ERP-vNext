using System.Numerics;
using Diten.SupplyChainService.Domain.Features.SupplierPerformance;
using Xunit;
namespace Diten.SupplyChainService.Tests.SupplierPerformance;

/// <summary>MOD-0147 §0.0 / §0.4 / §0.1 — the pack's acceptance tables, measured on the production policy.</summary>
public sealed class ScorePolicyTests
{
    private static MetricInput M(string code, string value, string weight = "25") => new(code, value, weight);

    // T-0147-V1-SCORE-01 / T-0147-V1-BAND-01: the v1 acceptance table of §0.0.
    [Theory]
    [InlineData("OTD:90,QTY_ACC:80,QUAL_ACC:100,INV_MATCH:70", "85.00", "MEDIUM")]
    [InlineData("OTD:90,QTY_ACC:80,INV_MATCH:70", "80.00", "MEDIUM")]
    [InlineData("OTD:90,QTY_ACC:90,INV_MATCH:90", "90.00", "LOW")]
    [InlineData("OTD:90,QTY_ACC:80,INV_MATCH:71", "80.33", "MEDIUM")]
    [InlineData("OTD:89.995,QTY_ACC:89.995,QUAL_ACC:89.995,INV_MATCH:89.995", "90.00", "MEDIUM")]
    [InlineData("OTD:100,QTY_ACC:100,QUAL_ACC:100", "100.00", "LOW")]
    [InlineData("OTD:0,QTY_ACC:0,QUAL_ACC:0", "0.00", "CRITICAL")]
    [InlineData("OTD:75,QTY_ACC:75,QUAL_ACC:75", "75.00", "MEDIUM")]
    [InlineData("OTD:50,QTY_ACC:50,QUAL_ACC:50", "50.00", "HIGH")]
    [InlineData("OTD:49.9999,QTY_ACC:50,QUAL_ACC:50", "50.00", "CRITICAL")]
    // 241.004 / 3 = 80.334666…: one rounding gives 80.33; rounding to three places first (80.335) and then half-even would give 80.34.
    [InlineData("OTD:80.001,QTY_ACC:80.001,QUAL_ACC:81.002", "80.33", "MEDIUM")]
    public void Score_WhenV1InputsAreValid_ShouldMatchThePackTable(string input, string overall, string band)
    {
        var metrics = input.Split(',').Select(x => x.Split(':')).Select(x => M(x[0], x[1])).ToList();

        var outcome = ScorePolicy.Score(metrics);

        Assert.NotNull(outcome);
        Assert.Equal(overall, outcome!.OverallScore);
        Assert.Equal(band, outcome.RiskLevel);
        Assert.All(outcome.Metrics, m => Assert.Equal("25", m.Weight));
        Assert.Equal(metrics.Select(x => x.MeasuredValue), outcome.Metrics.Select(x => x.Score));
    }

    // §0.1 arithmetic examples (not v1 inputs — under v1 these weights are 422): one rounding, half-even, band unrounded.
    [Theory]
    [InlineData(new[] { "92.5", "80" }, new[] { 60, 40 }, "87.50", "MEDIUM")]
    [InlineData(new[] { "89.995" }, new[] { 100 }, "90.00", "MEDIUM")]
    [InlineData(new[] { "75.125" }, new[] { 100 }, "75.12", "MEDIUM")]
    [InlineData(new[] { "75.135" }, new[] { 100 }, "75.14", "MEDIUM")]
    [InlineData(new[] { "49.995" }, new[] { 100 }, "50.00", "CRITICAL")]
    [InlineData(new[] { "60", "39.9999" }, new[] { 60, 40 }, "52.00", "HIGH")]
    public void Overall_And_Band_WhenGivenThePackArithmeticExamples_ShouldRoundOnceHalfEvenAndBandFromTheUnroundedValue(string[] values, int[] weights, string overall, string band)
    {
        var weighted = values.Zip(weights).Aggregate(BigInteger.Zero, (sum, x) => { ScorePolicy.TryScaled(x.First, out var v); return sum + v * x.Second; });
        var total = new BigInteger(weights.Sum());

        Assert.Equal(overall, ScorePolicy.Overall(weighted, total));
        Assert.Equal(band, ScorePolicy.Band(weighted, total));
    }

    // T-0147-V1-MISS-01: fewer than three metrics is refused; a missing metric is its absence.
    [Fact]
    public void Score_WhenOnlyTwoMetricsAreSent_ShouldRefuse()
    {
        Assert.Null(ScorePolicy.Score([M("OTD", "90"), M("QTY_ACC", "80")]));
        Assert.Null(ScorePolicy.Score([M("OTD", "90")]));
        Assert.Null(ScorePolicy.Score([]));
        var three = ScorePolicy.Score([M("OTD", "90"), M("QTY_ACC", "80"), M("INV_MATCH", "70")]);
        Assert.DoesNotContain(three!.Metrics, x => x.MetricCode == "QUAL_ACC");
    }

    // T-0147-V1-CODE-01: exact codes, no duplicate, no fifth metric, weight exactly 25.
    [Theory]
    [InlineData("ON_TIME_DELIVERY:90:25,QTY_ACC:80:25,QUAL_ACC:90:25")]
    [InlineData("otd:90:25,QTY_ACC:80:25,QUAL_ACC:90:25")]
    [InlineData("OTD:90:25,OTD:80:25,QUAL_ACC:90:25")]
    [InlineData("OTD:90:25,QTY_ACC:80:25,QUAL_ACC:90:25,INV_MATCH:70:25,OTD:70:25")]
    [InlineData("OTD:90:25,QTY_ACC:80:25,QUAL_ACC:90:25,INV_MATCH:70:25,EXTRA:70:25")]
    [InlineData("OTD:90:33.3333,QTY_ACC:80:33.3333,QUAL_ACC:90:33.3334")]
    [InlineData("OTD:90:40,QTY_ACC:80:25,QUAL_ACC:90:25")]
    [InlineData("OTD:90:24.9999,QTY_ACC:80:25,QUAL_ACC:90:25")]
    [InlineData("OTD:90:25.00001,QTY_ACC:80:25,QUAL_ACC:90:25")]
    [InlineData("OTD:-1:25,QTY_ACC:80:25,QUAL_ACC:90:25")]
    [InlineData("OTD:100.0001:25,QTY_ACC:80:25,QUAL_ACC:90:25")]
    [InlineData("OTD:50.12345:25,QTY_ACC:80:25,QUAL_ACC:90:25")]
    public void Score_WhenARuleOfThePolicyIsBroken_ShouldRefuse(string input)
    {
        var metrics = input.Split(',').Select(x => x.Split(':')).Select(x => M(x[0], x[1], x[2])).ToList();

        Assert.Null(ScorePolicy.Score(metrics));
    }

    [Theory]
    [InlineData("25.0")] [InlineData("25.0000")] [InlineData("025")]
    public void Score_WhenTheWeightIsTwentyFiveWrittenDifferently_ShouldAcceptAndAnswerThePolicyWeight(string weight)
    {
        var outcome = ScorePolicy.Score([M("OTD", "90", weight), M("QTY_ACC", "80"), M("QUAL_ACC", "70")]);

        Assert.NotNull(outcome);
        Assert.All(outcome!.Metrics, m => Assert.Equal("25", m.Weight));
    }

    // T-0147-V1-PER-01: one calendar month, first to last day.
    [Theory]
    [InlineData("2026-07-01", "2026-07-31", true)]
    [InlineData("2026-02-01", "2026-02-28", true)]
    [InlineData("2028-02-01", "2028-02-29", true)]
    [InlineData("2028-02-01", "2028-02-28", false)]
    [InlineData("2026-07-01", "2026-09-30", false)]
    [InlineData("2026-07-02", "2026-07-31", false)]
    [InlineData("2026-07-01", "2026-07-30", false)]
    [InlineData("2026-07-01", "2026-08-01", false)]
    [InlineData("2026-07-31", "2026-07-01", false)]
    public void IsCalendarMonth_ShouldAcceptExactlyOneCalendarMonth(string start, string end, bool expected) =>
        Assert.Equal(expected, ScorePolicy.IsCalendarMonth(DateOnly.Parse(start), DateOnly.Parse(end)));
}
