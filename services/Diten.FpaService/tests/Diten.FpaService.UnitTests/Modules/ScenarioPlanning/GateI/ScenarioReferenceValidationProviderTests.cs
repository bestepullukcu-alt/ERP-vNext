using Diten.FpaService.Application.Modules.ScenarioPlanning;
using Diten.FpaService.Application.Modules.ScenarioPlanning.GateI;
using Diten.FpaService.Domain.Modules.ScenarioPlanning;
namespace Diten.FpaService.UnitTests.Modules.ScenarioPlanning.GateI;
public sealed class ScenarioReferenceValidationProviderTests
{
 static readonly Guid Tenant=Guid.NewGuid(),Scenario=Guid.NewGuid(),Version=Guid.NewGuid(),Case=Guid.NewGuid();
 [Theory][InlineData(ReferenceValidationMode.HistoricalResolve,false,false,200)][InlineData(ReferenceValidationMode.NewReferenceEligibility,true,false,409)][InlineData(ReferenceValidationMode.CurrentSelectionEligibility,false,false,409)][InlineData(ReferenceValidationMode.CurrentSelectionEligibility,false,true,200)]
 public async Task Selected_mode_matrix_is_exact(ReferenceValidationMode mode,bool retired,bool current,int expected){var result=await Provider(new Reader(Snapshot(retired,current))).ValidateAsync(Request(mode),Context(),default);Assert.Equal(expected,result.StatusCode);}
 [Fact]public async Task Comparator_current_mode_is_400(){var c=new ComparatorOutputReferenceV1("fpa.scenario-planning-reference","1.0",Guid.NewGuid(),Guid.NewGuid(),1);var result=await Provider(new Reader(Snapshot())).ValidateAsync(new(ReferenceValidationMode.CurrentSelectionEligibility,ComparatorOutput:new(ScenarioGateIProfile.ComparatorWrapper,"1.0",Ctx(),c)),Context(),default);Assert.Equal(400,result.StatusCode);}
 [Fact]public async Task Cross_tenant_is_404(){var result=await Provider(new Reader(Snapshot(tenant:Guid.NewGuid()))).ValidateAsync(Request(),Context(),default);Assert.Equal(404,result.StatusCode);}
 [Fact]public async Task Wrong_identity_is_403_without_read(){var reader=new Reader(Snapshot());var result=await Provider(reader).ValidateAsync(Request(),Context(client:"alias"),default);Assert.Equal(403,result.StatusCode);Assert.Equal(0,reader.Reads);}
 [Fact]public async Task Default_is_disabled(){var result=await new ScenarioReferenceValidationProvider(new Reader(Snapshot()),ScenarioProviderOptions.Disabled).ValidateAsync(Request(),Context(),default);Assert.Equal(503,result.StatusCode);}
 static ScenarioReferenceValidationProvider Provider(Reader r)=>new(r,new(true));
 static ScenarioGateIRequest Request(ReferenceValidationMode mode=ReferenceValidationMode.NewReferenceEligibility)=>new(mode,SelectedScenario:new(ScenarioGateIProfile.SelectedWrapper,"1.0",Ctx(),new ScenarioVersionReferenceV1("fpa.scenario-planning-reference","1.0",Scenario,Version,1)));
 static InvestmentCaseContextV1 Ctx()=>new("ppm.investment-case-context","1.0",Case);
 static ScenarioGateIContext Context(string client=ScenarioGateIProfile.ClientId)=>new(true,Tenant,Guid.NewGuid(),client,ScenarioGateIProfile.Audience,ScenarioGateIProfile.Scope,ScenarioGateIProfile.Operation,ScenarioGateIProfile.Permission,new string('a',64));
 static ScenarioReferenceSnapshot Snapshot(bool retired=false,bool current=true,Guid? tenant=null)=>new(true,tenant??Tenant,Scenario,Version,1,true,retired,current);
 sealed class Reader(ScenarioReferenceSnapshot value):IScenarioReferenceSnapshotReader{public int Reads{get;private set;}public Task<ScenarioReferenceSnapshot?> ReadAsync(Guid tenantId,object reference,Guid investmentCaseId,CancellationToken cancellationToken){Reads++;return Task.FromResult<ScenarioReferenceSnapshot?>(value);}}
}
