using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;

namespace Diten.InventoryService.Domain.Warehouse.Repositories;

public sealed record OutboundShipmentQuery(
    IReadOnlyCollection<OutboundStatus> Statuses,
    Guid? WarehouseId,
    string? Search,
    bool OldestFirst,
    int Page,
    int PageSize,
    bool ExcludeItemLevel = false);

public sealed record OutboundShipmentPage(IReadOnlyList<OutboundShipment> Items, long Total);

public sealed record OutboundEventPage(IReadOnlyList<OutboundShipmentEvent> Items, long Total);

/// <summary>The unique indexes of <c>wms_outbound_shipments</c> / <c>wms_outbound_events</c> an outbound write can hit (pack §4.6).</summary>
public enum OutboundUniqueIndex
{
    /// <summary><c>(TenantId, OutboundNumber)</c> — the number is unique tenant-wide.</summary>
    Number,

    /// <summary><c>(TenantId, LegalEntityId, CreatedBy, IdempotencyKey)</c> — the create key of a user.</summary>
    CreateKey,

    /// <summary>An index of <c>wms_outbound_events</c> (outbound + seq, or a key event).</summary>
    Event,

    /// <summary>A unique index of <c>wms_tasks</c> / <c>wms_task_events</c> (a pick task written in transaction B).</summary>
    Task,

    /// <summary>DL-5: a unique index of <c>wms_handling_units</c> (the package number or the create key).</summary>
    Package
}

public sealed class OutboundDuplicateException(OutboundUniqueIndex index, Exception? inner = null) : Exception($"Duplicate on {index}.", inner)
{
    public OutboundUniqueIndex Index { get; } = index;
}

/// <summary>
/// MOD-0178 S2 outbound shipments and their insert-only timeline (pack §4.5, §4.6). Every member is bound to the caller's resolved Tenant +
/// LegalEntity — another company's or tenant's outbound does not exist here (404). The ONE cross-tenant read, the maintenance scan, is not on
/// this port (<see cref="IOutboundMaintenanceScan"/>). Changes go through <see cref="InTransactionAsync"/>, which also writes the pick tasks
/// of transaction B (§8.17 step 5c) in the same transaction.
/// </summary>
public interface IOutboundShipmentRepository
{
    Task<OutboundShipment?> FindAsync(Guid outboundId, CancellationToken cancellationToken = default);

