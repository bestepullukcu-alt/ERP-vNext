namespace Diten.InventoryService.Domain.Warehouse.Enums;

/// <summary>
/// The WMS role of a LOCATION node (MOD-0178 pack §4.4): <see cref="Receiving"/> (S1 putaway source), <see cref="Staging"/> (S2 dispatch
/// area). A node may hold several roles, but never Receiving and Staging together (M-5); a role is inherited by the node's subtree (F-13).
/// </summary>
public enum WarehouseRole
{
    Receiving,
    Staging
}

/// <summary>The kinds of the insert-only location-role history <c>wms_location_role_events</c> (pack §4.3).</summary>
public enum WarehouseLocationRoleEventKind
{
    Created,
    Updated,
    Deleted
}
