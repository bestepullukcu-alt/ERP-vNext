using Diten.DecisionIntelligenceService.Application.Modules.OutcomeTracking.GateI;
using Diten.DecisionIntelligenceService.Domain.Modules.OutcomeTracking;
using Xunit;
namespace Diten.DecisionIntelligenceService.UnitTests.Modules.OutcomeTracking.GateI;
public sealed class OutcomeReferenceValidationProviderTests
{
 static readonly Guid Tenant=Guid.NewGuid(),Outcome=Guid.NewGuid(),Version=Guid.NewGuid(),Commitment=Guid.NewGuid();
 [Theory][InlineData(ReferenceMode.HistoricalResolve,true,200)][InlineData(ReferenceMode.NewReferenceEligibility,true,409)][InlineData(ReferenceMode.CurrentSelectionEligibility,false,400)]
 public async Task Mode_matrix_is_exact(ReferenceMode mode,bool retired,int expected){var result=await Provider(new Reader(Snapshot(retired))).ValidateAsync(Request(mode),Context(),default);Assert.Equal(expected,result.StatusCode);}
 [Fact]public async Task Cross_tenant_is_404(){var result=await Provider(new Reader(Snapshot(tenant:Guid.NewGuid()))).ValidateAsync(Request(),Context(),default);Assert.Equal(404,result.StatusCode);}
 [Fact]public async Task Wrong_permission_is_403_without_read(){var reader=new Reader(Snapshot());var result=await Provider(reader).ValidateAsync(Request(),Context(permission:"alias"),default);Assert.Equal(403,result.StatusCode);Assert.Equal(0,reader.Reads);}
 [Fact]public async Task Default_is_disabled(){var result=await new OutcomeReferenceValidationProvider(new Reader(Snapshot()),OutcomeProviderOptions.Disabled).ValidateAsync(Request(),Context(),default);Assert.Equal(503,result.StatusCode);}
 static OutcomeReferenceValidationProvider Provider(Reader r)=>new(r,new(true));
 static OutcomeGateIRequest Request(ReferenceMode mode=ReferenceMode.NewReferenceEligibility)=>new(new("ppm.benefit-commitment-outcome-reference","1.0",Commitment,new("diten.decision-intelligence.outcome-reference","1.0",Outcome,Version,1)),mode);
 static OutcomeGateIContext Context(string permission=OutcomeGateIProfile.Permission)=>new(true,Tenant,Guid.NewGuid(),OutcomeGateIProfile.ClientId,OutcomeGateIProfile.Audience,OutcomeGateIProfile.Scope,OutcomeGateIProfile.Operation,permission,new string('a',64));
 static OutcomeReferenceSnapshot Snapshot(bool retired=false,Guid? tenant=null)=>new(true,tenant??Tenant,Outcome,Version,1,true,false,retired);
 sealed class Reader(OutcomeReferenceSnapshot value):IOutcomeReferenceSnapshotReader{public int Reads{get;private set;}public Task<OutcomeReferenceSnapshot?> ReadAsync(Guid tenantId,Guid outcomeId,Guid versionId,Guid benefitCommitmentId,CancellationToken cancellationToken){Reads++;return Task.FromResult<OutcomeReferenceSnapshot?>(value);}}
}
