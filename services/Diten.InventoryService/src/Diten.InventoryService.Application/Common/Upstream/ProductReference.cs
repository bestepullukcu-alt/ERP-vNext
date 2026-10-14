namespace Diten.InventoryService.Application.Common.Upstream;

/// <summary>
/// The fields of PRODUCT-MASTER <c>ProductRef</c> (getProduct) that MOD-0173 needs. <c>CanonicalCode</c> / <c>Name</c> (optional,
/// trailing — MOD-0175 Q5) are display labels only (the WorkCenter task of a quarantine decision); no rule reads them.
/// <para>WP-0173-PM1 (PRODUCT-MASTER v1.1, OD-1): <c>LotControlled</c> / <c>SerialControlled</c> are the DEPRECATED product flags,
/// nullable (absent / null / not a boolean = unknown, never false). No rule reads the lot flag here: the SKU's flags
/// (<see cref="SkuReference"/>) are authoritative. The serial flag is read only for a GSKU movement (OD-10).</para>
/// </summary>
public sealed record ProductReference(
    string ItemId,
    string? LifecycleStatus,
    string? BaseUomId,
    bool? LotControlled,
    bool? SerialControlled,
    string? CanonicalCode = null,
    string? Name = null);
