namespace Diten.InventoryService.Application.Common;

/// <summary>
/// Every <c>error.code</c> this service can emit. Contract-declared codes, plus exactly the six values
/// ASSUMPTION-0173-04 adds (values of the frozen <c>error.code</c> string, emitted only with declared statuses),
/// plus <see cref="InvalidRequest"/> which pack §12 names for structural field errors, plus
/// <see cref="LegalEntityMasterUnavailable"/> which pack §19 W-0173-02 names (503, company selector), plus the eight lot /
/// serial codes of the MOD-0174 pack §12 "Ledger posting rules" (WP-0174-P2, all 422; INVENTORY-BUNDLE v1 unchanged).
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

    // MOD-0174 pack §12 "Ledger posting rules" (WP-0174-P2). LOT_REQUIRED and SERIAL_COUNT_MISMATCH above are unchanged.
    public const string LotNotAllowed = "LOT_NOT_ALLOWED";
    public const string UnknownLot = "UNKNOWN_LOT";
    public const string LotSkuMismatch = "LOT_SKU_MISMATCH";
    public const string LotBlocked = "LOT_BLOCKED";
    public const string LotExpired = "LOT_EXPIRED";

    /// <summary>MOD-0175 Q4 (KQ-H, CT-Q3; INVENTORY-BUNDLE 1.0.2): a retest-due lot leaves only by the KQ-H exceptions.</summary>
    public const string LotRetestDue = "LOT_RETEST_DUE";
    public const string SerialAlreadyExists = "SERIAL_ALREADY_EXISTS";
    public const string SerialNotAvailable = "SERIAL_NOT_AVAILABLE";
    public const string SerialLotMismatch = "SERIAL_LOT_MISMATCH";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        InvalidMovementType, CrossLeForbidden, InsufficientStock, SerialCountMismatch, UnknownLocation,
        ProductMasterUnavailable, UnknownItem, InvalidRequest, IdempotencyKeyReused, SkuItemMismatch,
        UomConversionMissing, LotRequired, AlreadyReversed, LocationMasterUnavailable, InsufficientAvailable,
        ReservationConflict, LegalEntityMasterUnavailable, LotNotAllowed, UnknownLot, LotSkuMismatch, LotBlocked, LotExpired,
        SerialAlreadyExists, SerialNotAvailable, SerialLotMismatch, LotRetestDue
    };

    /// <summary>MOD-0175 Q4: the <c>LOT_RETEST_DUE</c> refusal (pre-check and in-transaction re-check give the same answer).</summary>
    public static (string Code, string Message, IReadOnlyDictionary<string, object?> Details) RetestDue(
        Diten.InventoryService.Domain.Traceability.Entities.Lot lot)
        => (LotRetestDue,
            "The lot's retest date has passed: its stock may not be used or come into use — only taking it out of use (status transfer to QUARANTINE / QUALITY_INSPECTION / BLOCKED, moving stock that is out of use), SCRAP, WRITE_OFF and REVERSAL post; a quality decision with a new retest date releases it (KQ-H).",
            new Dictionary<string, object?>
            {
                ["lotId"] = lot.Id.ToString("D"), ["lotNumber"] = lot.LotNumber, ["lotStatus"] = lot.Status.ToString(),
                ["retestDate"] = lot.RetestDate?.UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
            });
}
