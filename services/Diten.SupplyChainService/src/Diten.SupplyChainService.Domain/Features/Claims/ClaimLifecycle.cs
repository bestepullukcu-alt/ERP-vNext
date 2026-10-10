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
  /*
   * D187-01, owner ruling 2026-10-11. AN ABSENT SHIPMENT CARRIER IS NOT A MISMATCH.
   *
   * Nothing writes Shipment.CarrierId: MOD-0183:95 says "no new assignment API" and :499 holds
   * carrier/load assignment until an approved frozen assignment surface exists, so the field is
   * null in every running system today. The old condition compared the claim's carrier to that
   * null and rejected, which made it IMPOSSIBLE to file a claim naming the carrier that actually
   * carried the shipment - measured live on 2026-10-10: a claim with the real carrier answered
   * 422 while the same claim without one answered 201. The closing step of the golden flow could
   * not be reached.
   *
   * The claim's carrier is still not taken on trust: ClaimReferenceReader resolves it against the
   * scoped carrier list and answers 404 when it is not there, which is what sets CarrierStatus.
   * So "absent" means only that the shipment cannot corroborate - not that the carrier is unknown.
   *
   * A shipment carrier that is PRESENT and DIFFERENT is still a mismatch, so the guard keeps its
   * full meaning the day an assignment surface lands.
   */
  if(claim.CarrierId is not null
     && ((observed.ShipmentCarrierId is not null && claim.CarrierId!=observed.ShipmentCarrierId)
         || !new[]{"Active","Suspended","Retired"}.Contains(observed.CarrierStatus)))
   throw new ClaimFailureException(422,"CLAIM_CARRIER_MISMATCH");
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
