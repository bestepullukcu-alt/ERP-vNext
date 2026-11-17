using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;

namespace Diten.InventoryService.Domain.Warehouse.Repositories;

/// <summary>§8.23 Activity Trace query: the range is mandatory (≤ 31 days — the handler checks), warehouse and source optional, newest first.</summary>
public sealed record WarehouseActivityQuery(DateTimeOffset FromUtc, DateTimeOffset ToUtc, Guid? WarehouseId, WarehouseActivitySource? Source, int Page, int PageSize);

/// <summary>
/// One Activity Trace row: a task timeline row, an outbound timeline row or a <c>sourceModule = MOD-0178</c> INVENTORY movement. <see cref="Data"/>
/// is the row's own text data (a timeline row's <c>Data</c>; a movement's type, quantity, lot and locations) — never a ship-to.
/// </summary>
public sealed record WarehouseActivityRow(
    WarehouseActivitySource Source,
    string Kind,
    DateTimeOffset AtUtc,
    Guid RecordId,
    string? RecordNumber,
    int? Seq,
    Guid? WarehouseId,
    Guid? ActorUserId,
    string? ActorName,
    bool System,
    IReadOnlyDictionary<string, string?> Data);

public sealed record WarehouseActivityPage(IReadOnlyList<WarehouseActivityRow> Items, long Total);

/// <summary>§8.23 Integration Monitor — the six values of the four cards (no stock, no ship-to).</summary>
public sealed record WarehouseIntegrationSnapshot(
    long OutboxPending,
    long OutboxDelivered,
    DateTimeOffset? OldestPendingAtUtc,
    long ReadyToShip,
    DateTimeOffset? OldestReadyAtUtc,
    DateTimeOffset? LastShippedAtUtc,
    long PendingPutawayConfirmations,
    long PendingPickConfirmations);

/// <summary>
/// The S3 monitoring reads of MOD-0178 (pack §8.23) — READ-ONLY, every member bound to the caller's resolved Tenant + LegalEntity first (another
/// company's or tenant's row does not exist here). Nothing is stored for S3: an exception is derived at read time from these rows.
/// </summary>
public interface IWarehouseMonitoringReads
{
    /// <summary>The company's tasks (putaway and pick) with at least one Pending, Refused or IntegrityError confirmation.</summary>
    Task<IReadOnlyList<WarehouseTask>> TasksWithOpenConfirmationsAsync(Guid? warehouseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The company's outbounds that can give an exception: <c>Allocating</c>, <c>Allocated</c> / <c>Picked</c> / <c>Packed</c> / <c>ReadyToShip</c>
    /// (recall, dispatch), <c>Cancelled</c> / <c>Shipped</c> with a Pending release, <c>Shipped</c> with an unresolved short-dispatch row and
    /// <c>AllocationFailed</c> of kind <c>KeyReused</c>.
    /// </summary>
    Task<IReadOnlyList<OutboundShipment>> ExceptionOutboundsAsync(Guid? warehouseId, CancellationToken cancellationToken = default);

    /// <summary>The company's outbounds whose <c>StagingLocationId</c> is one of <paramref name="nodes"/> (every status).</summary>
    Task<IReadOnlyList<OutboundShipment>> OutboundsAtStagingAsync(IReadOnlyCollection<Guid> nodes, CancellationToken cancellationToken = default);

    /// <summary>The company's Activity Trace page (the three sources merged, newest first).</summary>
    Task<WarehouseActivityPage> ActivityAsync(WarehouseActivityQuery query, CancellationToken cancellationToken = default);

    /// <summary>The company's Integration Monitor values.</summary>
    Task<WarehouseIntegrationSnapshot> IntegrationAsync(CancellationToken cancellationToken = default);
}
