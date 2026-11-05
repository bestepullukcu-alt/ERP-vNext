namespace Diten.InventoryService.Domain.Warehouse.Enums;

// MOD-0178 S2 outbound enums (pack §4.5, §8.18; WMS draft §6.2). Stored by name (InventoryBsonMappings' Domain convention).

/// <summary>
/// §8.18 — <see cref="Allocating"/> and <see cref="AllocationFailed"/> are INTERNAL: never on the frozen WAREHOUSE-OUTBOUND reads nor on the
/// screen (WMS draft §2.1 "there is no draft outbound"); the other six are WAREHOUSE-OUTBOUND 1.0.0's <c>OutboundStatus</c>.
/// </summary>
public enum OutboundStatus
{
    Allocating,
    AllocationFailed,
    Allocated,
    Picked,
    Packed,
    ReadyToShip,
    Shipped,
    Cancelled
}

/// <summary>§4.5 — the kinds of the insert-only outbound timeline <c>wms_outbound_events</c>.</summary>
public enum OutboundEventKind
{
    Created,
    AllocationFailed,
    Allocated,
    Updated,
    PickCompleted,
    PackageCreated,
    PackageUpdated,
    PackageClosed,
    PackageCancelled,
    Packed,
    Released,
    Shipped,
    ReservationsReleased,
    ReservationReleaseFailed,
    Cancelled
}

/// <summary>§4.5 <c>ReservationReleaseState</c> of one allocation (§8.17 step 6, §8.21, §8.22).</summary>
public enum ReservationReleaseState
{
    None,
    Pending,
    Released
}

/// <summary>§4.5 <c>HandlingUnit.Status</c> (DL-5, §8.20): only an <see cref="Open"/> package changes; a cancelled outbound cancels its packages.</summary>
public enum HandlingUnitStatus
{
    Open,
    Closed,
    Cancelled
}

/// <summary>§4.5 <c>AllocationFailure.kind</c> (r24): the FU03 batch refusal, or the FU03 400 <c>IDEMPOTENCY_KEY_REUSED</c> branch.</summary>
public enum AllocationFailureKind
{
    BatchRefused,
    KeyReused
}
