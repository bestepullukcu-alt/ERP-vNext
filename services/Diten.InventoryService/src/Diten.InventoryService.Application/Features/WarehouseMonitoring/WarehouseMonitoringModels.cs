using Diten.InventoryService.Application.Features.PutawayTasks;

namespace Diten.InventoryService.Application.Features.WarehouseMonitoring;

// MOD-0178 S3 wire shapes (pack §8.23; WMS draft §9) — read-only, nothing stored. No S3 answer carries a ship-to (Q-10a; the schema test pins it).
// Quantities are decimal strings, times UTC; an empty optional member is omitted — except the two §8.23 members of a ShortDispatchUnresolved
// record's details (stagingDifference, differenceDeferredBy), which are written as null when they do not apply (the pack names the null).

/// <summary>WMS draft §9 <c>WarehouseException { exceptionId, kind, taskId? | outboundId?, code?, details?, openedAtUtc, status }</c> (+ warehouseId).</summary>
public sealed record WarehouseExceptionDto(
    string ExceptionId,
    string Kind,
    string? TaskId,
    string? OutboundId,
    string WarehouseId,
    string? Code,
    IReadOnlyDictionary<string, object?> Details,
    string OpenedAtUtc,
    string Status);

public sealed record WarehouseExceptionPageDto(IReadOnlyList<WarehouseExceptionDto> Items, long Total, int Page, int PageSize,
    string ContractVersion = Warehouse.WarehouseContract.ContractVersion);

/// <summary>One Activity Trace row: <c>source</c> = PutawayTask | PickTask | Outbound | Movement; <c>kind</c> = the timeline kind or the movement type.</summary>
public sealed record WarehouseActivityDto(
    string Source,
    string Kind,
    string AtUtc,
    string RecordId,
    string? RecordNumber,
    int? Seq,
    string? WarehouseId,
    TaskPersonDto? Actor,
    bool System,
    IReadOnlyDictionary<string, string> Data);

public sealed record WarehouseActivityPageDto(IReadOnlyList<WarehouseActivityDto> Items, long Total, int Page, int PageSize, string FromUtc, string ToUtc,
    string ContractVersion = Warehouse.WarehouseContract.ContractVersion);

/// <summary>Card 1 — the <c>OutboundShipmentReady</c> outbox: Pending, Delivered, the oldest Pending's age.</summary>
public sealed record IntegrationEventQueueDto(long Pending, long Delivered, long? OldestPendingAgeSeconds, string? OldestPendingAtUtc);

/// <summary>Card 2 — outbounds waiting in <c>ReadyToShip</c> and the oldest one's <c>ReadyAtUtc</c>.</summary>
public sealed record IntegrationAwaitingDispatchDto(long Count, string? OldestReadyAtUtc);

/// <summary>Card 3 — the last successful dispatch sync (the last <c>Shipped</c> write).</summary>
public sealed record IntegrationLastSyncDto(string? LastShippedAtUtc);

/// <summary>Card 4 — Pending confirmations, putaway + pick.</summary>
public sealed record IntegrationPendingConfirmationsDto(long Putaway, long Pick, long Total);

/// <summary>§8.23 Integration Monitor (r24, son bakış 02 F-9): the four cards; the answer's values are each on exactly one card.</summary>
public sealed record WarehouseIntegrationStatusDto(
    IntegrationEventQueueDto EventQueue,
    IntegrationAwaitingDispatchDto AwaitingDispatch,
    IntegrationLastSyncDto LastSync,
    IntegrationPendingConfirmationsDto PendingConfirmations,
    string AsOfUtc,
    string ContractVersion = Warehouse.WarehouseContract.ContractVersion);
