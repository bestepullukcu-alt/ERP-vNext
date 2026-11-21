using System.Globalization;
using Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed class HistoryUnitConversionTests
{
    [Theory]
    [InlineData("1", "1", "3", "0.3333")]
    [InlineData("3", "1", "3", "1")]
    [InlineData("1", "1", "20000", "0.0001")]
    [InlineData("1", "1", "40000", "0")]
    [InlineData("1", "10", "1", "10")]
    public void ConvertsWithFourPlaceAwayFromZeroRounding(string quantity, string numerator,
        string denominator, string expected)
    {
        var converted = HistoryUnitConversion.ToBaseQuantity(
            decimal.Parse(quantity, CultureInfo.InvariantCulture),
            decimal.Parse(numerator, CultureInfo.InvariantCulture),
            decimal.Parse(denominator, CultureInfo.InvariantCulture));
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), converted);
    }

    [Fact]
    public void DoesNotOverflowAnIntermediateProductThatCancels()
    {
        Assert.Equal(decimal.MaxValue,
            HistoryUnitConversion.ToBaseQuantity(decimal.MaxValue, 10m, 10m));
    }
}
