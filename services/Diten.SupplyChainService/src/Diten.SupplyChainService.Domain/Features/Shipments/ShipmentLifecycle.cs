namespace Diten.SupplyChainService.Domain.Features.Shipments;
public static class ShipmentLifecycle
{
    public static bool Allows(ShipmentStatus from, ShipmentStatus to) => (from, to) switch
    {
        (ShipmentStatus.Draft, ShipmentStatus.Planned or ShipmentStatus.Cancelled) => true,
        (ShipmentStatus.Planned, ShipmentStatus.Dispatched or ShipmentStatus.Cancelled) => true,
        (ShipmentStatus.Dispatched, ShipmentStatus.InTransit or ShipmentStatus.Delivered or ShipmentStatus.Exception) => true,
        (ShipmentStatus.InTransit, ShipmentStatus.Delivered or ShipmentStatus.Exception) => true,
        (ShipmentStatus.Exception, ShipmentStatus.InTransit or ShipmentStatus.Cancelled) => true,
        (ShipmentStatus.Delivered, ShipmentStatus.Closed) => true,
        _ => false
    };
}
