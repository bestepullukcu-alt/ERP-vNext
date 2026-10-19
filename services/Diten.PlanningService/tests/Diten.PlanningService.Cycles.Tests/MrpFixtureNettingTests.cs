using Diten.PlanningService.Application.Features.Mrp;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed class MrpFixtureNettingTests
{
    private static readonly DateOnly Monday = new(2026, 10, 19);

    private static MrpInput Input(params MrpWeek[] weeks) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "EA",
            "published-revision-1", 4, "checksum-1", DateTimeOffset.UtcNow, "approved-safety-1",
            "approved-lead-time-1", 9, 15m, weeks);

    [Fact]
    public void CarriesProjectedSurplus_AndProducesNonnegativeLotForLotQuantity()
    {
        var input = Input(new MrpWeek(Monday, 5m, 5m, MrpWeekQuality.Reliable),
            new MrpWeek(Monday.AddDays(7), 8m, 5m, MrpWeekQuality.Reliable));

        var result = MrpNetting.Calculate(input);

        Assert.Equal([0m, 3m], result.Weeks.Select(w => w.ProposedLotForLotQuantity));
        Assert.Equal([10m, 5m], result.Weeks.Select(w => w.ProjectedClosingStock));
        Assert.True(result.IsDegraded);
        Assert.Equal(MrpNetting.OpenOrdersUnavailable, result.DegradedReason);
        Assert.Equal(input.DemandRevisionId, result.DemandRevisionId);
        Assert.Equal(input.DemandChecksum, result.DemandChecksum);
        Assert.Equal(input.InventoryAsOf, result.InventoryAsOf);
        Assert.Equal(input.SafetyPolicyRevisionId, result.SafetyPolicyRevisionId);
        Assert.Equal(input.LeadTimeRevisionId, result.LeadTimeRevisionId);
    }

    [Fact]
    public void DoesNotTreatMissingWeekAsZeroDemand()
    {
        var input = Input(new MrpWeek(Monday, 1m, 0m, MrpWeekQuality.Reliable),
            new MrpWeek(Monday.AddDays(14), 1m, 0m, MrpWeekQuality.Reliable));
        Assert.Throws<ArgumentException>(() => MrpNetting.Calculate(input));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void RejectsInvalidDemandOrSafety(decimal gross, decimal safety)
    {
        Assert.Throws<ArgumentException>(() => MrpNetting.Calculate(Input(
            new MrpWeek(Monday, gross, safety, MrpWeekQuality.Reliable))));
    }

    [Fact]
    public void MissingSafetyStockSource_IsExplicitlyDegraded()
    {
        var input = Input(new MrpWeek(Monday, 1m, 0m, MrpWeekQuality.Reliable))
            with { SafetyPolicyRevisionId = "" };
        var result = MrpNetting.Calculate(input);
        Assert.Contains(MrpNetting.OpenOrdersUnavailable, result.DegradedReason);
        Assert.Contains(MrpNetting.SafetyStockUnavailable, result.DegradedReason);
        Assert.Equal(0m, result.Weeks[0].ApprovedSafetyStock);
    }

    [Fact]
    public void RejectsOverflowWithoutReturningPartialPlan()
    {
        var input = Input(new MrpWeek(Monday, decimal.MaxValue, 1m, MrpWeekQuality.Reliable));
        Assert.Throws<ArgumentException>(() => MrpNetting.Calculate(input));
    }

    [Theory]
    [InlineData(MrpWeekQuality.Missing)]
    [InlineData(MrpWeekQuality.Unknown)]
    [InlineData(MrpWeekQuality.OutOfStock)]
    public void UnreliableZero_IsNotTrustedDemand(MrpWeekQuality quality)
    {
        var input = Input(new MrpWeek(Monday, 0m, 0m, quality));
        Assert.Throws<ArgumentException>(() => MrpNetting.Calculate(input));
    }
}
