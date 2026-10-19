using Diten.SafetyStockService.Calculation;
using Xunit;

namespace Diten.SafetyStockService.Calculation.Tests;

public sealed class MethodACalculatorTests
{
    private static readonly DateOnly StartDate = new(2026, 1, 1);
    private static readonly CalculationScope Scope = new("tenant-1", "legal-1", "sku-1", "warehouse-1");
    private readonly MethodACalculator _calculator = new();

    [Fact]
    public void Calculate_CompleteFixture_ReturnsRawCandidateAndExplanation()
    {
        // Arrange
        var fixture = CreateFixture(2m, 0m, 4m);

        // Act
        var result = _calculator.Calculate(new MethodARequest(fixture, 5m));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(10m, result.CandidateQuantity);
        Assert.Equal("EA", result.BaseUom);
        Assert.Null(result.Error);
        var trace = Assert.IsType<MethodATrace>(result.Trace);
        Assert.Equal("fixture-v1", trace.FixtureVersion);
        Assert.Equal(Scope, trace.Scope);
        Assert.Equal("EA", trace.BaseUom);
        Assert.Equal(StartDate, trace.StartDate);
        Assert.Equal(StartDate.AddDays(2), trace.EndDate);
        Assert.Equal(new decimal?[] { 2m, 0m, 4m }, trace.DailyInputs.Select(day => day.DemandQuantity));
        Assert.Equal(6m, trace.TotalDemand);
        Assert.Equal(3, trace.CalendarDayCount);
        Assert.Equal(2m, trace.AverageDailyDemand);
        Assert.Equal(5m, trace.CoverageDays);
        Assert.Equal(10m, trace.RawCandidateQuantity);
        Assert.Equal("(TotalDemand / CalendarDayCount) * CoverageDays", trace.Formula);
    }

