namespace Diten.SupplyChainService.Domain.Features.Shipments;
public sealed record ShipmentLine(string LineNumber, Guid ItemId, Guid SkuId, string Quantity, string UomId, string? InventoryReferenceId)
{
    // SHIPMENT-BUNDLE 3.4.0 (MOD-0183 §23.1, G1): the stock fields the dispatch issue sends to INVENTORY. Optional and
    // additive; a line without SkuLevel cannot be dispatched (422 SHIPMENT_LINE_STOCK_INCOMPLETE, INVENTORY not called).
    public string? SkuLevel { get; init; }
    public string? LotId { get; init; }
    public string? FromLocationId { get; init; }
    public string? FefoBypassReason { get; init; }
}
