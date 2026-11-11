namespace Diten.InventoryService.Application.Features.Warehouse;

/// <summary>
/// The WMS-BUNDLE's own error codes (draft §5.1; pack §12, Q-06). Codes the service already defines — <c>INVALID_REQUEST</c>,
/// <c>IDEMPOTENCY_KEY_REUSED</c>, <c>UNKNOWN_LOCATION</c>, <c>CROSS_LE_FORBIDDEN</c>, <c>LOT_*</c>, … — are used from
/// <c>InventoryErrorCodes</c> and never redefined here (draft §5.2); <c>InventoryErrorCodes</c> itself does not change.
/// </summary>
public static class WarehouseErrorCodes
{
    public const string TaskNotFound = "WMS_TASK_NOT_FOUND";
    public const string LocationRoleNotFound = "WMS_LOCATION_ROLE_NOT_FOUND";
    public const string Conflict = "WMS_CONFLICT";
    public const string InvalidTransition = "WMS_INVALID_TRANSITION";
    public const string ConfirmationPending = "WMS_CONFIRMATION_PENDING";
    public const string ConfirmationConflictRetry = "WMS_CONFIRMATION_CONFLICT_RETRY";
    public const string ConfirmationNotRetryable = "WMS_CONFIRMATION_NOT_RETRYABLE";
    public const string SourceStockNotFound = "WMS_SOURCE_STOCK_NOT_FOUND";
    public const string SourceNotReceiving = "WMS_SOURCE_NOT_RECEIVING";
    public const string StockNotPutawayEligible = "WMS_STOCK_NOT_PUTAWAY_ELIGIBLE";
    public const string QuantityExceedsOpen = "WMS_QUANTITY_EXCEEDS_OPEN";
    public const string QuantityExceedsRemaining = "WMS_QUANTITY_EXCEEDS_REMAINING";
    public const string TargetNotAllowed = "WMS_TARGET_NOT_ALLOWED";
    public const string AssigneeInvalid = "WMS_ASSIGNEE_INVALID";

    /// <summary>Q-13 A: AUTH could not confirm the assignee (no answer, 5xx, 401 / 403) — 503, nothing written.</summary>
    public const string AssigneeDirectoryUnavailable = "WMS_ASSIGNEE_DIRECTORY_UNAVAILABLE";
    public const string LocationRoleExists = "WMS_LOCATION_ROLE_EXISTS";
    public const string LocationRoleConflict = "WMS_LOCATION_ROLE_CONFLICT";
    public const string LocationTypeNotAllowed = "WMS_LOCATION_TYPE_NOT_ALLOWED";
    public const string RecordCodeUnavailable = "WMS_RECORD_CODE_UNAVAILABLE";
    public const string CompanyCodeInvalid = "WMS_COMPANY_CODE_INVALID";
    public const string StorageConditionMismatch = "WMS_STORAGE_CONDITION_MISMATCH";
    public const string StorageConditionsUnknown = "WMS_STORAGE_CONDITIONS_UNKNOWN";
    public const string StorageConditionInvalid = "WMS_STORAGE_CONDITION_INVALID";
    public const string LocationHasOpenTasks = "WMS_LOCATION_HAS_OPEN_TASKS";
    public const string NotAssignee = "WMS_NOT_ASSIGNEE";
    public const string TaskAlreadyAssigned = "WMS_TASK_ALREADY_ASSIGNED";
    public const string QuantityBelowConfirmed = "WMS_QUANTITY_BELOW_CONFIRMED";
    public const string SerialPartialNotSupported = "WMS_SERIAL_PARTIAL_NOT_SUPPORTED";
    public const string SerialBucketChanged = "WMS_SERIAL_BUCKET_CHANGED";
    public const string ConfirmationNotResolvable = "WMS_CONFIRMATION_NOT_RESOLVABLE";
    public const string LocationRoleInheritedConflict = "WMS_LOCATION_ROLE_INHERITED_CONFLICT";
    public const string PostingIntegrityError = "WMS_POSTING_INTEGRITY_ERROR";
    public const string ConfirmationInFlight = "WMS_CONFIRMATION_IN_FLIGHT";
    public const string SerialBucketMixedStatus = "WMS_SERIAL_BUCKET_MIXED_STATUS";

