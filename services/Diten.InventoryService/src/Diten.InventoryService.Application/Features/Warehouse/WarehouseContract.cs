namespace Diten.InventoryService.Application.Features.Warehouse;

/// <summary>
/// WMS-BUNDLE v1 (draft <c>docs/analysis/contracts/drafts/wms-bundle-v1-proposal.md</c>, owner MOD-0178) — the constants the DL-1 slice
/// serves: base path, idempotency-key bounds, paging and the WMS <c>operationId</c>s stored as <c>RequestOp</c> on the key events (pack §4.3).
/// </summary>
public static class WarehouseContract
{
    public const string ModuleId = "MOD-0178";

    public const string BasePath = "/api/warehouse";

    public const string ContractVersion = "v1";

    public const int MinIdempotencyKeyLength = 8;
    public const int MaxIdempotencyKeyLength = 100;

    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 200;

    /// <summary>The rows one bulk delete may carry (the list page size).</summary>
    public const int MaxBulkItems = 200;

    // WMS operationIds of the location-role writes (pack §4.3 "RequestOp", §4.6 key-event indexes)
    public const string OpCreateLocationRole = "createLocationRole";
    public const string OpUpdateLocationRole = "updateLocationRole";
    public const string OpDeleteLocationRole = "deleteLocationRole";
    public const string OpBulkDeleteLocationRoles = "bulkDeleteLocationRoles";

    // WMS operationIds of the task writes (pack §4.3 "RequestOp")
    public const string OpCreatePutawayTask = "createPutawayTask";
    public const string OpUpdatePutawayTask = "updatePutawayTask";
    public const string OpClaimPutawayTask = "claimPutawayTask";
    public const string OpBulkAssignPutawayTasks = "bulkAssignPutawayTasks";
    public const string OpConfirmPutawayTask = "confirmPutawayTask";
    public const string OpRetryPutawayConfirmation = "retryPutawayConfirmation";
    public const string OpClosePutawayTaskShort = "closePutawayTaskShort";
    public const string OpCancelPutawayTask = "cancelPutawayTask";
    public const string OpMarkPutawayConfirmationReturned = "markPutawayConfirmationReturned";
    public const string OpResolvePutawayIntegrity = "resolvePutawayIntegrity";

    /// <summary>The rows one bulk assignment may carry (WMS draft §4: ≤ 100).</summary>
    public const int MaxBulkAssignItems = 100;

    // WMS operationIds of the S2 outbound writes (WMS draft §11; pack §4.5 "RequestOp")
    public const string OpCreateOutboundShipment = "createOutboundShipment";
    public const string OpUpdateOutboundShipment = "updateOutboundShipment";

    /// <summary>The maintenance service's operation (no request key; the fingerprint of the FU03 batch is the stored plan's).</summary>
    public const string OpCompleteOutboundAllocation = "completeOutboundAllocation";

    // DL-5 — WMS operationIds of the S2 pick / package / outbound writes (WMS draft §11; pack §4.5 "RequestOp")
    public const string OpCancelOutboundShipment = "cancelOutboundShipment";
    public const string OpReleaseOutboundShipment = "releaseOutboundShipment";
    public const string OpSyncOutboundDispatch = "syncOutboundDispatch";
    public const string OpReleaseOutboundReservations = "releaseOutboundReservations";
    public const string OpClaimPickTask = "claimPickTask";
    public const string OpBulkAssignPickTasks = "bulkAssignPickTasks";
    public const string OpConfirmPickTask = "confirmPickTask";
    public const string OpRetryPickConfirmation = "retryPickConfirmation";
    public const string OpMarkPickConfirmationReturned = "markPickConfirmationReturned";
    public const string OpResolvePickIntegrity = "resolvePickIntegrity";
    public const string OpClosePickTaskShort = "closePickTaskShort";
    public const string OpCreatePackage = "createPackage";
    public const string OpUpdatePackage = "updatePackage";
    public const string OpClosePackage = "closePackage";

    /// <summary>S2 (pack §8.21): the MVP-6 source of an outbound's shipment — anything else is a foreign shipment.</summary>
    public const string DispatchSourceType = "WAREHOUSE_OUTBOUND";

    /// <summary>S2 (pack §8.10, FU03 Q-07): the source module of every 0178 reservation.</summary>
    public const string ReservationSourceModule = ModuleId;

    // bulk-delete row results (WMS draft §4, rule 5)
    public const string RowDeleted = "Deleted";
    public const string RowConflict = "Conflict";
    public const string RowNotFound = "NotFound";

    // bulkAssignPutawayTasks row outcomes besides Conflict / NotFound (pack §12 "toplu atama")
    public const string RowAssigned = "Assigned";
    public const string RowPendingConfirmation = "PendingConfirmation";
    public const string RowInvalidTransition = "InvalidTransition";
}
