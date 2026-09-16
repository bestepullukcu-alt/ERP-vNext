using System.Text.Json.Serialization;
namespace Diten.SupplyChainService.Application.Features.Shipments;
public static class ShipmentModels
{
    public sealed record Line([property: JsonRequired] string LineNumber, [property: JsonRequired] Guid ItemId,
     [property: JsonRequired] Guid SkuId, [property: JsonRequired] string Quantity, [property: JsonRequired] string UomId, string? InventoryReferenceId);
    public sealed record Create([property: JsonRequired] string SourceModule, [property: JsonRequired] string SourceType,
     [property: JsonRequired] string SourceDocumentId, [property: JsonRequired] string WarehouseReferenceId,
     [property: JsonRequired] string ShipToReference, [property: JsonRequired] DateTimeOffset PlannedShipAt,
     [property: JsonRequired] Line[] Lines, DateTimeOffset? PlannedDeliverAt);
    public sealed record Transition([property: JsonRequired] string TargetStatus, [property: JsonRequired] DateTimeOffset OccurredAt, string? ReasonCode, string? Note);
    public sealed record Pod([property: JsonRequired] string RecipientName, [property: JsonRequired] DateTimeOffset ReceivedAt,
     [property: JsonRequired] string[] EvidenceReferenceIds, string? Note);
}
