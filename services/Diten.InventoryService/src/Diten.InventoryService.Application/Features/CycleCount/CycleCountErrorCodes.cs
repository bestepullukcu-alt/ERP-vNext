namespace Diten.InventoryService.Application.Features.CycleCounting;

/// <summary>
/// WMS-COUNT-BUNDLE's own error codes (draft §5.1 — pack §8.14's proposal fixed there). Codes the service already defines
/// (<c>INVALID_REQUEST</c>, <c>IDEMPOTENCY_KEY_REUSED</c>, <c>UNKNOWN_LOCATION</c>, <c>*_MASTER_UNAVAILABLE</c>, …) are used from
/// <c>InventoryErrorCodes</c> and never redefined here (draft §5.2).
/// </summary>
public static class CycleCountErrorCodes
{
    public const string NotFound = "COUNT_NOT_FOUND";
    public const string LineNotFound = "COUNT_LINE_NOT_FOUND";
    public const string Conflict = "COUNT_CONFLICT";
    public const string InvalidState = "COUNT_INVALID_STATE";
    public const string ScopeEmpty = "COUNT_SCOPE_EMPTY";
    public const string ScopeTooLarge = "COUNT_SCOPE_TOO_LARGE";
    public const string ScopeOverlap = "COUNT_SCOPE_OVERLAP";
    public const string ScopeNodeInvalid = "COUNT_SCOPE_NODE_INVALID";
    public const string AssigneeInvalid = "COUNT_ASSIGNEE_INVALID";
    public const string AssigneeDirectoryUnavailable = "COUNT_ASSIGNEE_DIRECTORY_UNAVAILABLE";
    public const string SnapshotDrift = "COUNT_SNAPSHOT_DRIFT";
    public const string LinesUncounted = "COUNT_LINES_UNCOUNTED";
    public const string QuantityInvalid = "COUNT_QUANTITY_INVALID";
    public const string SelfApprovalForbidden = "COUNT_SELF_APPROVAL_FORBIDDEN";
    public const string LotUnknown = "COUNT_LOT_UNKNOWN";
    public const string LotRequired = "COUNT_LOT_REQUIRED";
    public const string SerialNotPostable = "COUNT_SERIAL_NOT_POSTABLE";
    public const string ReasonRequired = "COUNT_REASON_REQUIRED";
    public const string RecountLimit = "COUNT_RECOUNT_LIMIT";
    public const string CompanyCodeInvalid = "COUNT_COMPANY_CODE_INVALID";
    public const string RecordCodeUnavailable = "COUNT_RECORD_CODE_UNAVAILABLE";
    public const string InventoryUnavailable = "COUNT_INVENTORY_UNAVAILABLE";
    public const string AdjustmentIntegrity = "COUNT_ADJUSTMENT_INTEGRITY";
    public const string LineOutOfScope = "COUNT_LINE_OUT_OF_SCOPE";
    public const string LineDuplicate = "COUNT_LINE_DUPLICATE";

    /// <summary>DL-5 (draft §4.4): the Evidence Pack export (or its file) is not the caller's company's — 404 (WMS-COUNT-BUNDLE 1.1.0, additive).</summary>
    public const string EvidenceNotFound = "COUNT_EVIDENCE_NOT_FOUND";

    /// <summary>DL-5: the stored evidence file does not hash to its recorded SHA-256 — never served (500).</summary>
    public const string EvidenceIntegrity = "COUNT_EVIDENCE_INTEGRITY";
}
