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
/// <param name="SkuLevel">SHIPMENT-BUNDLE 3.4.0 line fields read for the MOD-0186 §34.1 CUSTOMER_RETURN; never used by the entitlement.</param>
public sealed record ReturnSourceLine(string LineNumber, string Quantity, string UomId, Guid? ItemId, Guid? SkuId, string? SkuLevel = null, string? LotId = null,
    string? IssueTransactionId = null);
/// <param name="StockIssued">SHIPMENT-BUNDLE 3.4.0 <c>ShipmentDetail.stockIssued</c>: null on a shipment without a dispatch attempt (PRE_WP_P1).</param>
public sealed record ReturnReferenceSnapshot(Guid ShipmentId, string Status, Guid Root, DateTimeOffset ObservedAt, IReadOnlyList<ReturnSourceLine> Lines,
    bool? StockIssued = null);
