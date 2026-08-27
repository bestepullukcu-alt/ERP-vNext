using Diten.FpaService.Domain.Modules.ScenarioPlanning;

namespace Diten.FpaService.Application.Modules.ScenarioPlanning.GateI;
public static class ScenarioGateIProfile
{
    public const string Operation="fpa.scenario-planning.references.validate",Permission=Operation,Audience="diten-fpa-service",ClientId="diten.fpa",Scope="diten.s2s.delegated.invoke";
    public const string ScenarioWrapper="ppm.investment-case-scenario-version-reference",ComparatorWrapper="ppm.investment-case-comparator-output-reference",SelectedWrapper="ppm.investment-case-selected-scenario-reference",Version="1.0";
}
public sealed record ScenarioGateIRequest(ReferenceValidationMode Mode,InvestmentCaseScenarioVersionReferenceV1? ScenarioVersion=null,InvestmentCaseComparatorOutputReferenceV1? ComparatorOutput=null,SelectedScenarioReferenceV1? SelectedScenario=null);
public sealed record ScenarioGateIContext(bool Authenticated,Guid TenantId,Guid ActorId,string ClientId,string Audience,string Scope,string Operation,string Permission,string RequestHash);
public interface IScenarioGateIContextAccessor{ScenarioGateIContext Current{get;}}
public sealed record ScenarioReferenceSnapshot(bool Exists,Guid TenantId,Guid PrimaryId,Guid SecondaryId,long Version,bool Visible,bool Retired,bool Current);
public interface IScenarioReferenceSnapshotReader{Task<ScenarioReferenceSnapshot?> ReadAsync(Guid tenantId,object reference,Guid investmentCaseId,CancellationToken cancellationToken);}
public sealed record ScenarioProviderOptions(bool Enabled){public static ScenarioProviderOptions Disabled{get;}=new(false);}
public sealed record ScenarioGateIResponse(int StatusCode,string Code,object? Reference=null,bool Resolved=false,bool Eligible=false,bool Current=false,bool Retired=false);
public sealed class ScenarioReferenceValidationProvider(IScenarioReferenceSnapshotReader reader,ScenarioProviderOptions options)
{
 public async Task<ScenarioGateIResponse> ValidateAsync(ScenarioGateIRequest request,ScenarioGateIContext context,CancellationToken cancellationToken)
 {
  if(!options.Enabled)return new(503,"scenario_reference_provider_disabled");
  if(!TryGetBinding(request,out var reference,out var investmentContext,out var isSelected))return new(400,"scenario_reference_request_malformed");
  if(!context.Authenticated||context.TenantId==Guid.Empty||context.ActorId==Guid.Empty||string.IsNullOrWhiteSpace(context.RequestHash))return new(401,"scenario_reference_context_invalid");
  if(!Exact(context))return new(403,"scenario_reference_forbidden");
  if(request.Mode==ReferenceValidationMode.CurrentSelectionEligibility&&!isSelected)return new(400,"scenario_reference_request_malformed");
  ScenarioReferenceSnapshot? snapshot;
  try{snapshot=await reader.ReadAsync(context.TenantId,reference,investmentContext.InvestmentCaseId,cancellationToken);}
  catch(OperationCanceledException){throw;}catch{return new(503,"scenario_reference_authority_unavailable");}
  var identity=Identity(reference);
  if(snapshot is null||!snapshot.Exists||!snapshot.Visible||snapshot.TenantId!=context.TenantId||snapshot.PrimaryId!=identity.Primary||snapshot.SecondaryId!=identity.Secondary||snapshot.Version!=identity.Version)return new(404,"scenario_reference_not_found");
  if(request.Mode==ReferenceValidationMode.NewReferenceEligibility&&snapshot.Retired)return new(409,"scenario_reference_ineligible",reference,true,false,false,true);
  if(request.Mode==ReferenceValidationMode.CurrentSelectionEligibility&&!snapshot.Current)return new(409,"scenario_reference_not_current",reference,true,true,false,snapshot.Retired);
  return new(200,"scenario_reference_accepted",reference,true,!snapshot.Retired,snapshot.Current,snapshot.Retired);
 }
 private static bool TryGetBinding(ScenarioGateIRequest r,out object reference,out InvestmentCaseContextV1 context,out bool selected)
 {
  reference=null!;context=null!;selected=false;
  if(new object?[]{r.ScenarioVersion,r.ComparatorOutput,r.SelectedScenario}.Count(x=>x is not null)!=1)return false;
  string name,version;
  if(r.ScenarioVersion is not null){name=r.ScenarioVersion.ContractName;version=r.ScenarioVersion.ContractVersion;context=r.ScenarioVersion.InvestmentCaseContext;reference=r.ScenarioVersion.ScenarioVersionReference;}
  else if(r.ComparatorOutput is not null){name=r.ComparatorOutput.ContractName;version=r.ComparatorOutput.ContractVersion;context=r.ComparatorOutput.InvestmentCaseContext;reference=r.ComparatorOutput.ComparatorOutputReference;}
  else{name=r.SelectedScenario!.ContractName;version=r.SelectedScenario.ContractVersion;context=r.SelectedScenario.InvestmentCaseContext;reference=r.SelectedScenario.ScenarioVersionReference;selected=true;}
  if(version!=ScenarioGateIProfile.Version||name!=(selected?ScenarioGateIProfile.SelectedWrapper:reference is ComparatorOutputReferenceV1?ScenarioGateIProfile.ComparatorWrapper:ScenarioGateIProfile.ScenarioWrapper))return false;
  try{context.Validate();}catch{return false;}
  return reference switch{ScenarioVersionReferenceV1 v=>v.ContractName=="fpa.scenario-planning-reference"&&v.ContractVersion=="1.0"&&v.ScenarioId!=Guid.Empty&&v.ScenarioVersionId!=Guid.Empty&&v.ScenarioVersionNumber>0,ComparatorOutputReferenceV1 c=>c.ContractName=="fpa.scenario-planning-reference"&&c.ContractVersion=="1.0"&&c.ComparatorRunId!=Guid.Empty&&c.ComparatorOutputId!=Guid.Empty&&c.OutputVersion>0,_=>false};
 }
 private static (Guid Primary,Guid Secondary,long Version) Identity(object r)=>r switch{ScenarioVersionReferenceV1 v=>(v.ScenarioId,v.ScenarioVersionId,v.ScenarioVersionNumber),ComparatorOutputReferenceV1 c=>(c.ComparatorRunId,c.ComparatorOutputId,c.OutputVersion),_=>default};
 private static bool Exact(ScenarioGateIContext x)=>x.ClientId==ScenarioGateIProfile.ClientId&&x.Audience==ScenarioGateIProfile.Audience&&x.Scope==ScenarioGateIProfile.Scope&&x.Operation==ScenarioGateIProfile.Operation&&x.Permission==ScenarioGateIProfile.Permission;
}
public sealed class UnavailableScenarioReferenceSnapshotReader:IScenarioReferenceSnapshotReader{public Task<ScenarioReferenceSnapshot?> ReadAsync(Guid tenantId,object reference,Guid investmentCaseId,CancellationToken cancellationToken)=>throw new InvalidOperationException("Scenario persistence reader is not composed.");}
public sealed class UnavailableScenarioGateIContextAccessor:IScenarioGateIContextAccessor{public ScenarioGateIContext Current{get;}=new(false,Guid.Empty,Guid.Empty,"","","","","","");}
