namespace Diten.SupplyChainService.Domain.Features.Shipments;
public sealed record ShipmentLine(string LineNumber, Guid ItemId, Guid SkuId, string Quantity, string UomId, string? InventoryReferenceId);
