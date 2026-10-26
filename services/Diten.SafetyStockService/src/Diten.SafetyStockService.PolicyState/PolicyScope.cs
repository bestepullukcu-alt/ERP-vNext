namespace Diten.SafetyStockService.PolicyState;

public sealed record PolicyScope(
    string TenantId,
    string LegalEntityId,
    string SkuId,
    string WarehouseId);
