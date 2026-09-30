namespace Diten.InventoryService.Application.Common.Upstream;

/// <summary>The fields of PRODUCT-MASTER <c>ProductRef</c> (getProduct) that MOD-0173 needs.</summary>
public sealed record ProductReference(
    string ItemId,
    string? LifecycleStatus,
    string? BaseUomId,
    bool LotControlled,
    bool SerialControlled);
