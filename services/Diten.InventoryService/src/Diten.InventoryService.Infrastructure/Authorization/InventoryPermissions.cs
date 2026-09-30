namespace Diten.InventoryService.Infrastructure.Authorization;

/// <summary>
/// MOD-0173 permission names (pack §14, existing <c>supplychain.*</c> convention). Their definition / seed in the
/// shared permission registry is a separate integration seam (INT-1) — this service only CHECKS them.
/// </summary>
public static class InventoryPermissions
{
    public const string Read = "supplychain.inventory.read";

    public const string PostMovements = "supplychain.inventory.movements.post";
}
