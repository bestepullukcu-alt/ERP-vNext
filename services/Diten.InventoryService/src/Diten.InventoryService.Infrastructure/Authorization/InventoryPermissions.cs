namespace Diten.InventoryService.Infrastructure.Authorization;

/// <summary>
/// MOD-0173 permission names (pack §3 / §14). Namespace <c>inventory.*</c> by owner decision 2026-10-01 (pack §19):
/// PKS-001 §4 gives every namespace exactly one owner and <c>supplychain.*</c> belongs to MVP-6. The former
/// MVP-6-namespace names (S1/S2) were never seeded, assigned or issued, so there is no alias for them — a token
/// carrying only the old names is 403 everywhere. Registering these names (module manifest) is FU01's seam; this
/// service only CHECKS them, and every <c>[HasPermission]</c> uses one of these constants.
/// </summary>
public static class InventoryPermissions
{
    public const string Read = "inventory.stock.read";

    public const string CreateMovements = "inventory.movements.create";

    /// <summary>S2: create and release reservations (pack §3 / §14).</summary>
    public const string ManageReservations = "inventory.reservations.manage";

    /// <summary>MOD-0173-FU02: every LOCATION read (v1 + v1.1 GETs, validate, history, stock presence).</summary>
    public const string LocationsRead = "inventory.locations.read";

    /// <summary>MOD-0173-FU02: create, edit (incl. move), status change, bulk bins — one Tier-3 steward action (§14).</summary>
    public const string LocationsManage = "inventory.locations.manage";
}
