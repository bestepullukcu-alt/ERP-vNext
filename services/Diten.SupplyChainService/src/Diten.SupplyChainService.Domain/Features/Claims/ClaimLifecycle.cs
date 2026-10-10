namespace Diten.SupplyChainService.Domain.Features.Claims;
public static class ClaimLifecycle
{
 public static bool CanTransition(ClaimStatus source,ClaimStatus target)=>(source,target) is
 (ClaimStatus.Open,ClaimStatus.Investigating) or (ClaimStatus.Open,ClaimStatus.Withdrawn) or
 (ClaimStatus.Investigating,ClaimStatus.Approved) or (ClaimStatus.Investigating,ClaimStatus.Rejected) or
 (ClaimStatus.Approved,ClaimStatus.Settled) or (ClaimStatus.Rejected,ClaimStatus.Closed) or (ClaimStatus.Settled,ClaimStatus.Closed);
 public static void ValidateCreate(Claim claim,ClaimReferenceSnapshot observed)
 {
  if(!new[]{"Dispatched","InTransit","Delivered","Exception","Closed"}.Contains(observed.ShipmentStatus))throw new ClaimFailureException(422,"CLAIM_SHIPMENT_INELIGIBLE");
  if(claim.CarrierId is not null && (claim.CarrierId!=observed.ShipmentCarrierId || !new[]{"Active","Suspended","Retired"}.Contains(observed.CarrierStatus)))throw new ClaimFailureException(422,"CLAIM_CARRIER_MISMATCH");
  if(!ExactClaimAmount.Parse(claim.ClaimedAmount).IsPositive)throw new ClaimFailureException(422,"CLAIM_AMOUNT_INVALID");
 }
 public static void ValidateTransition(Claim claim,ClaimStatus target,string? approvedAmount)
 {
  if(!CanTransition(claim.Status,target))throw new ClaimFailureException(422,"INVALID_CLAIM_TRANSITION");
  if(target==ClaimStatus.Approved)
  { if(approvedAmount is null||ExactClaimAmount.Parse(approvedAmount).IsNegative||ExactClaimAmount.Parse(approvedAmount).CompareTo(ExactClaimAmount.Parse(claim.ClaimedAmount))>0)throw new ClaimFailureException(422,"CLAIM_APPROVAL_AMOUNT_INVALID"); }
  else if(approvedAmount is not null)throw new ClaimFailureException(422,"CLAIM_APPROVED_AMOUNT_NOT_ALLOWED");
 }
}
