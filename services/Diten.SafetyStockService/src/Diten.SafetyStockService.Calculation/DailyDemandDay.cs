namespace Diten.SafetyStockService.Calculation;

public sealed record DailyDemandDay(
    DateOnly Date,
    CalculationScope? Scope,
    string? BaseUom,
    decimal? DemandQuantity);
