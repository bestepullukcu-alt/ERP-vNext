namespace Diten.SupplyChainService.Domain.Features.Returns;
public sealed class ReturnEntitlement
{
 public Guid Id { get; set; }
 public Guid TenantId { get; set; }
 public Guid LegalEntityId { get; set; }
 public Guid ShipmentId { get; set; }
 public string LineNumber { get; set; } = "";
 public string SourceQuantity { get; set; } = "";
 public string UomId { get; set; } = "";
 public Guid? ItemId { get; set; }
 public Guid? SkuId { get; set; }
 public string UsedQuantity { get; set; } = "0";
 public int Version { get; set; }
}
public sealed record ReturnSourceLine(string LineNumber, string Quantity, string UomId, Guid? ItemId, Guid? SkuId);
public sealed record ReturnReferenceSnapshot(Guid ShipmentId, string Status, Guid Root, DateTimeOffset ObservedAt, IReadOnlyList<ReturnSourceLine> Lines);
