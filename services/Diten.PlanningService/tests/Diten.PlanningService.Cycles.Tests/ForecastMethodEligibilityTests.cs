using Eligibility = Diten.PlanningService.Application.Features.DemandPlanning.ForecastMethodEligibility;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed class ForecastMethodEligibilityTests
{
    private static readonly DateOnly Start = new(2025, 1, 6);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GeneralMethod_RequiresTwentySixVerifiedWeeks(int methodCode)
    {
        var method = (Eligibility.Method)methodCode;
        Assert.Equal(Eligibility.EligibilityReason.InsufficientReliableWeeks,
            Eligibility.Assess(Fixture(25), method, Policy()).Reason);

        var result = Eligibility.Assess(Fixture(26), method, Policy());

        Assert.True(result.IsEligible);
        Assert.Equal(26, result.ReliableWeeks);
    }

    [Fact]
    public void SeasonalNaive_RequiresOneHundredFourVerifiedWeeks()
    {
        Assert.Equal(Eligibility.EligibilityReason.InsufficientReliableWeeks,
            Eligibility.Assess(Fixture(103), Eligibility.Method.SeasonalNaive, Policy()).Reason);
        Assert.True(Eligibility.Assess(Fixture(104),
            Eligibility.Method.SeasonalNaive, Policy()).IsEligible);
    }

    [Fact]
    public void Croston_RequiresFourDistinctPositiveDemandEvents()
    {
        var three = Eligibility.Assess(Fixture(26, 3),
            Eligibility.Method.BiasCorrectedCroston, Policy());
        var four = Eligibility.Assess(Fixture(26, 4),
            Eligibility.Method.BiasCorrectedCroston, Policy());

        Assert.Equal(Eligibility.EligibilityReason.InsufficientRealDemandEvents, three.Reason);
        Assert.Equal(3, three.RealDemandEvents);
        Assert.True(four.IsEligible);
        Assert.Equal(4, four.RealDemandEvents);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void UntrustedWeek_CannotBecomeReliableZero(int evidenceCode)
    {
        var evidence = (Eligibility.WeekEvidence)evidenceCode;
        var result = Eligibility.Assess(Fixture(26, exceptionalWeek: evidence),
            Eligibility.Method.Naive, Policy());

        Assert.False(result.IsEligible);
        Assert.Equal(25, result.ReliableWeeks);
        Assert.Equal(Eligibility.EligibilityReason.IncompleteOrUntrustedHistory, result.Reason);
    }

    [Fact]
    public void VerifiedZeroDemand_CountsAsReliableButNotAsDemandEvent()
    {
        var history = Fixture(26);

        Assert.True(Eligibility.Assess(history, Eligibility.Method.Naive, Policy()).IsEligible);
        var intermittent = Eligibility.Assess(history,
            Eligibility.Method.BiasCorrectedCroston, Policy());
        Assert.Equal(26, intermittent.ReliableWeeks);
        Assert.Equal(0, intermittent.RealDemandEvents);
        Assert.False(intermittent.IsEligible);
    }

    [Fact]
    public void DifferentSeries_DoNotShareDemandEventsOrEligibility()
    {
        var first = Key();
        var second = Key();

        var eligible = Eligibility.Assess(Fixture(26, 4, first),
            Eligibility.Method.BiasCorrectedCroston, Policy());
        var ineligible = Eligibility.Assess(Fixture(26, 3, second),
            Eligibility.Method.BiasCorrectedCroston, Policy());

        Assert.Equal(first, eligible.Series);
        Assert.Equal(second, ineligible.Series);
        Assert.True(eligible.IsEligible);
        Assert.False(ineligible.IsEligible);
    }

    [Fact]
    public void PolicyVersionAndThresholdChange_AffectOnlyTheirOwnAssessment()
    {
        var history = Fixture(26);
        var first = Eligibility.Assess(history, Eligibility.Method.Naive,
            Eligibility.FixturePolicy.Mvp("fixture-policy-v1"));
        var second = Eligibility.Assess(history, Eligibility.Method.Naive,
            new Eligibility.FixturePolicy("fixture-policy-v2", 27, 104, 26, 4));

        Assert.Equal("fixture-policy-v1", first.PolicyVersion);
        Assert.Equal("fixture-policy-v2", second.PolicyVersion);
        Assert.True(first.IsEligible);
        Assert.False(second.IsEligible);
        Assert.Equal(Eligibility.EligibilityReason.InsufficientReliableWeeks, second.Reason);
        Assert.Null(typeof(Eligibility.Decision).GetProperty("Selected"));
        Assert.Null(typeof(Eligibility.Decision).GetProperty("Winner"));
    }

    [Fact]
    public void HoltWinters_RequiresOneHundredFourVerifiedWeeks()
    {
        var shortHistory = Eligibility.Assess(Fixture(103),
            Eligibility.Method.HoltWinters, Policy());
        var completeHistory = Eligibility.Assess(Fixture(104),
            Eligibility.Method.HoltWinters, Policy());

        Assert.False(shortHistory.IsEligible);
        Assert.Equal(Eligibility.EligibilityReason.InsufficientReliableWeeks,
            shortHistory.Reason);
        Assert.True(completeHistory.IsEligible);
        Assert.Equal(104, completeHistory.ReliableWeeks);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void HoltWinters_RejectsIncompleteOrUntrustedWeek(int evidenceCode)
    {
        var history = Fixture(104,
            exceptionalWeek: (Eligibility.WeekEvidence)evidenceCode);

        var result = Eligibility.Assess(history,
            Eligibility.Method.HoltWinters, Policy());

        Assert.False(result.IsEligible);
        Assert.Equal(103, result.ReliableWeeks);
        Assert.Equal(Eligibility.EligibilityReason.IncompleteOrUntrustedHistory,
            result.Reason);
    }

    [Fact]
    public void Fixture_RejectsEventFromAnotherSeries()
    {
        var series = Key();
        var other = Key();
        var week = new Eligibility.FixtureWeek(Start,
            Eligibility.WeekEvidence.VerifiedDemand,
            [new Eligibility.FixtureDemandEvent(Guid.NewGuid(), other, Start, 1m)]);

        Assert.Throws<ArgumentException>(() =>
            new Eligibility.VerifiedHistoryFixture(series, [week]));
    }

    [Fact]
    public void Fixture_RejectsDuplicateEventInsteadOfCountingItTwice()
    {
        var series = Key();
        var eventId = Guid.NewGuid();
        var weeks = new[]
        {
            new Eligibility.FixtureWeek(Start, Eligibility.WeekEvidence.VerifiedDemand,
                [new Eligibility.FixtureDemandEvent(eventId, series, Start, 1m)]),
            new Eligibility.FixtureWeek(Start.AddDays(7), Eligibility.WeekEvidence.VerifiedDemand,
                [new Eligibility.FixtureDemandEvent(eventId, series, Start.AddDays(7), 1m)])
        };

        Assert.Throws<ArgumentException>(() =>
            new Eligibility.VerifiedHistoryFixture(series, weeks));
    }

    [Fact]
    public void Fixture_CopiesWeekAndEventListsBeforeAssessment()
    {
        var series = Key();
        var events = new List<Eligibility.FixtureDemandEvent>
        {
            new(Guid.NewGuid(), series, Start, 1m)
        };
        var weeks = new List<Eligibility.FixtureWeek>
        {
            new(Start, Eligibility.WeekEvidence.VerifiedDemand, events)
        };
        var history = new Eligibility.VerifiedHistoryFixture(series, weeks);

        events.Clear();
        weeks.Clear();

        Assert.Single(history.Weeks);
        Assert.Single(history.Weeks[0].Events);
    }

    private static Eligibility.FixturePolicy Policy() =>
        Eligibility.FixturePolicy.Mvp("fixture-policy-v1");

    private static Eligibility.SeriesKey Key() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "warehouse-1");

    private static Eligibility.VerifiedHistoryFixture Fixture(int weekCount,
        int demandEvents = 0, Eligibility.SeriesKey? series = null,
        Eligibility.WeekEvidence? exceptionalWeek = null)
    {
        series ??= Key();
        var weeks = new List<Eligibility.FixtureWeek>();
        for (var index = 0; index < weekCount; index++)
        {
            var start = Start.AddDays(index * 7);
            if (exceptionalWeek.HasValue && index == weekCount - 1)
            {
                weeks.Add(new Eligibility.FixtureWeek(start, exceptionalWeek.Value,
                    Array.Empty<Eligibility.FixtureDemandEvent>()));
                continue;
            }

            var hasEvent = index < demandEvents;
            var events = hasEvent
                ? new[] { new Eligibility.FixtureDemandEvent(Guid.NewGuid(), series, start, 1m) }
                : Array.Empty<Eligibility.FixtureDemandEvent>();
            weeks.Add(new Eligibility.FixtureWeek(start,
                hasEvent ? Eligibility.WeekEvidence.VerifiedDemand :
                    Eligibility.WeekEvidence.VerifiedZeroDemand, events));
        }

        return new Eligibility.VerifiedHistoryFixture(series, weeks);
    }
}
