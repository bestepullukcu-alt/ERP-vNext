using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;

namespace Diten.InventoryService.Domain.Warehouse.Entities;

// MOD-0178 location roles (pack §3, §4.3, §4.4). Every record carries TenantId + LegalEntityId (EntityBase, stamped by the repository
// from the resolved scope). None keeps a quantity: the stock is the MOD-0173 ledger's (pack §2 boundary rule 1).

/// <summary>
/// One record per LOCATION node (pack §4.4, design decision M-3): the node's WMS roles and the storage-condition codes it accepts. The
/// record applies to the node's stockable subtree (roles and conditions are inherited). It only points at an existing LOCATION node — no
/// place is invented here. Soft delete (<see cref="EntityBase.IsDeleted"/> + <see cref="EntityBase.DeletedAt"/>); <see cref="EntityBase.Version"/>
/// is the If-Match version (create = 1).
/// </summary>
public sealed class WarehouseLocationRole : EntityBase
{
    /// <summary>
    /// The LOCATION Warehouse on the node's path when the record was created (the list filter's index). A LOCATION move (K-07) may later take
    /// the node under another warehouse: reads answer the warehouse of the node's CURRENT path, and the conflict scan does not filter on this.
    /// </summary>
    public Guid WarehouseId { get; set; }

    /// <summary>The LOCATION node (StorageLocation | Zone | Bin); unique among the company's live records.</summary>
    public Guid LocationId { get; set; }

    public List<WarehouseRole> Roles { get; set; } = [];

    /// <summary>PRODUCT-MASTER v1.1 <c>StorageConditionCode</c> codes, at most one temperature code (pack §4.4).</summary>
    public List<string> AcceptedStorageConditions { get; set; } = [];

    public string? Note { get; set; }

    /// <summary>The creating user — written at create only; the scope of the create key (pack §4.4, independent review 12 F-1).</summary>
    public Guid CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    /// <summary>The create request's <c>Idempotency-Key</c> (unique per company + <see cref="CreatedBy"/>) and its fingerprint (pack §8.1).</summary>
    public string? IdempotencyKey { get; set; }

    public string? RequestFingerprint { get; set; }

    public WarehouseLocationRole Copy()
    {
        var copy = (WarehouseLocationRole)MemberwiseClone();
        copy.Roles = [.. Roles];
        copy.AcceptedStorageConditions = [.. AcceptedStorageConditions];
        return copy;
    }
}

/// <summary>The role values a history row records before / after a command (pack §4.3 <c>Data { Before / After }</c>).</summary>
public sealed class WarehouseLocationRoleSnapshot
{
    public Guid LocationId { get; set; }

    public List<WarehouseRole> Roles { get; set; } = [];

    public List<string> AcceptedStorageConditions { get; set; } = [];

    public string? Note { get; set; }

    public static WarehouseLocationRoleSnapshot Of(WarehouseLocationRole role) => new()
    {
        LocationId = role.LocationId,
        Roles = [.. role.Roles],
        AcceptedStorageConditions = [.. role.AcceptedStorageConditions],
        Note = role.Note
    };
}

/// <summary>
/// Insert-only history of a location role (pack §4.3, <c>wms_location_role_events</c>): written in the SAME transaction as the role write it
/// describes, and only when that write changed a record. The central audit record carries no values (pack §8.8), so the before / after
/// values live only here. <see cref="Seq"/> is the role version the write produced (one write = one version = one row). The request key,
/// operation and fingerprint are the "key event" of update / delete / bulk delete (pack §8.1); a create keeps its key on the role itself.
/// </summary>
public sealed class WarehouseLocationRoleEvent : EntityBase
{
    public Guid RoleId { get; set; }

    public int Seq { get; set; }

    public WarehouseLocationRoleEventKind Kind { get; set; }

    public Guid ActorUserId { get; set; }

    /// <summary>The actor's display name at event time (token name); null when the token carried none.</summary>
    public string? ActorName { get; set; }

    public DateTimeOffset AtUtc { get; set; }

    public WarehouseLocationRoleSnapshot? Before { get; set; }

    public WarehouseLocationRoleSnapshot? After { get; set; }

    public Guid CorrelationId { get; set; }

    public string? IdempotencyKey { get; set; }

    /// <summary>The WMS <c>operationId</c> of the command (updateLocationRole, deleteLocationRole, bulkDeleteLocationRoles).</summary>
    public string? RequestOp { get; set; }

    public string? RequestFingerprint { get; set; }
}
