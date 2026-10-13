using Diten.SupplyChainService.Domain.Common;
namespace Diten.SupplyChainService.Domain.Features.Claims;
public sealed class Claim : EntityBase
{
 public Guid LegalEntityId {get;set;}
 public Guid CreatedBy {get;set;}
 public Guid? UpdatedBy {get;set;}
 public string ClaimNumber {get;set;} = "";
 public Guid ShipmentId {get;set;}
 public Guid? CarrierId {get;set;}
 public string ReasonCode {get;set;} = "";
 public string ClaimedAmount {get;set;} = "";
 public string Currency {get;set;} = "";
 public string? ApprovedAmount {get;set;}
 public List<string> EvidenceReferenceIds {get;set;} = [];
 public ClaimStatus Status {get;set;}
 public Guid CorrelationRoot {get;set;}
 public Guid LastEventId {get;set;}
 public ClaimReferenceSnapshot? ReferenceSnapshot {get;set;}
}
