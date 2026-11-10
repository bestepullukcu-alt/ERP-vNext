namespace Diten.InventoryService.Domain.Warehouse.Enums;

// MOD-0178 S3 monitoring enums (pack §8.23; WMS draft §9). Nothing here is stored: an exception is DERIVED at read time.

/// <summary>
/// §8.23 — the nine kinds a warehouse exception has in v1 (WMS draft §9 names). <c>OutboundDrift</c> and <c>CancelAfterReady</c> are reserved names
/// and are never produced in v1 (no change and no cancel after <c>ReadyToShip</c>; Q-M6-7).
/// </summary>
public enum WarehouseExceptionKind
{
    PostingRefused,
    PostingPendingLong,
    PostingIntegrityError,
    DispatchNotSeen,
    ReservationReleasePending,
    ShortDispatchUnresolved,
    AllocationOrphanSuspected,
    ReservedLotUnderRecall,
    AllocationStuck
}

/// <summary>
/// §8.23 (r25, son bakış 03 F-1 (b)) — why the Staging difference of a <c>ShortDispatchUnresolved</c> row was NOT computed (the row stays listed):
/// a Pending confirmation into or out of the node for the SKU, or a not yet synced <c>ReadyToShip</c> outbound on the node for the SKU (if both,
/// the first).
/// </summary>
public enum StagingDifferenceDeferral
{
    PendingConfirmation,
    UnsyncedReadyToShip
}

/// <summary>§8.23 Activity Trace — where an activity row comes from (the <c>kind</c> filter of <c>GET /activity</c>).</summary>
public enum WarehouseActivitySource
{
    PutawayTask,
    PickTask,
    Outbound,
    Movement
}
