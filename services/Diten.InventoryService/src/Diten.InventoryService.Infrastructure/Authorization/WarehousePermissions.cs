namespace Diten.InventoryService.Infrastructure.Authorization;

/// <summary>
/// MOD-0178 permission keys — namespace <c>warehouse.*</c>, owned by MOD-0178 (pack §14.1, decision Q-08; PKS-001 §4 one owner per namespace).
/// All Tenant scope. <see cref="Implemented"/> are the keys the controllers enforce so far — exactly what the manifest declares (DL-1: the
/// location-role keys); the task, outbound, pick and package keys come with their slices.
/// </summary>
public static class WarehousePermissions
{
    public const string PutawayTasksRead = "warehouse.putaway-tasks.read";

    public const string PutawayTasksCreate = "warehouse.putaway-tasks.create";

    public const string PutawayTasksUpdate = "warehouse.putaway-tasks.update";

    public const string PutawayTasksConfirm = "warehouse.putaway-tasks.confirm";

    public const string PutawayTasksCancel = "warehouse.putaway-tasks.cancel";

    public const string PutawayTasksResolve = "warehouse.putaway-tasks.resolve";

    public const string LocationRolesRead = "warehouse.location-roles.read";

    public const string LocationRolesManage = "warehouse.location-roles.manage";

    public const string OutboundShipmentsRead = "warehouse.outbound-shipments.read";

    /// <summary>Pack §14.2 — the S1 keys of the namespace.</summary>
    public static readonly IReadOnlyList<string> S1 =
    [
        PutawayTasksRead, PutawayTasksCreate, PutawayTasksUpdate, PutawayTasksConfirm, PutawayTasksCancel, PutawayTasksResolve,
        LocationRolesRead, LocationRolesManage, OutboundShipmentsRead
    ];

    /// <summary>DL-1 — the keys the location-role controller enforces.</summary>
    public static readonly IReadOnlyList<string> Implemented = [LocationRolesRead, LocationRolesManage];
}
