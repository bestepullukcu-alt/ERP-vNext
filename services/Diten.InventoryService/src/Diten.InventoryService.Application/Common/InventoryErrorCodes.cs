namespace Diten.InventoryService.Application.Common;

/// <summary>
/// Every <c>error.code</c> this service can emit. Contract-declared codes, plus exactly the six values
/// ASSUMPTION-0173-04 adds (values of the frozen <c>error.code</c> string, emitted only with declared statuses),
/// plus <see cref="InvalidRequest"/> which pack §12 names for structural field errors, plus
/// <see cref="LegalEntityMasterUnavailable"/> which pack §19 W-0173-02 names (503, company selector).
/// No other code may be added without a new pack version.
/// </summary>
public static class InventoryErrorCodes
{
    // Declared by inventory-bundle.openapi.yaml.
    public const string InvalidMovementType = "INVALID_MOVEMENT_TYPE";
    public const string CrossLeForbidden = "CROSS_LE_FORBIDDEN";
    public const string InsufficientStock = "INSUFFICIENT_STOCK";
    public const string SerialCountMismatch = "SERIAL_COUNT_MISMATCH";
    public const string UnknownLocation = "UNKNOWN_LOCATION";
    public const string ProductMasterUnavailable = "PRODUCT_MASTER_UNAVAILABLE";
    public const string UnknownItem = "UNKNOWN_ITEM";

    // Pack §12 field rules.
    public const string InvalidRequest = "INVALID_REQUEST";

    // ASSUMPTION-0173-04.
    public const string IdempotencyKeyReused = "IDEMPOTENCY_KEY_REUSED";
    public const string SkuItemMismatch = "SKU_ITEM_MISMATCH";
    public const string UomConversionMissing = "UOM_CONVERSION_MISSING";
    public const string LotRequired = "LOT_REQUIRED";
    public const string AlreadyReversed = "ALREADY_REVERSED";
    public const string LocationMasterUnavailable = "LOCATION_MASTER_UNAVAILABLE";

    // Declared by inventory-bundle.openapi.yaml for the S2 reservation operations.
    public const string InsufficientAvailable = "INSUFFICIENT_AVAILABLE";
    public const string ReservationConflict = "RESERVATION_CONFLICT";

    // Pack §19 W-0173-02: MDM could not confirm the header legal entity (fail-closed, nothing read or written).
    public const string LegalEntityMasterUnavailable = "LEGAL_ENTITY_MASTER_UNAVAILABLE";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        InvalidMovementType, CrossLeForbidden, InsufficientStock, SerialCountMismatch, UnknownLocation,
        ProductMasterUnavailable, UnknownItem, InvalidRequest, IdempotencyKeyReused, SkuItemMismatch,
        UomConversionMissing, LotRequired, AlreadyReversed, LocationMasterUnavailable, InsufficientAvailable,
        ReservationConflict, LegalEntityMasterUnavailable
    };
}
