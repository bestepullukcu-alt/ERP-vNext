using Diten.SupplyChainService.Domain.Common;
namespace Diten.SupplyChainService.Domain.Features.Returns;
public sealed class ReturnOrder : EntityBase
{
 public Guid LegalEntityId { get; set; }
 public Guid CreatedBy { get; set; }
 public Guid? UpdatedBy { get; set; }
 public string RmaNumber { get; set; } = "";
 public Guid ShipmentId { get; set; }
 public string ReasonCode { get; set; } = "";
 /// <summary>SHIPMENT-BUNDLE 3.5.0 (MOD-0186 r10 §35.6, item 6): the shipment's customerRef at create, copied by the service, never
 /// taken from the client; null on returns created earlier (not back-filled) and for a shipment without one.</summary>
 public string? CustomerRef { get; set; }
 public List<ReturnLine> Lines { get; set; } = [];
 public List<string> EvidenceReferenceIds { get; set; } = [];
 public ReturnStatus Status { get; set; } = ReturnStatus.Requested;
 public Guid CorrelationRoot { get; set; }
 public Guid LastEventId { get; set; }
 public string? InventoryTransactionReferenceId { get; set; }
 public string? DispositionCode { get; set; }
 /// <summary>MOD-0186 §34.2: the open (or completed) receipt attempt behind InTransit → Received.</summary>
 public ReceiptAttempt? ReceiptAttempt { get; set; }
 /// <summary>§34.2 late-writer pairs; append-only.</summary>
 public List<ReceiptAuditEntry> ReceiptAuditHistory { get; set; } = [];
}
