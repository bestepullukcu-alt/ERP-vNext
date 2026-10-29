using System.Text.Json.Serialization;
namespace Diten.SupplyChainService.Application.Features.Shipments;
public static class ShipmentModels
{
    // 3.4.0 (MOD-0183 §23.1): the optional stock fields are left out of the JSON when absent, so a request without them
    // keeps the idempotency fingerprint it had before 3.4.0. issueTransactionId is read-only and ignored on create.
    public sealed record Line([property: JsonRequired] string LineNumber, [property: JsonRequired] Guid ItemId,
     [property: JsonRequired] Guid SkuId, [property: JsonRequired] string Quantity, [property: JsonRequired] string UomId, string? InventoryReferenceId,
     [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SkuLevel = null,
     [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? LotId = null,
     [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? FromLocationId = null,
     [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? FefoBypassReason = null,
     [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? IssueTransactionId = null);
    public sealed record Create([property: JsonRequired] string SourceModule, [property: JsonRequired] string SourceType,
     [property: JsonRequired] string SourceDocumentId, [property: JsonRequired] string WarehouseReferenceId,
     [property: JsonRequired] string ShipToReference, [property: JsonRequired] DateTimeOffset PlannedShipAt,
     [property: JsonRequired] Line[] Lines, DateTimeOffset? PlannedDeliverAt);
    public sealed record Transition([property: JsonRequired] string TargetStatus, [property: JsonRequired] DateTimeOffset OccurredAt, string? ReasonCode, string? Note);
    public sealed record Pod([property: JsonRequired] string RecipientName, [property: JsonRequired] DateTimeOffset ReceivedAt,
     [property: JsonRequired] string[] EvidenceReferenceIds, string? Note);
}