    [Fact]
    public void Calculate_VerifiedZeroDemand_ReturnsSuccessfulZeroCandidate()
    {
        // Arrange
        var fixture = CreateFixture(0m);

        // Act
        var result = _calculator.Calculate(new MethodARequest(fixture, 3m));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0m, result.CandidateQuantity);
        Assert.Equal(1, result.Trace?.CalendarDayCount);
    }

    [Fact]
    public void Calculate_RepeatingDailyAverage_ReturnsExactRawCandidate()
    {
        // Arrange
        var fixture = CreateFixture(1m, 0m, 0m);

        // Act
        var result = _calculator.Calculate(new MethodARequest(fixture, 3m));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1m, result.CandidateQuantity);
        Assert.Equal(1m / 3m, result.Trace?.AverageDailyDemand);
        Assert.Equal(1m, result.Trace?.RawCandidateQuantity);
    }

    [Fact]
    public void Calculate_RepeatingCandidate_UsesDecimalPrecisionWithoutUnitRounding()
    {
        // Arrange
        var fixture = CreateFixture(1m, 0m, 0m);

        // Act
        var result = _calculator.Calculate(new MethodARequest(fixture, 2m));

        // Assert
        Assert.Equal(2m / 3m, result.CandidateQuantity);
        Assert.Equal(1m / 3m, result.Trace?.AverageDailyDemand);
    }

    [Fact]
    public void Calculate_LeapDayWindow_CountsEachIncludedCalendarDay()
    {
        // Arrange
        var first = new DateOnly(2028, 2, 28);
        var days = new[]
        {
            new DailyDemandDay(first, Scope, "EA", 1m),
            new DailyDemandDay(first.AddDays(1), Scope, "EA", 1m),
            new DailyDemandDay(first.AddDays(2), Scope, "EA", 1m)
        };
        var fixture = new DailyDemandFixture("fixture-v1", Scope, "EA", first, first.AddDays(2), days);

        // Act
        var result = _calculator.Calculate(new MethodARequest(fixture, 3m));

        // Assert
        Assert.Equal(3, result.Trace?.CalendarDayCount);
        Assert.Equal(3m, result.CandidateQuantity);
    }

    [Fact]
    public void Calculate_FractionalCoverage_ReturnsUnroundedDecimal()
    {
        // Arrange
        var fixture = CreateFixture(1m, 2m);

        // Act
        var result = _calculator.Calculate(new MethodARequest(fixture, 1.5m));

        // Assert
        Assert.Equal(2.25m, result.CandidateQuantity);
    }

    [Fact]
    public void Calculate_ReorderedCompleteDays_ReturnsSameTraceInCalendarOrder()
    {
        // Arrange
        var fixture = CreateFixture(2m, 0m, 4m);
        var reordered = fixture with { Days = fixture.Days!.Reverse().ToArray() };

        // Act
        var first = _calculator.Calculate(new MethodARequest(fixture, 5m));
        var second = _calculator.Calculate(new MethodARequest(reordered, 5m));

        // Assert
        Assert.Equal(first.CandidateQuantity, second.CandidateQuantity);
        Assert.Equal(
            first.Trace!.DailyInputs.Select(day => day.Date),
            second.Trace!.DailyInputs.Select(day => day.Date));
        Assert.Equal(first.Trace.TotalDemand, second.Trace.TotalDemand);
    }

    [Fact]
    public void Calculate_InputListMutatedAfterReturn_KeepsExplanationSnapshot()
    {
        // Arrange
        var days = CreateFixture(1m).Days!.ToList();
        var fixture = CreateFixture(1m) with { Days = days };

        // Act
        var result = _calculator.Calculate(new MethodARequest(fixture, 2m));
        days.Clear();

        // Assert
        Assert.Single(result.Trace!.DailyInputs);
        Assert.Equal(2m, result.CandidateQuantity);
    }

    [Fact]
    public void Calculate_MissingCalendarDay_ReturnsNoCandidate()
    {
        // Arrange
        var fixture = CreateFixture(1m, 2m, 3m);
        fixture = fixture with { Days = fixture.Days!.Where(day => day.Date != StartDate.AddDays(1)).ToArray() };

        // Act
        var result = _calculator.Calculate(new MethodARequest(fixture, 2m));

        // Assert
        AssertFailure(result, MethodAError.MissingDay);
        Assert.Contains("2026-01-02", result.ErrorDetail);
    }

    [Fact]
    public void Calculate_DuplicateDay_ReturnsNoCandidate()
    {
        // Arrange
        var fixture = CreateFixture(1m, 2m);
        fixture = fixture with { Days = [fixture.Days![0], fixture.Days[0]] };

        // Act
        var result = _calculator.Calculate(new MethodARequest(fixture, 2m));

        // Assert
        AssertFailure(result, MethodAError.DuplicateDay);
    }

    [Fact]
    public void Calculate_DayOutsideWindow_ReturnsNoCandidate()
    {
        // Arrange
        var fixture = CreateFixture(1m);
        fixture = fixture with { Days = [fixture.Days![0] with { Date = StartDate.AddDays(1) }] };

        // Act
        var result = _calculator.Calculate(new MethodARequest(fixture, 2m));

        // Assert
        AssertFailure(result, MethodAError.DayOutsideRange);
    }

    [Theory]
    [InlineData(null, MethodAError.UnknownDemand)]
    [InlineData(-1, MethodAError.NegativeDemand)]
    public void Calculate_UnknownOrNegativeDemand_ReturnsNoCandidate(int? demand, MethodAError expectedError)
    {
        // Arrange
        var fixture = CreateFixture(demand);

        // Act
        var result = _calculator.Calculate(new MethodARequest(fixture, 2m));

        // Assert
        AssertFailure(result, expectedError);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("legal")]
    [InlineData("sku")]
    [InlineData("warehouse")]
    public void Calculate_DayScopeDiffersFromFixture_ReturnsNoCandidate(string dimension)
    {
        // Arrange
        var fixture = CreateFixture(1m);
        var mismatchedScope = dimension switch
        {
            "tenant" => Scope with { TenantId = "tenant-2" },
            "legal" => Scope with { LegalEntityId = "legal-2" },
            "sku" => Scope with { SkuId = "sku-2" },
            _ => Scope with { WarehouseId = "warehouse-2" }
        };
        fixture = fixture with { Days = [fixture.Days![0] with { Scope = mismatchedScope }] };

        // Act
        var result = _calculator.Calculate(new MethodARequest(fixture, 2m));

        // Assert
        AssertFailure(result, MethodAError.ScopeMismatch);
    }

    [Fact]
    public void Calculate_DayUnitDiffersFromFixture_ReturnsNoCandidate()
    {
        // Arrange
        var fixture = CreateFixture(1m);
        fixture = fixture with { Days = [fixture.Days![0] with { BaseUom = "KG" }] };

        // Act
        var result = _calculator.Calculate(new MethodARequest(fixture, 2m));

        // Assert
        AssertFailure(result, MethodAError.BaseUomMismatch);
    }

    [Theory]
    [InlineData(null, MethodAError.MissingCoverageDays)]
    [InlineData(0, MethodAError.InvalidCoverageDays)]
    [InlineData(-1, MethodAError.InvalidCoverageDays)]
    public void Calculate_MissingOrNonPositiveCoverage_ReturnsNoCandidate(int? coverage, MethodAError expectedError)
    {
        // Arrange
        var fixture = CreateFixture(1m);

        // Act
        var result = _calculator.Calculate(new MethodARequest(fixture, coverage));

        // Assert
        AssertFailure(result, expectedError);
    }

    [Fact]
    public void Calculate_ReversedDateRange_ReturnsNoCandidate()
    {
        // Arrange
        var fixture = CreateFixture(1m) with { EndDate = StartDate.AddDays(-1) };

        // Act
        var result = _calculator.Calculate(new MethodARequest(fixture, 2m));

        // Assert
        AssertFailure(result, MethodAError.InvalidDateRange);
    }

    [Fact]
    public void Calculate_UnspecifiedDate_ReturnsNoCandidate()
    {
        // Arrange
        var fixture = CreateFixture(1m) with { StartDate = default };

        // Act
        var result = _calculator.Calculate(new MethodARequest(fixture, 2m));

        // Assert
        AssertFailure(result, MethodAError.InvalidDateRange);
    }

    [Fact]
    public void Calculate_MissingFixtureMetadata_ReturnsNoCandidate()
    {
        // Arrange
        var fixture = CreateFixture(1m) with { Version = " " };

        // Act
        var result = _calculator.Calculate(new MethodARequest(fixture, 2m));

        // Assert
        AssertFailure(result, MethodAError.MissingFixtureVersion);
    }

    [Fact]
    public void Calculate_MissingScopeDimension_ReturnsNoCandidate()
    {
        // Arrange
        var fixture = CreateFixture(1m) with { Scope = Scope with { LegalEntityId = "" } };

        // Act
        var result = _calculator.Calculate(new MethodARequest(fixture, 2m));

        // Assert
        AssertFailure(result, MethodAError.InvalidScope);
    }

    [Fact]
    public void Calculate_MissingBaseUom_ReturnsNoCandidate()
    {
        // Arrange
        var fixture = CreateFixture(1m) with { BaseUom = null };

        // Act
        var result = _calculator.Calculate(new MethodARequest(fixture, 2m));

        // Assert
        AssertFailure(result, MethodAError.MissingBaseUom);
    }

    [Fact]
    public void Calculate_MissingDayList_ReturnsNoCandidate()
    {
        // Arrange
        var fixture = CreateFixture(1m) with { Days = null };

        // Act
        var result = _calculator.Calculate(new MethodARequest(fixture, 2m));

        // Assert
        AssertFailure(result, MethodAError.MissingDays);
    }

    [Fact]
    public void Calculate_NullRequest_ReturnsNoCandidate()
    {
        // Arrange
        MethodARequest? request = null;

        // Act
        var result = _calculator.Calculate(request);

        // Assert
        AssertFailure(result, MethodAError.MissingRequest);
    }

    [Fact]
    public void Calculate_NullFixture_ReturnsNoCandidate()
    {
        // Arrange
        var request = new MethodARequest(null, 2m);

        // Act
        var result = _calculator.Calculate(request);

        // Assert
        AssertFailure(result, MethodAError.MissingFixture);
    }

    [Fact]
    public void Calculate_DailySumOverflows_ReturnsNoCandidate()
    {
        // Arrange
        var fixture = CreateFixture(decimal.MaxValue, 1m);

        // Act
        var result = _calculator.Calculate(new MethodARequest(fixture, 1m));

        // Assert
        AssertFailure(result, MethodAError.ArithmeticOverflow);
    }

    [Fact]
    public void Calculate_CandidateMultiplicationOverflows_ReturnsNoCandidate()
    {
        // Arrange
        var fixture = CreateFixture(decimal.MaxValue);

        // Act
        var result = _calculator.Calculate(new MethodARequest(fixture, 2m));

        // Assert
        AssertFailure(result, MethodAError.ArithmeticOverflow);
    }

    [Fact]
    public void Calculate_LargeIntermediateProductWithRepresentableCandidate_DoesNotOverflow()
    {
        // Arrange
        var fixture = CreateFixture(decimal.MaxValue, 0m);

        // Act
        var result = _calculator.Calculate(new MethodARequest(fixture, 2m));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(decimal.MaxValue, result.CandidateQuantity);
        Assert.Equal(decimal.MaxValue, result.Trace?.RawCandidateQuantity);
    }

    [Fact]
    public void Calculate_PositiveCandidateBelowDecimalPrecision_ReturnsNoCandidate()
    {
        // Arrange
        var smallestPositiveDecimal = 0.0000000000000000000000000001m;
        var fixture = CreateFixture(smallestPositiveDecimal);

        // Act
        var result = _calculator.Calculate(new MethodARequest(fixture, smallestPositiveDecimal));

        // Assert
        AssertFailure(result, MethodAError.ArithmeticOverflow);
    }

    [Fact]
    public void Calculate_LargeRepresentableDecimal_ReturnsRawValue()
    {
        // Arrange
        var fixture = CreateFixture(decimal.MaxValue);

        // Act
        var result = _calculator.Calculate(new MethodARequest(fixture, 1m));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(decimal.MaxValue, result.CandidateQuantity);
    }

    private static DailyDemandFixture CreateFixture(params decimal?[] quantities)
    {
        var days = quantities
            .Select((quantity, index) => new DailyDemandDay(StartDate.AddDays(index), Scope, "EA", quantity))
            .ToArray();

        return new DailyDemandFixture("fixture-v1", Scope, "EA", StartDate, StartDate.AddDays(quantities.Length - 1), days);
    }

    private static void AssertFailure(MethodAResult result, MethodAError expectedError)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedError, result.Error);
        Assert.Null(result.CandidateQuantity);
        Assert.Null(result.Trace);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorDetail));
    }
}
