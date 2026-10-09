namespace Diten.InventoryService.Application.Common.Upstream;

/// <summary>
/// The fields of PRODUCT-MASTER <c>ProductRef</c> (getProduct) that MOD-0173 needs. <c>CanonicalCode</c> / <c>Name</c> (optional,
/// trailing — MOD-0175 Q5) are display labels only (the WorkCenter task of a quarantine decision); no rule reads them.
/// </summary>
public sealed record ProductReference(
    string ItemId,
    string? LifecycleStatus,
    string? BaseUomId,
    bool LotControlled,
    bool SerialControlled,
    string? CanonicalCode = null,
    string? Name = null);
