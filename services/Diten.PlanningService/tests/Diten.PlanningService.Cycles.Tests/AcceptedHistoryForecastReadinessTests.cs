using Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed class AcceptedHistoryForecastReadinessTests
{
    private static AcceptedSeriesHistory History(int reliableWeeks, int unknownWeeks = 0,
        string unit = "CASE")
    {
        var from = new DateOnly(2026, 1, 5);
        var weeks = Enumerable.Range(0, reliableWeeks + unknownWeeks)
            .Select(index => new AcceptedWeekObservation(from.AddDays(index * 7),
                index < reliableWeeks ? AcceptedWeekState.VerifiedZeroDemand : AcceptedWeekState.Unknown,
                index < reliableWeeks ? unit : null, index < reliableWeeks, null, []))
            .ToArray();
        return new AcceptedSeriesHistory(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Gsku", "wh-01", from.AddDays(weeks.Length * 7), weeks);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    public void FewerThan26ReliableWeeks_RequiresManualDraft(int count)
    {
        var result = AcceptedHistoryForecastReadiness.Assess(History(count));
        Assert.Equal(AcceptedHistoryRoute.ManualDraftRequired, result.Route);
        Assert.Equal(count, result.ReliableWeeks);
    }

    [Fact]
    public void TwentySixReliableWeeks_AllowCandidateButPreserveUnitWarning()
    {
        var result = AcceptedHistoryForecastReadiness.Assess(History(26));
        Assert.Equal(AcceptedHistoryRoute.ForecastCandidate, result.Route);
        Assert.Equal("CASE", result.UomId);
        Assert.True(result.UnitVerificationPending);
    }

    [Fact]
    public void UnknownWeekNeverBecomesForecastInput()
    {
        var result = AcceptedHistoryForecastReadiness.Assess(History(26, 1));
        Assert.Equal(AcceptedHistoryRoute.DataReviewRequired, result.Route);
    }

    [Fact]
    public void MixedAcceptedUnitsRequireReview()
    {
        var history = History(26);
        var weeks = history.Weeks.ToArray();
        weeks[25] = weeks[25] with { UomId = "EA" };
        var result = AcceptedHistoryForecastReadiness.Assess(history with { Weeks = weeks });
        Assert.Equal(AcceptedHistoryRoute.DataReviewRequired, result.Route);
    }
}
