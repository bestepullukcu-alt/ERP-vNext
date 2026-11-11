namespace Diten.SafetyStockService.Calculation;

public sealed record MethodARequest(
    DailyDemandFixture? Fixture,
    decimal? CoverageDays);
