namespace Diten.SafetyStockService.Calculation;

public sealed record DailyDemandFixture(
    string? Version,
    CalculationScope? Scope,
    string? BaseUom,
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<DailyDemandDay>? Days);
