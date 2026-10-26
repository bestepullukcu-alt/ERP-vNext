namespace Diten.SafetyStockService.Calculation;

public sealed record MethodATrace(
    string FixtureVersion,
    CalculationScope Scope,
    string BaseUom,
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<DailyDemandDay> DailyInputs,
    decimal TotalDemand,
    int CalendarDayCount,
    decimal AverageDailyDemand,
    decimal CoverageDays,
    decimal RawCandidateQuantity)
{
    public string Formula => "(TotalDemand / CalendarDayCount) * CoverageDays";
}
