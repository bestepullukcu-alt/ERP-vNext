using Diten.FpaService.Domain.Modules.ScenarioPlanning;

namespace Diten.FpaService.Application.Modules.ScenarioPlanning.GateI;
public static class ScenarioGateIProfile
{
    public const string Operation="fpa.scenario-planning.references.validate",Permission=Operation,Audience="diten-fpa-service",ClientId="diten.fpa",Scope="diten.s2s.delegated.invoke";
    public const string ScenarioWrapper="ppm.investment-case-scenario-version-reference",ComparatorWrapper="ppm.investment-case-comparator-output-reference",SelectedWrapper="ppm.investment-case-selected-scenario-reference",Version="1.0";
}
public sealed record ScenarioGateIRequest(string WrapperName,string WrapperVersion,ReferenceValidationMode Mode,object Reference,InvestmentCaseContextV1 Context);
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
  if(!Valid(request))return new(400,"scenario_reference_request_malformed");
  if(!context.Authenticated||context.TenantId==Guid.Empty||context.ActorId==Guid.Empty||string.IsNullOrWhiteSpace(context.RequestHash))return new(401,"scenario_reference_context_invalid");
  if(!Exact(context))return new(403,"scenario_reference_forbidden");
  ScenarioReferenceSnapshot? snapshot;
  try{snapshot=await reader.ReadAsync(context.TenantId,request.Reference,request.Context.InvestmentCaseId,cancellationToken);}
  catch(OperationCanceledException){throw;}catch{return new(503,"scenario_reference_authority_unavailable");}
  var identity=Identity(request.Reference);
  if(snapshot is null||!snapshot.Exists||!snapshot.Visible||snapshot.TenantId!=context.TenantId||snapshot.PrimaryId!=identity.Primary||snapshot.SecondaryId!=identity.Secondary||snapshot.Version!=identity.Version)return new(404,"scenario_reference_not_found");
  if(request.Mode==ReferenceValidationMode.NewReferenceEligibility&&snapshot.Retired)return new(409,"scenario_reference_ineligible",request.Reference,true,false,false,true);
  if(request.Mode==ReferenceValidationMode.CurrentSelectionEligibility&&!snapshot.Current)return new(409,"scenario_reference_not_current",request.Reference,true,true,false,snapshot.Retired);
  return new(200,"scenario_reference_accepted",request.Reference,true,!snapshot.Retired,snapshot.Current,snapshot.Retired);
 }
 private static bool Valid(ScenarioGateIRequest r)
 {
  if(r.Context is null||r.Reference is null||r.WrapperVersion!=ScenarioGateIProfile.Version)return false;
  try{r.Context.Validate();}catch{return false;}
  return r.Reference switch
  {
   ScenarioVersionReferenceV1 v when v.ContractName=="fpa.scenario-planning-reference"&&v.ContractVersion=="1.0"&&v.ScenarioId!=Guid.Empty&&v.ScenarioVersionId!=Guid.Empty&&v.ScenarioVersionNumber>0 =>
    r.WrapperName==ScenarioGateIProfile.ScenarioWrapper||r.WrapperName==ScenarioGateIProfile.SelectedWrapper&&r.Mode==ReferenceValidationMode.CurrentSelectionEligibility||r.WrapperName==ScenarioGateIProfile.SelectedWrapper,
   ComparatorOutputReferenceV1 c when c.ContractName=="fpa.scenario-planning-reference"&&c.ContractVersion=="1.0"&&c.ComparatorRunId!=Guid.Empty&&c.ComparatorOutputId!=Guid.Empty&&c.OutputVersion>0 =>
    r.WrapperName==ScenarioGateIProfile.ComparatorWrapper&&r.Mode!=ReferenceValidationMode.CurrentSelectionEligibility,
   _=>false
  };
 }
 private static (Guid Primary,Guid Secondary,long Version) Identity(object r)=>r switch{ScenarioVersionReferenceV1 v=>(v.ScenarioId,v.ScenarioVersionId,v.ScenarioVersionNumber),ComparatorOutputReferenceV1 c=>(c.ComparatorRunId,c.ComparatorOutputId,c.OutputVersion),_=>default};
 private static bool Exact(ScenarioGateIContext x)=>x.ClientId==ScenarioGateIProfile.ClientId&&x.Audience==ScenarioGateIProfile.Audience&&x.Scope==ScenarioGateIProfile.Scope&&x.Operation==ScenarioGateIProfile.Operation&&x.Permission==ScenarioGateIProfile.Permission;
}
public sealed class UnavailableScenarioReferenceSnapshotReader:IScenarioReferenceSnapshotReader{public Task<ScenarioReferenceSnapshot?> ReadAsync(Guid tenantId,object reference,Guid investmentCaseId,CancellationToken cancellationToken)=>throw new InvalidOperationException("Scenario persistence reader is not composed.");}
public sealed class UnavailableScenarioGateIContextAccessor:IScenarioGateIContextAccessor{public ScenarioGateIContext Current{get;}=new(false,Guid.Empty,Guid.Empty,"","","","","","");}
