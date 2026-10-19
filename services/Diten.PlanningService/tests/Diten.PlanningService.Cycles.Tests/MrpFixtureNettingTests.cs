using Diten.PlanningService.Application.Features.Mrp;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed class MrpFixtureNettingTests
{
    private static readonly DateOnly Monday = new(2026, 10, 19);

    private static MrpFixtureInput Input(params MrpFixtureWeek[] weeks) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "EA",
            "published-revision-1", 4, "inventory-asof-1", "approved-safety-1",
            "approved-lead-time-1", 9, 15m, weeks);

    [Fact]
    public void CarriesProjectedSurplus_AndProducesNonnegativeLotForLotQuantity()
    {
        var input = Input(new MrpFixtureWeek(Monday, 5m, 5m, MrpFixtureWeekQuality.Reliable),
            new MrpFixtureWeek(Monday.AddDays(7), 8m, 5m, MrpFixtureWeekQuality.Reliable));

        var result = MrpFixtureNetting.Calculate(input);

        Assert.Equal([0m, 3m], result.Weeks.Select(w => w.ProposedLotForLotQuantity));
        Assert.Equal([10m, 5m], result.Weeks.Select(w => w.ProjectedClosingStock));
        Assert.True(result.IsDegraded);
        Assert.Equal(MrpFixtureNetting.OpenOrdersUnavailable, result.DegradedReason);
        Assert.Equal(input.DemandRevisionId, result.DemandRevisionId);
        Assert.Equal(input.InventorySnapshotVersion, result.InventorySnapshotVersion);
        Assert.Equal(input.SafetyPolicyRevisionId, result.SafetyPolicyRevisionId);
        Assert.Equal(input.LeadTimeRevisionId, result.LeadTimeRevisionId);
    }

    [Fact]
    public void DoesNotTreatMissingWeekAsZeroDemand()
    {
        var input = Input(new MrpFixtureWeek(Monday, 1m, 0m, MrpFixtureWeekQuality.Reliable),
            new MrpFixtureWeek(Monday.AddDays(14), 1m, 0m, MrpFixtureWeekQuality.Reliable));
        Assert.Throws<ArgumentException>(() => MrpFixtureNetting.Calculate(input));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void RejectsInvalidDemandOrSafety(decimal gross, decimal safety)
    {
        Assert.Throws<ArgumentException>(() => MrpFixtureNetting.Calculate(Input(
            new MrpFixtureWeek(Monday, gross, safety, MrpFixtureWeekQuality.Reliable))));
    }

    [Fact]
    public void RejectsAbsentSourceVersion()
    {
        var input = Input(new MrpFixtureWeek(Monday, 1m, 0m, MrpFixtureWeekQuality.Reliable))
            with { SafetyPolicyRevisionId = "" };
        Assert.Throws<ArgumentException>(() => MrpFixtureNetting.Calculate(input));
    }

    [Fact]
    public void RejectsOverflowWithoutReturningPartialPlan()
    {
        var input = Input(new MrpFixtureWeek(Monday, decimal.MaxValue, 1m, MrpFixtureWeekQuality.Reliable));
        Assert.Throws<ArgumentException>(() => MrpFixtureNetting.Calculate(input));
    }

    [Theory]
    [InlineData(MrpFixtureWeekQuality.Missing)]
    [InlineData(MrpFixtureWeekQuality.Unknown)]
    [InlineData(MrpFixtureWeekQuality.OutOfStock)]
    public void UnreliableZero_IsNotTrustedDemand(MrpFixtureWeekQuality quality)
    {
        var input = Input(new MrpFixtureWeek(Monday, 0m, 0m, quality));
        Assert.Throws<ArgumentException>(() => MrpFixtureNetting.Calculate(input));
    }
}
