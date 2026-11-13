namespace Diten.InventoryService.Application.Features.WavePlanning;

/// <summary>
/// WMS-WAVE-BUNDLE v1 (draft <c>docs/analysis/contracts/drafts/wms-wave-bundle-v1-proposal.md</c>, owner MOD-0180) — the constants DL-1
/// serves: module id, idempotency-key bounds, paging and the <c>operationId</c>s stored as <c>RequestOp</c> on the key events (pack §4.3).
/// </summary>
public static class WaveContract
{
    public const string ModuleId = "MOD-0180";

    public const string ContractVersion = "v1";

    public const int MinIdempotencyKeyLength = 8;
    public const int MaxIdempotencyKeyLength = 100;

    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 200;

    /// <summary>The <c>RecordCodeRules</c> type of a wave's number (pack §8.14, Q-4 = A: MOD-0178's temporary counter, type WAV).</summary>
    public const string RecordType = "WAV";

    // WMS-WAVE-BUNDLE operationIds of the DL-1 writes (draft §4.1)
    public const string OpCreateWave = "createWave";
    public const string OpUpdateWave = "updateWave";
    public const string OpAddWaveOrders = "addWaveOrders";
    public const string OpRemoveWaveOrders = "removeWaveOrders";
    public const string OpCancelWave = "cancelWave";
    public const string OpUpdateWaveWarehouseSettings = "updateWaveWarehouseSettings";

    // addWaveOrders / removeWaveOrders row outcomes (draft §3.6)
    public const string RowAdded = "Added";
    public const string RowRemoved = "Removed";
    public const string RowNotMember = "NotMember";

    // error.details.orders[].code of a refused add (draft §3.6)
    public const string OrderNotFound = "NotFound";
    public const string OrderOtherWarehouse = "OtherWarehouse";
    public const string OrderNotAllocated = "NotAllocated";
    public const string OrderNotDeferred = "NotDeferred";
    public const string OrderInOtherWave = "InOtherWave";
    public const string OrderAlreadyInWave = "AlreadyInWave";

    // WaveCandidate.ineligibleReason (draft §3.5)
    public const string AllocationPending = "AllocationPending";

    // listWaveSuggestions.limitedBy (draft §4.1)
    public const string LimitedByMaxOrders = "MaxOrders";
    public const string LimitedByMaxLines = "MaxLines";
    public const string LimitedByDefault = "Default";
}
