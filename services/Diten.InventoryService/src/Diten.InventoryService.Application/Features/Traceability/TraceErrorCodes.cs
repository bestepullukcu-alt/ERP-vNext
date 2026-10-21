using Diten.InventoryService.Application.Common;

namespace Diten.InventoryService.Application.Features.Traceability;

/// <summary>
/// <c>error.code</c> values of TRACE-BUNDLE (v1 frozen + v1.1, accepted proposal §5). Values of the free v1
/// <c>error.code</c> string — no schema change. Shared MOD-0173 codes are re-used by reference, never re-spelled.
/// </summary>
public static class TraceErrorCodes
{
    // v1
    public const string UnknownLot = "UNKNOWN_LOT";
    public const string LotAlreadyExists = "LOT_ALREADY_EXISTS";

    // v1.1
    public const string LotSkuRequired = "LOT_SKU_REQUIRED";
    public const string LotSkuMismatch = "LOT_SKU_MISMATCH";
    public const string LotNumberInvalid = "LOT_NUMBER_INVALID";
    public const string LotDatesInvalid = "LOT_DATES_INVALID";
    public const string LotKeyImmutable = "LOT_KEY_IMMUTABLE";
    public const string LotNoChanges = "LOT_NO_CHANGES";
    public const string LotConflict = "LOT_CONFLICT";
    public const string LotStatusTransitionInvalid = "LOT_STATUS_TRANSITION_INVALID";
    public const string ReasonRequired = "REASON_REQUIRED";
    public const string AcknowledgementRequired = "ACKNOWLEDGEMENT_REQUIRED";
    public const string ProductNotLotControlled = "PRODUCT_NOT_LOT_CONTROLLED";
    public const string Gs1Unreadable = "GS1_UNREADABLE";
    public const string GenealogySelfLink = "GENEALOGY_SELF_LINK";
    public const string GenealogyCycle = "GENEALOGY_CYCLE";
    public const string UnknownSerial = "UNKNOWN_SERIAL";

    // typed-lot migration (WP-0174-P4, contract commitManualLotMigration x-error-codes)
    public const string MigrationAlreadyCompleted = "MIGRATION_ALREADY_COMPLETED";
    public const string MigrationStale = "MIGRATION_STALE";
    public const string MigrationDecisionsIncomplete = "MIGRATION_DECISIONS_INCOMPLETE";
    public const string MigrationTypoUnresolved = "MIGRATION_TYPO_UNRESOLVED";

    // shared with MOD-0173
    public const string InvalidRequest = InventoryErrorCodes.InvalidRequest;
    public const string IdempotencyKeyReused = InventoryErrorCodes.IdempotencyKeyReused;
    public const string UnknownItem = InventoryErrorCodes.UnknownItem;
    public const string ProductMasterUnavailable = InventoryErrorCodes.ProductMasterUnavailable;

    // TRACE-BUNDLE 1.3.0 (WP-0174-ITEM) — createLot, the same spelling as INVENTORY-BUNDLE 1.2.0
    public const string ProductMasterDataIncomplete = InventoryErrorCodes.ProductMasterDataIncomplete;
    public const string SkuLevelNotStockable = InventoryErrorCodes.SkuLevelNotStockable;

    // TRACE-BUNDLE 1.4.0 (MOD-0176 WP-0176-F2, KF-D) — createLot / Draft updateLot with a date that differs from the suggestion
    public const string LotDateOverrideRequired = "LOT_DATE_OVERRIDE_REQUIRED";
    public const string LotDateOverrideSignatureRequired = "LOT_DATE_OVERRIDE_SIGNATURE_REQUIRED";
    public const string LotDateOverrideSignatureMismatch = "LOT_DATE_OVERRIDE_SIGNATURE_MISMATCH";
    public const string LotDateOverrideMismatch = "LOT_DATE_OVERRIDE_MISMATCH";
    /// <summary>The FEFO-BUNDLE code, answered by TRACE when the override is consumed, expired, another user's or unknown.</summary>
    public const string ExpiryDateOverrideNotUsable = "EXPIRY_DATE_OVERRIDE_NOT_USABLE";
}
