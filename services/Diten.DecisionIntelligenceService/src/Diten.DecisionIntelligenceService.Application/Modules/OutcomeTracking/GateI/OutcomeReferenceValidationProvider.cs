using Diten.DecisionIntelligenceService.Domain.Modules.OutcomeTracking;
namespace Diten.DecisionIntelligenceService.Application.Modules.OutcomeTracking.GateI;
public static class OutcomeGateIProfile
{
 public const string Operation="outcome-tracking.outcome-references.validate",Permission="decision-intelligence.outcome-references.validate",Audience="diten-decision-intelligence-service",ClientId="diten.decision-intelligence",Scope="diten.s2s.delegated.invoke";
}
public sealed record OutcomeGateIRequest(BenefitCommitmentOutcomeReferenceV1 Wrapper,ReferenceMode Mode);
public sealed record OutcomeGateIContext(bool Authenticated,Guid TenantId,Guid ActorId,string ClientId,string Audience,string Scope,string Operation,string Permission,string RequestHash);
public interface IOutcomeGateIContextAccessor{OutcomeGateIContext Current{get;}}
public sealed record OutcomeReferenceSnapshot(bool Exists,Guid TenantId,Guid OutcomeId,Guid VersionId,int VersionNumber,bool Visible,bool TechnicallyDeleted,bool Retired);
public interface IOutcomeReferenceSnapshotReader{Task<OutcomeReferenceSnapshot?> ReadAsync(Guid tenantId,Guid outcomeId,Guid versionId,Guid benefitCommitmentId,CancellationToken cancellationToken);}
public sealed record OutcomeProviderOptions(bool Enabled){public static OutcomeProviderOptions Disabled{get;}=new(false);}
public sealed record OutcomeGateIResponse(int StatusCode,string Code,OutcomeReferenceV1? Reference=null,bool Resolved=false,bool Eligible=false,bool Retired=false);
public sealed class OutcomeReferenceValidationProvider(IOutcomeReferenceSnapshotReader reader,OutcomeProviderOptions options)
{
 public async Task<OutcomeGateIResponse> ValidateAsync(OutcomeGateIRequest request,OutcomeGateIContext context,CancellationToken cancellationToken)
 {
  if(!options.Enabled)return new(503,"outcome_reference_provider_disabled");
  if(!Valid(request))return new(400,"outcome_reference_request_malformed");
  if(!context.Authenticated||context.TenantId==Guid.Empty||context.ActorId==Guid.Empty||string.IsNullOrWhiteSpace(context.RequestHash))return new(401,"outcome_reference_context_invalid");
  if(!Exact(context))return new(403,"outcome_reference_forbidden");
  var reference=request.Wrapper.OutcomeReference;
  OutcomeReferenceSnapshot? snapshot;
  try{snapshot=await reader.ReadAsync(context.TenantId,reference.OutcomeId,reference.OutcomeVersionId,request.Wrapper.BenefitCommitmentId,cancellationToken);}
  catch(OperationCanceledException){throw;}catch{return new(503,"outcome_reference_authority_unavailable");}
  if(snapshot is null||!snapshot.Exists||!snapshot.Visible||snapshot.TechnicallyDeleted||snapshot.TenantId!=context.TenantId||snapshot.OutcomeId!=reference.OutcomeId||snapshot.VersionId!=reference.OutcomeVersionId||snapshot.VersionNumber!=reference.OutcomeVersionNumber)return new(404,"outcome_reference_not_found");
  if(request.Mode==ReferenceMode.NewReferenceEligibility&&snapshot.Retired)return new(409,"outcome_reference_ineligible",reference,true,false,true);
  return new(200,"outcome_reference_accepted",reference,true,!snapshot.Retired,snapshot.Retired);
 }
 private static bool Valid(OutcomeGateIRequest request){if(request.Mode==ReferenceMode.CurrentSelectionEligibility||request.Wrapper is null)return false;try{request.Wrapper.Validate();return true;}catch{return false;}}
 private static bool Exact(OutcomeGateIContext x)=>x.ClientId==OutcomeGateIProfile.ClientId&&x.Audience==OutcomeGateIProfile.Audience&&x.Scope==OutcomeGateIProfile.Scope&&x.Operation==OutcomeGateIProfile.Operation&&x.Permission==OutcomeGateIProfile.Permission;
}
public sealed class UnavailableOutcomeReferenceSnapshotReader:IOutcomeReferenceSnapshotReader{public Task<OutcomeReferenceSnapshot?> ReadAsync(Guid tenantId,Guid outcomeId,Guid versionId,Guid benefitCommitmentId,CancellationToken cancellationToken)=>throw new InvalidOperationException("OutcomeTracking persistence reader is not composed.");}
public sealed class UnavailableOutcomeGateIContextAccessor:IOutcomeGateIContextAccessor{public OutcomeGateIContext Current{get;}=new(false,Guid.Empty,Guid.Empty,"","","","","","");}
