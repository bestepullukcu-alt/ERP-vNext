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
 public List<ReturnLine> Lines { get; set; } = [];
 public List<string> EvidenceReferenceIds { get; set; } = [];
 public ReturnStatus Status { get; set; } = ReturnStatus.Requested;
 public Guid CorrelationRoot { get; set; }
 public Guid LastEventId { get; set; }
 public string? InventoryTransactionReferenceId { get; set; }
 public string? DispositionCode { get; set; }
}
