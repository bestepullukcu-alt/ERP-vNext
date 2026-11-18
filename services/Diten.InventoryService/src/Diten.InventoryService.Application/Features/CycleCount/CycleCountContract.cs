namespace Diten.InventoryService.Application.Features.CycleCounting;

/// <summary>
/// WMS-COUNT-BUNDLE v1 (draft <c>docs/analysis/contracts/drafts/wms-count-bundle-v1-proposal.md</c>, owner MOD-0181) — the constants DL-1
/// serves: module id, idempotency-key bounds, paging and the <c>operationId</c>s stored as <c>RequestOp</c> on the key events (pack §4.3).
/// </summary>
public static class CycleCountContract
{
    public const string ModuleId = "MOD-0181";

    public const string ContractVersion = "v1";

    public const int MinIdempotencyKeyLength = 8;
    public const int MaxIdempotencyKeyLength = 100;

    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 200;

    /// <summary>The rows one bulk delete may carry (the list page size).</summary>
    public const int MaxBulkItems = 200;

    /// <summary>The <c>RecordCodeRules</c> type of a count's number (pack §8.12, Q-7 = B: MOD-0178's temporary counter, type CNT).</summary>
    public const string RecordType = "CNT";

    // WMS-COUNT-BUNDLE operationIds of the DL-1 writes (draft §4.1)
    public const string OpCreateCycleCount = "createCycleCount";
    public const string OpUpdateCycleCount = "updateCycleCount";
    public const string OpBulkDeleteCycleCounts = "bulkDeleteCycleCounts";
    public const string OpReleaseCycleCount = "releaseCycleCount";
    public const string OpCancelCycleCount = "cancelCycleCount";

    // DL-2 writes (draft §4.2 → binding with DL-2)
    public const string OpAddCycleCountLine = "addCycleCountLine";
    public const string OpRecordCycleCountLine = "recordCycleCountLine";
    public const string OpSubmitCycleCount = "submitCycleCount";

    // DL-3 writes (draft §4.3 → binding with DL-3)
    public const string OpApproveCountVariance = "approveCountVariance";
    public const string OpRejectCountVariance = "rejectCountVariance";
    public const string OpRequestCountRecount = "requestCountRecount";
    public const string OpRetryCountAdjustment = "retryCountAdjustment";
    public const string OpResolveCountAdjustmentIntegrity = "resolveCountAdjustmentIntegrity";
    public const string OpUpdateCycleCountSettings = "updateCycleCountSettings";

    // DL-5 (draft §4.4, binding in DL-5; WMS-COUNT-BUNDLE 1.1.0): the Evidence Pack write. Its key lives on the export record (not a count event).
    public const string OpCreateCountEvidenceExport = "createCountEvidenceExport";

    /// <summary>Pack §8.5: a recount selects 1..200 lines.</summary>
    public const int MaxRecountLines = 200;

    /// <summary>Pack §2.4 / §12: a manual / all Variance Trace read spans at most 31 days.</summary>
    public const int MaxManualWindowDays = 31;

    // Variance Trace sources (pack §2.4)
    public const string SourceCount = "count";
    public const string SourceManual = "manual";
    public const string SourceAll = "all";
    public const string RowSourceCount = "Count";
    public const string RowSourceManual = "Manual";

    /// <summary>The opaque scan reference's bound (MOD-0182 fills it; v1 stores it unchecked — Q-12 = A).</summary>
    public const int MaxScanRefLength = 200;

    /// <summary>A line write retries its compare-and-set on the count head this many times before 409 (concurrent entries on other lines).</summary>
    public const int MaxHeadAttempts = 5;

    // bulk-delete row outcomes (draft §3.5)
    public const string RowDeleted = "Deleted";
    public const string RowConflict = "Conflict";
    public const string RowNotFound = "NotFound";
    public const string RowInvalidState = "InvalidState";
}
