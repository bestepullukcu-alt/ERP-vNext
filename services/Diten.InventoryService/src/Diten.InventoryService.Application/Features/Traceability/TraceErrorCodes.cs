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

    // shared with MOD-0173
    public const string InvalidRequest = InventoryErrorCodes.InvalidRequest;
    public const string IdempotencyKeyReused = InventoryErrorCodes.IdempotencyKeyReused;
    public const string UnknownItem = InventoryErrorCodes.UnknownItem;
    public const string ProductMasterUnavailable = InventoryErrorCodes.ProductMasterUnavailable;
}
