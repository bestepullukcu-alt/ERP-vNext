namespace Diten.SupplyChainService.Infrastructure.Authorization;
public static class ShipmentPermissions
{
    public const string Read = "supplychain.shipments.read";
    public const string Create = "supplychain.shipments.create";
    public const string Dispatch = "supplychain.shipments.dispatch";
    public const string Cancel = "supplychain.shipments.cancel";
    public const string CapturePod = "supplychain.shipments.pod.capture";
    public const string Reconcile = "supplychain.shipments.reconcile";
}
