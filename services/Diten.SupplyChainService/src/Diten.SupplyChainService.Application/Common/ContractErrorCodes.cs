namespace Diten.SupplyChainService.Application.Common;

/// <summary>
/// Every error code the SHIPMENT-BUNDLE contract declares, and nothing else.
/// </summary>
/// <remarks>
/// Q262 (2026-10-03). MOD-0183 pack §22 ("Commands, queries, event/correlation behavior and error codes are
/// frozen in the contract") makes <c>docs/analysis/contracts/shipment-bundle.openapi.yaml</c> the single source
/// of these strings, yet nothing in code referenced it: codes were written as literals at each emission site.
/// C-02 showed the cost is not theoretical — a 500 was emitted carrying <c>INVALID_REQUEST</c>, a code the
/// contract reserves for 400, and <c>SHIPMENT_ROOT_INVALID</c> was emitted although the contract declares it
/// nowhere. The UI's lookup returned <c>undefined</c> and the user saw no message on a server failure.
/// <para>
/// The 52 constants below were extracted from the contract, not authored. To re-extract and compare:
/// <code>
/// python3 -c "import re;print(sorted({m.group(1) for m in (re.search(r'code:\s*([A-Z_]+)',l) for l in open('docs/analysis/contracts/shipment-bundle.openapi.yaml')) if m}))"
/// </code>
/// Per K7 the command is kept here rather than only the count, because the count drifts and the command does not.
/// </para>
/// <para>
/// A code absent from this type is absent from the contract and must not be emitted. Adding one here without
/// adding it to the contract inverts the authority the pack sets, so the contract changes first.
/// <c>docs/analysis/**</c> is deliberately read-only to this work package.
/// </para>
/// </remarks>
public static class ContractErrorCodes
{
    // Envelope and cross-cutting
    public const string InvalidRequest = "INVALID_REQUEST";
    public const string InternalError = "INTERNAL_ERROR";
    public const string Unauthenticated = "UNAUTHENTICATED";
    public const string Forbidden = "FORBIDDEN";
    public const string UnsupportedMediaType = "UNSUPPORTED_MEDIA_TYPE";
    public const string PersistenceUnavailable = "PERSISTENCE_UNAVAILABLE";
    public const string IdempotencyKeyReuse = "IDEMPOTENCY_KEY_REUSED";
    public const string CorrelationRootMismatch = "CORRELATION_ROOT_MISMATCH";
    public const string DependencyUnavailable = "DEPENDENCY_UNAVAILABLE";
    public const string DependencyResponseInvalid = "DEPENDENCY_RESPONSE_INVALID";
    public const string ReferenceStateUnavailable = "REFERENCE_STATE_UNAVAILABLE";

    // Shipment and proof of delivery
    public const string ShipmentNotFound = "SHIPMENT_NOT_FOUND";
    public const string ShipmentLineNotFound = "SHIPMENT_LINE_NOT_FOUND";
    public const string InvalidShipmentTransition = "INVALID_SHIPMENT_TRANSITION";
    public const string DuplicateShipment = "DUPLICATE_SHIPMENT";
    public const string ShipmentNotEligible = "SHIPMENT_NOT_ELIGIBLE";
    public const string ShipmentNotReturnable = "SHIPMENT_NOT_RETURNABLE";
    public const string ShipmentAlreadyAssigned = "SHIPMENT_ALREADY_ASSIGNED";
    public const string ShipmentCarrierMismatch = "SHIPMENT_CARRIER_MISMATCH";
    public const string PodAlreadyCaptured = "POD_ALREADY_CAPTURED";
    public const string WarehouseShipmentTriggerUnavailable = "WAREHOUSE_SHIPMENT_TRIGGER_UNAVAILABLE";

    // Carrier
    public const string CarrierNotFound = "CARRIER_NOT_FOUND";
    public const string CarrierCodeConflict = "CARRIER_CODE_CONFLICT";
    public const string CarrierModeUnsupported = "CARRIER_MODE_UNSUPPORTED";
    public const string InvalidCarrierTransition = "INVALID_CARRIER_TRANSITION";

