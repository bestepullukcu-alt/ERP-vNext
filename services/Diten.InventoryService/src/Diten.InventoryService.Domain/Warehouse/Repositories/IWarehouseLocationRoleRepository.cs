using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;

namespace Diten.InventoryService.Domain.Warehouse.Repositories;

public sealed record WarehouseLocationRoleQuery(Guid? WarehouseId, WarehouseRole? Role, int Page, int PageSize);

public sealed record WarehouseLocationRolePage(IReadOnlyList<WarehouseLocationRole> Items, long Total);

/// <summary>The unique indexes of <c>wms_location_roles</c> a create can hit (pack §4.6).</summary>
public enum WarehouseLocationRoleUniqueIndex
{
    /// <summary><c>(TenantId, LegalEntityId, LocationId)</c> among live records — one record per node.</summary>
    Location,

    /// <summary><c>(TenantId, LegalEntityId, CreatedBy, IdempotencyKey)</c> — the create key of a user.</summary>
    CreateKey,

    /// <summary>An index of <c>wms_location_role_events</c> (role + seq, or the key event of an update / delete).</summary>
    Event
}

/// <summary>A write refused by a unique index; nothing of the transaction was written.</summary>
public sealed class WarehouseLocationRoleDuplicateException(WarehouseLocationRoleUniqueIndex index, Exception? inner = null)
    : Exception($"Duplicate on {index}.", inner)
{
    public WarehouseLocationRoleUniqueIndex Index { get; } = index;
}

/// <summary>
/// The outcome of one transaction of a role command: how many role records its business write changed (0 = nothing written, no event, no
/// central-audit enlistment — pack §8.8) and the record the central audit row names (null for a bulk command).
/// </summary>
public sealed record WarehouseWriteOutcome(int Affected, Guid? EntityId)
{
    public static readonly WarehouseWriteOutcome None = new(0, null);
}

/// <summary>
/// MOD-0178 location roles and their insert-only history (pack §3, §4.4, §4.6). Every member is bound to the caller's resolved
/// Tenant + LegalEntity — there is no overload without the scope, so another company's (or tenant's) record does not exist here (404).
/// Reads of live records add <c>IsDeleted = false</c>; the idempotency lookups see deleted records too (a replay answers the record's
/// current state). Changes go through <see cref="InTransactionAsync"/>: one Mongo transaction per command.
/// </summary>
public interface IWarehouseLocationRoleRepository
{
    Task<WarehouseLocationRole?> FindAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>The record whether deleted or not — the "current state" a replay answers (pack §8.1).</summary>
    Task<WarehouseLocationRole?> FindIncludingDeletedAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>The live records among these ids (the rows of a bulk delete).</summary>
    Task<IReadOnlyList<WarehouseLocationRole>> FindManyAsync(IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default);

    /// <summary>The live record of a LOCATION node, if any.</summary>
    Task<WarehouseLocationRole?> FindByLocationAsync(Guid locationId, CancellationToken cancellationToken = default);

    /// <summary>The live records of these LOCATION nodes (a path or a subtree).</summary>
    Task<IReadOnlyList<WarehouseLocationRole>> FindByLocationsAsync(IReadOnlyCollection<Guid> locationIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// The company's live records holding <paramref name="role"/> (the inherited-conflict scan of a subtree). Company-wide on purpose: a LOCATION
    /// move (K-07) may take a node under another warehouse, so the warehouse stored on a record is not a safe filter for "below this node".
    /// </summary>
    Task<IReadOnlyList<WarehouseLocationRole>> FindWithRoleAsync(WarehouseRole role, CancellationToken cancellationToken = default);

    Task<WarehouseLocationRolePage> ListAsync(WarehouseLocationRoleQuery query, CancellationToken cancellationToken = default);

    /// <summary>The record (deleted or not) created by <paramref name="createdBy"/> with this create key.</summary>
    Task<WarehouseLocationRole?> FindByCreateKeyAsync(Guid createdBy, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>The key event of a single-record command: this role, this user, this operation, this key.</summary>
    Task<WarehouseLocationRoleEvent?> FindKeyEventAsync(Guid roleId, Guid actorUserId, string requestOp, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>The key events of a bulk command: every row this user wrote under this operation and key (pack §8.1 "Toplu komutlar").</summary>
    Task<IReadOnlyList<WarehouseLocationRoleEvent>> FindBulkKeyEventsAsync(Guid actorUserId, string requestOp, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>The history of a role, oldest first.</summary>
    Task<IReadOnlyList<WarehouseLocationRoleEvent>> HistoryAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="work"/> in ONE multi-document transaction. The central-audit event is enlisted only when the work changed a record
    /// (<see cref="WarehouseWriteOutcome.Affected"/> &gt; 0). A unique-index refusal surfaces as <see cref="WarehouseLocationRoleDuplicateException"/>.
    /// </summary>
    Task<WarehouseWriteOutcome> InTransactionAsync(Func<IWarehouseLocationRoleWriteSession, CancellationToken, Task<WarehouseWriteOutcome>> work,
        CancellationToken cancellationToken = default);
}

/// <summary>Writes inside one role transaction. Roles are stamped with the caller's scope; events are insert-only.</summary>
public interface IWarehouseLocationRoleWriteSession
{
    Task InsertRoleAsync(WarehouseLocationRole role, CancellationToken cancellationToken = default);

    /// <summary>Replaces the live record if its stored version is still <paramref name="expectedVersion"/>; false = stale / gone (0 records).</summary>
    Task<bool> ReplaceRoleAsync(WarehouseLocationRole role, int expectedVersion, CancellationToken cancellationToken = default);

    Task InsertEventAsync(WarehouseLocationRoleEvent roleEvent, CancellationToken cancellationToken = default);
}
