using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;

namespace Diten.InventoryService.Domain.Warehouse.Repositories;

public sealed record WarehouseTaskQuery(
    IReadOnlyCollection<WarehouseTaskStatus> Statuses,
    Guid? WarehouseId,
    Guid? AssigneeUserId,
    WarehouseTaskPriority? Priority,
    string? Search,
    bool OldestFirst,
    int Page,
    int PageSize);

public sealed record WarehouseTaskPage(IReadOnlyList<WarehouseTask> Items, long Total);

public sealed record WarehouseTaskEventPage(IReadOnlyList<WarehouseTaskEvent> Items, long Total);

/// <summary>The unique indexes of <c>wms_tasks</c> / <c>wms_task_events</c> a task write can hit (pack §4.6).</summary>
public enum WarehouseTaskUniqueIndex
{
    /// <summary><c>(TenantId, Number)</c> — the number is unique tenant-wide.</summary>
    Number,

    /// <summary><c>(TenantId, LegalEntityId, CreatedBy, IdempotencyKey)</c> — the create key of a user.</summary>
    CreateKey,

    /// <summary>The partial index on <c>OpenSerialSlot = true</c> — one open task per serial bucket.</summary>
    SerialSlot,

    /// <summary>An index of <c>wms_task_events</c> (task + seq, or a key event).</summary>
    Event
}

public sealed class WarehouseTaskDuplicateException(WarehouseTaskUniqueIndex index, Exception? inner = null) : Exception($"Duplicate on {index}.", inner)
{
    public WarehouseTaskUniqueIndex Index { get; } = index;
}

/// <summary>
/// MOD-0178 tasks and their insert-only timeline (pack §3, §4.1–§4.3, §4.6). Every member is bound to the caller's resolved Tenant +
/// LegalEntity — another company's or tenant's task does not exist here (404). The ONE cross-tenant read, the reconciler's scan, is not on
/// this port (<see cref="IWarehousePendingScan"/>). Changes go through <see cref="InTransactionAsync"/>.
/// </summary>
public interface IWarehouseTaskRepository
{
    /// <summary>A PUTAWAY task of the caller's company (DL-4: an S2 pick task in the same collection is not found here — the S1 surface is putaway only).</summary>
    Task<WarehouseTask?> FindAsync(Guid taskId, CancellationToken cancellationToken = default);

    Task<WarehouseTask?> FindByCreateKeyAsync(Guid createdBy, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>The PUTAWAY tasks among <paramref name="taskIds"/> (pick tasks are absent).</summary>
    Task<IReadOnlyList<WarehouseTask>> FindManyAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken cancellationToken = default);

    Task<WarehouseTaskPage> ListAsync(WarehouseTaskQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// The company's Open / InProgress tasks — putaway AND pick (pack §8.17 step 4, r23) — whose source is one of <paramref name="fromLocationIds"/>
    /// (open quantity, open-task rule, the S2 bin plan).
    /// </summary>
    Task<IReadOnlyList<WarehouseTask>> OpenTasksFromAsync(IReadOnlyCollection<Guid> fromLocationIds, CancellationToken cancellationToken = default);

    Task<WarehouseTaskEventPage> EventsAsync(Guid taskId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<WarehouseTaskEvent?> FindKeyEventAsync(Guid taskId, Guid actorUserId, string requestOp, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WarehouseTaskEvent>> FindBulkKeyEventsAsync(Guid actorUserId, string requestOp, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="work"/> in ONE multi-document transaction; the central-audit event is enlisted only when the work changed a task
    /// (<see cref="WarehouseWriteOutcome.Affected"/> &gt; 0). A unique-index refusal surfaces as <see cref="WarehouseTaskDuplicateException"/>.
    /// </summary>
    Task<WarehouseWriteOutcome> InTransactionAsync(Func<IWarehouseTaskWriteSession, CancellationToken, Task<WarehouseWriteOutcome>> work,
        CancellationToken cancellationToken = default);
}

public interface IWarehouseTaskWriteSession
{
    Task InsertTaskAsync(WarehouseTask task, CancellationToken cancellationToken = default);

    /// <summary>Replaces the task if its stored version is still <paramref name="expectedVersion"/>; false = 0 records (a concurrent write won).</summary>
    Task<bool> ReplaceTaskAsync(WarehouseTask task, int expectedVersion, CancellationToken cancellationToken = default);

    Task InsertEventsAsync(IReadOnlyList<WarehouseTaskEvent> events, CancellationToken cancellationToken = default);
}

/// <summary>A task the reconciler should look at, with the scope it is processed in (the actor = the confirmation's <c>ConfirmedBy</c>).</summary>
public sealed record PendingScanRow(Guid TaskId, Guid TenantId, Guid LegalEntityId, Guid ActorUserId, string? ActorName);

/// <summary>
/// The reconciler's scan (pack §8.3 "Seçim"): an EXPLICIT cross-tenant read, used by the hosted service only — every row is then processed in
/// its own Tenant + company scope. Read-only; two cursors, two budgets.
/// </summary>
public interface IWarehousePendingScan
{
    /// <summary>Tasks with a Pending confirmation whose last attempt is older than <paramref name="olderThan"/>, after the cursor, ≤ limit.</summary>
    Task<IReadOnlyList<PendingScanRow>> PendingAsync(DateTimeOffset olderThan, (Guid TenantId, Guid TaskId)? after, int limit, IReadOnlyCollection<Guid>? onlyTenants = null,
        CancellationToken cancellationToken = default);

    /// <summary>Tasks with a Refused-on-retry confirmation (PreCheck / Ledger4xx) refused since <paramref name="since"/>, after the cursor, ≤ limit.</summary>
    Task<IReadOnlyList<PendingScanRow>> DefensiveAsync(DateTimeOffset since, (Guid TenantId, Guid TaskId)? after, int limit, IReadOnlyCollection<Guid>? onlyTenants = null,
        CancellationToken cancellationToken = default);
}