    Task<OutboundShipment?> FindByCreateKeyAsync(Guid createdBy, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<OutboundShipmentPage> ListAsync(OutboundShipmentQuery query, CancellationToken cancellationToken = default);

    Task<OutboundEventPage> EventsAsync(Guid outboundId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<OutboundShipmentEvent?> FindKeyEventAsync(Guid outboundId, Guid actorUserId, string requestOp, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>The company's pick tasks of this outbound (every status).</summary>
    Task<IReadOnlyList<WarehouseTask>> PickTasksAsync(Guid outboundId, CancellationToken cancellationToken = default);

    /// <summary>DL-5: the company's pick tasks of these outbounds (the frozen page reads the picked quantities of a whole page at once).</summary>
    Task<IReadOnlyList<WarehouseTask>> PickTasksOfManyAsync(IReadOnlyCollection<Guid> outboundIds, CancellationToken cancellationToken = default);

    /// <summary>DL-5: the outbound's packages (every status), in number order.</summary>
    Task<IReadOnlyList<HandlingUnit>> PackagesAsync(Guid outboundId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HandlingUnit>> PackagesOfManyAsync(IReadOnlyCollection<Guid> outboundIds, CancellationToken cancellationToken = default);

    Task<HandlingUnit?> FindPackageAsync(Guid packageId, CancellationToken cancellationToken = default);

    Task<HandlingUnit?> FindPackageByCreateKeyAsync(Guid createdBy, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="work"/> in ONE multi-document transaction; the central-audit event is enlisted only when the work changed an
    /// outbound (<see cref="WarehouseWriteOutcome.Affected"/> &gt; 0). A unique-index refusal surfaces as <see cref="OutboundDuplicateException"/>.
    /// </summary>
    Task<WarehouseWriteOutcome> InTransactionAsync(Func<IOutboundWriteSession, CancellationToken, Task<WarehouseWriteOutcome>> work,
        CancellationToken cancellationToken = default);
}

public interface IOutboundWriteSession
{
    Task InsertOutboundAsync(OutboundShipment outbound, CancellationToken cancellationToken = default);

    /// <summary>Replaces the outbound if its stored version is still <paramref name="expectedVersion"/>; false = 0 records (a concurrent write won).</summary>
    Task<bool> ReplaceOutboundAsync(OutboundShipment outbound, int expectedVersion, CancellationToken cancellationToken = default);

    Task InsertEventsAsync(IReadOnlyList<OutboundShipmentEvent> events, CancellationToken cancellationToken = default);

    /// <summary>The pick tasks of transaction B and their <c>Created</c> timeline rows (<c>wms_tasks</c> / <c>wms_task_events</c>).</summary>
    Task InsertPickTasksAsync(IReadOnlyList<WarehouseTask> tasks, IReadOnlyList<WarehouseTaskEvent> events, CancellationToken cancellationToken = default);

    // ── DL-5 ──

    /// <summary>A task of the outbound (a pick task closed by the cancel) — compare-and-set on its version.</summary>
    Task<bool> ReplaceTaskAsync(WarehouseTask task, int expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>Putaway tasks opened by the outbound (<c>Origin = Cancellation</c> / <c>ShortDispatch</c>) and their <c>Created</c> rows.</summary>
    Task InsertTasksAsync(IReadOnlyList<WarehouseTask> tasks, IReadOnlyList<WarehouseTaskEvent> events, CancellationToken cancellationToken = default);

    Task InsertPackageAsync(HandlingUnit package, CancellationToken cancellationToken = default);

    Task<bool> ReplacePackageAsync(HandlingUnit package, int expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>§8.9 / §8.21: the <c>OutboundShipmentReady</c> outbox row, in the same transaction as <c>ReadyToShip</c>.</summary>
    Task InsertOutboxAsync(WarehouseOutboxMessage message, CancellationToken cancellationToken = default);
}

/// <summary>An outbound the maintenance service should complete, with the scope it is processed in (the actor = the outbound's <c>CreatedBy</c>).</summary>
public sealed record OutboundMaintenanceRow(Guid OutboundId, Guid TenantId, Guid LegalEntityId, Guid ActorUserId, string? ActorName);

/// <summary>
/// The maintenance service's scan (pack §8.17 step 6): an EXPLICIT cross-tenant read, used by the hosted service only — every row is then
/// processed in its own Tenant + company scope with the outbound's creator as actor. Read-only.
/// </summary>
public interface IOutboundMaintenanceScan
{
    /// <summary><see cref="OutboundStatus.Allocating"/> outbounds whose recovery lock ended before <paramref name="now"/>, after the cursor, ≤ limit.</summary>
    Task<IReadOnlyList<OutboundMaintenanceRow>> ExpiredAllocationsAsync(DateTimeOffset now, (Guid TenantId, Guid OutboundId)? after, int limit,
        IReadOnlyCollection<Guid>? onlyTenants = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// DL-5 (§8.17 step 6, §8.21, §8.22): <c>AllocationFailed</c> / <c>Shipped</c> / <c>Cancelled</c> outbounds with an allocation whose
    /// reservation release is <c>Pending</c> (<c>(TenantId, "Lines.Allocations.ReservationReleaseState")</c>), after the cursor, ≤ limit.
    /// </summary>
    Task<IReadOnlyList<OutboundMaintenanceRow>> PendingReleasesAsync((Guid TenantId, Guid OutboundId)? after, int limit, IReadOnlyCollection<Guid>? onlyTenants = null,
        CancellationToken cancellationToken = default);
}
