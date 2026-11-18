namespace Diten.SupplyChainService.Domain.Features.Shipments;
public enum ShipmentSortColumn { ShipmentNumber, SourceDocumentId, Status, PlannedShipAt, PlannedDeliverAt }
/// <summary>queryShipments filters (3.4.0) plus the 3.5.0 search and order (MOD-0183 r9 §24.7 item 1). A null
/// <see cref="OrderBy"/> keeps the 3.4.0 order, by shipment id.</summary>
public sealed record ShipmentListRequest(ShipmentStatus? Status, string? SourceDocumentId, string? Search,
    ShipmentSortColumn? OrderBy, bool Descending, int Page, int PageSize);
/// <summary><see cref="Total"/> keeps its 3.4.0 meaning (status and source document filters); <see cref="FilteredTotal"/>
/// also applies the 3.5.0 search.</summary>
public sealed record ShipmentListPage(IReadOnlyList<Shipment> Items, long Total, long FilteredTotal);
