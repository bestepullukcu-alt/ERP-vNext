using Diten.SupplyChainService.Domain.Features.Shipments;
namespace Diten.SupplyChainService.Application.Features.Shipments.Queries;
/// <summary>SHIPMENT-BUNDLE 3.5.0 <c>queryShipments</c> <c>orderBy</c> enum, mapped to the stored column.</summary>
public static class ShipmentListOrder
{
    public static readonly IReadOnlyDictionary<string, ShipmentSortColumn> Columns = new Dictionary<string, ShipmentSortColumn>(StringComparer.Ordinal)
    {
        ["shipmentNumber"] = ShipmentSortColumn.ShipmentNumber,
        ["sourceDocumentId"] = ShipmentSortColumn.SourceDocumentId,
        ["status"] = ShipmentSortColumn.Status,
        ["plannedShipAt"] = ShipmentSortColumn.PlannedShipAt,
        ["plannedDeliverAt"] = ShipmentSortColumn.PlannedDeliverAt
    };
}