    // Load
    public const string LoadNotFound = "LOAD_NOT_FOUND";
    public const string InvalidLoadTransition = "INVALID_LOAD_TRANSITION";
    public const string InvalidLoadStops = "INVALID_LOAD_STOPS";

    // Return
    public const string ReturnNotFound = "RETURN_NOT_FOUND";
    public const string InvalidReturnTransition = "INVALID_RETURN_TRANSITION";
    public const string InvalidReturnQuantity = "INVALID_RETURN_QUANTITY";
    public const string ReturnQuantityExceeded = "RETURN_QUANTITY_EXCEEDED";
    public const string ReturnUomMismatch = "RETURN_UOM_MISMATCH";
    public const string ReturnSourceChanged = "RETURN_SOURCE_CHANGED";
    public const string DuplicateReturnLine = "DUPLICATE_RETURN_LINE";
    public const string DispositionRequired = "DISPOSITION_REQUIRED";
    public const string ReturnShipmentRootInvalid = "RETURN_SHIPMENT_ROOT_INVALID";
    public const string ReturnShipmentRootUnavailable = "RETURN_SHIPMENT_ROOT_UNAVAILABLE";

    // Claim
    public const string ClaimNotFound = "CLAIM_NOT_FOUND";
    public const string InvalidClaimTransition = "INVALID_CLAIM_TRANSITION";
    public const string ClaimAmountInvalid = "CLAIM_AMOUNT_INVALID";
    public const string ClaimApprovalAmountInvalid = "CLAIM_APPROVAL_AMOUNT_INVALID";
    public const string ClaimApprovedAmountNotAllowed = "CLAIM_APPROVED_AMOUNT_NOT_ALLOWED";
    public const string ClaimCarrierMismatch = "CLAIM_CARRIER_MISMATCH";
    public const string ClaimCorrelationMismatch = "CLAIM_CORRELATION_MISMATCH";
    public const string ClaimShipmentIneligible = "CLAIM_SHIPMENT_INELIGIBLE";
    public const string ClaimReferenceInvalid = "CLAIM_REFERENCE_INVALID";
    public const string ClaimReferenceIncomplete = "CLAIM_REFERENCE_INCOMPLETE";
    public const string ClaimReferenceUnavailable = "CLAIM_REFERENCE_UNAVAILABLE";
    public const string ClaimStorageUnavailable = "CLAIM_STORAGE_UNAVAILABLE";

    // Inventory seam
    public const string InventoryUnavailable = "INVENTORY_UNAVAILABLE";
    public const string InventoryReferenceInvalid = "INVENTORY_REFERENCE_INVALID";

    /// <summary>Every declared code, for a drift check against the contract.</summary>
    public static readonly string[] All =
    [
        InvalidRequest, InternalError, Unauthenticated, Forbidden, UnsupportedMediaType, PersistenceUnavailable,
        IdempotencyKeyReuse, CorrelationRootMismatch, DependencyUnavailable, DependencyResponseInvalid,
        ReferenceStateUnavailable, ShipmentNotFound, ShipmentLineNotFound, InvalidShipmentTransition,
        DuplicateShipment, ShipmentNotEligible, ShipmentNotReturnable, ShipmentAlreadyAssigned,
        ShipmentCarrierMismatch, PodAlreadyCaptured, WarehouseShipmentTriggerUnavailable, CarrierNotFound,
        CarrierCodeConflict, CarrierModeUnsupported, InvalidCarrierTransition, LoadNotFound,
        InvalidLoadTransition, InvalidLoadStops, ReturnNotFound, InvalidReturnTransition, InvalidReturnQuantity,
        ReturnQuantityExceeded, ReturnUomMismatch, ReturnSourceChanged, DuplicateReturnLine, DispositionRequired,
        ReturnShipmentRootInvalid, ReturnShipmentRootUnavailable, ClaimNotFound, InvalidClaimTransition,
        ClaimAmountInvalid, ClaimApprovalAmountInvalid, ClaimApprovedAmountNotAllowed, ClaimCarrierMismatch,
        ClaimCorrelationMismatch, ClaimShipmentIneligible, ClaimReferenceInvalid, ClaimReferenceIncomplete,
        ClaimReferenceUnavailable, ClaimStorageUnavailable, InventoryUnavailable, InventoryReferenceInvalid,
    ];
}