    // S2 — DL-4 (WMS draft §5.1, pack §12 S2 rows)
    public const string AllocationFailed = "WMS_ALLOCATION_FAILED";
    public const string AllocationTooManyLines = "WMS_ALLOCATION_TOO_MANY_LINES";
    public const string OutboundAllocationInProgress = "WMS_OUTBOUND_ALLOCATION_IN_PROGRESS";
    public const string OutboundImmutable = "WMS_OUTBOUND_IMMUTABLE";
    public const string StagingNotConfigured = "WMS_STAGING_NOT_CONFIGURED";
    public const string UomNotBase = "WMS_UOM_NOT_BASE";
    public const string LotNotAllocatable = "WMS_LOT_NOT_ALLOCATABLE";
    public const string ItemNotResolvable = "WMS_ITEM_NOT_RESOLVABLE";
    public const string OutboundLevelMixed = "WMS_OUTBOUND_LEVEL_MIXED";

    // S2 — DL-5 (WMS draft §5.1, pack §12 S2 rows)
    public const string OutboundCancelNotAllowed = "WMS_OUTBOUND_CANCEL_NOT_ALLOWED";
    public const string ItemLevelNotPublishable = "WMS_ITEM_LEVEL_NOT_PUBLISHABLE";
    public const string PackageExceedsPicked = "WMS_PACKAGE_EXCEEDS_PICKED";
    public const string PackageEmpty = "WMS_PACKAGE_EMPTY";
    public const string PackageNotFound = "WMS_PACKAGE_NOT_FOUND";
    public const string DispatchAmbiguous = "WMS_DISPATCH_AMBIGUOUS";
    public const string DispatchReadForbidden = "WMS_DISPATCH_READ_FORBIDDEN";
    public const string DispatchSourceUnavailable = "WMS_DISPATCH_SOURCE_UNAVAILABLE";

    /// <summary>The frozen WAREHOUSE-OUTBOUND 1.0.0 not-found code; the S2 outbound paths use it too (WMS draft §5.1 — no WMS_* twin).</summary>
    public const string UnknownOutbound = "UNKNOWN_OUTBOUND";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        TaskNotFound, LocationRoleNotFound, Conflict, InvalidTransition, ConfirmationPending, ConfirmationConflictRetry, ConfirmationNotRetryable,
        SourceStockNotFound, SourceNotReceiving, StockNotPutawayEligible, QuantityExceedsOpen, QuantityExceedsRemaining, TargetNotAllowed,
        AssigneeInvalid, LocationRoleExists, LocationRoleConflict, LocationTypeNotAllowed, RecordCodeUnavailable, CompanyCodeInvalid,
        StorageConditionMismatch, StorageConditionsUnknown, StorageConditionInvalid, LocationHasOpenTasks, NotAssignee, TaskAlreadyAssigned,
        QuantityBelowConfirmed, SerialPartialNotSupported, SerialBucketChanged, ConfirmationNotResolvable, LocationRoleInheritedConflict,
        PostingIntegrityError, ConfirmationInFlight, SerialBucketMixedStatus,
        AllocationFailed, AllocationTooManyLines, OutboundAllocationInProgress, OutboundImmutable, StagingNotConfigured, UomNotBase, LotNotAllocatable,
        ItemNotResolvable, OutboundLevelMixed, UnknownOutbound,
        OutboundCancelNotAllowed, ItemLevelNotPublishable, PackageExceedsPicked, PackageEmpty, PackageNotFound, DispatchAmbiguous, DispatchReadForbidden,
        DispatchSourceUnavailable
    };
}
