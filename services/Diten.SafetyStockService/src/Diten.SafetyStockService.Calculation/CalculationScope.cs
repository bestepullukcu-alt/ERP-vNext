namespace Diten.SafetyStockService.Calculation;

public sealed record CalculationScope(
    string TenantId,
    string LegalEntityId,
    string SkuId,
    string WarehouseId);
