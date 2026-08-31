using System.Text;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.FundingScenario;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.GateI.FundingScenario;
using Xunit;

namespace Diten.PpmService.IntegrationTests.GateI.FundingScenario;

public sealed class FundingScenarioValidationContractTests
{
    private static readonly Guid Tenant=Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),Actor=Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly InvestmentCaseContextV1 Context=new(InvestmentCaseContextV1.ExpectedContractName,"1.0",Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly SelectedBudgetVersionReferenceV1 Budget=new(SelectedBudgetVersionReferenceV1.ExpectedContractName,"1.0",Context,new(BudgetVersionReferenceV1.ExpectedContractName,"1.0",Guid.NewGuid(),Guid.NewGuid(),1));
    private static readonly ScenarioVersionReferenceV1 ScenarioTuple=new(ScenarioVersionReferenceV1.ExpectedContractName,"1.0",Guid.NewGuid(),Guid.NewGuid(),1);
    private static readonly InvestmentCaseScenarioVersionReferenceV1 Scenario=new(InvestmentCaseScenarioVersionReferenceV1.ExpectedContractName,"1.0",Context,ScenarioTuple);
    private static readonly InvestmentCaseComparatorOutputReferenceV1 Comparator=new(InvestmentCaseComparatorOutputReferenceV1.ExpectedContractName,"1.0",Context,new(ComparatorOutputReferenceV1.ExpectedContractName,"1.0",Guid.NewGuid(),Guid.NewGuid(),1));
    private static readonly SelectedScenarioReferenceV1 SelectedScenario=new(SelectedScenarioReferenceV1.ExpectedContractName,"1.0",Context,ScenarioTuple);
    private static readonly byte[] BudgetBytes=Encoding.UTF8.GetBytes(Budget.ToExactJson()),ScenarioBytes=Encoding.UTF8.GetBytes(Scenario.ToExactJson()),ComparatorBytes=Encoding.UTF8.GetBytes(Comparator.ToExactJson()),SelectedScenarioBytes=Encoding.UTF8.GetBytes(SelectedScenario.ToExactJson());

    [Fact]
    public void Atomic_budget_and_scenario_lane_requires_distinct_exact_profiles_and_stays_non_runtime()
    {
        var budget=FundingScenarioContractEvaluator.EvaluateBudgetBytes(BudgetBytes,FundingScenarioValidationMode.CurrentSelectionEligibility,Security(FundingScenarioAtomicLane.Budgeting,BudgetBytes),new(ProducerReferenceState.Allowed));
        var scenario=FundingScenarioContractEvaluator.EvaluateScenarioBytes(SelectedScenarioBytes,FundingScenarioReferenceKind.SelectedScenario,FundingScenarioValidationMode.CurrentSelectionEligibility,Security(FundingScenarioAtomicLane.ScenarioPlanning,SelectedScenarioBytes),new(ProducerReferenceState.Allowed));
        Assert.Equal(503,budget.HttpStatus);Assert.Equal(503,scenario.HttpStatus);Assert.True(budget.ContractSatisfied);Assert.True(scenario.ContractSatisfied);Assert.False(budget.IsExecutable);Assert.False(scenario.IsExecutable);
        Assert.NotEqual(FundingScenarioAtomicLane.Budgeting.OwnerModule,FundingScenarioAtomicLane.ScenarioPlanning.OwnerModule);Assert.NotEqual(FundingScenarioAtomicLane.Budgeting.OperationId,FundingScenarioAtomicLane.ScenarioPlanning.OperationId);Assert.NotEqual(FundingScenarioAtomicLane.Budgeting.SigningIdentity,FundingScenarioAtomicLane.ScenarioPlanning.SigningIdentity);Assert.NotEqual(FundingScenarioAtomicLane.Budgeting.FixtureKeyId,FundingScenarioAtomicLane.ScenarioPlanning.FixtureKeyId);
    }

    [Theory]
    [InlineData(S2SAuthenticationState.Invalid,401)]
    [InlineData(S2SAuthenticationState.Unavailable,503)]
    [InlineData(S2SAuthenticationState.Malformed,503)]
    [InlineData(S2SAuthenticationState.Indeterminate,503)]
    public void Authentication_states_map_exactly_without_fallback(S2SAuthenticationState state,int status)=>Assert.Equal(status,FundingScenarioContractEvaluator.EvaluateBudgetBytes(BudgetBytes,FundingScenarioValidationMode.HistoricalResolve,Security(FundingScenarioAtomicLane.Budgeting,BudgetBytes) with{AuthenticationState=state},new(ProducerReferenceState.Allowed)).HttpStatus);

    [Theory]
    [InlineData(S2SAuthorizationState.Denied,403)]
    [InlineData(S2SAuthorizationState.Unavailable,503)]
    [InlineData(S2SAuthorizationState.Malformed,503)]
    [InlineData(S2SAuthorizationState.Indeterminate,503)]
    public void Entitlement_and_explicit_grant_are_typed_authoritative_states(S2SAuthorizationState state,int status)
    {
        var valid=Security(FundingScenarioAtomicLane.Budgeting,BudgetBytes);
        Assert.Equal(status,FundingScenarioContractEvaluator.EvaluateBudgetBytes(BudgetBytes,FundingScenarioValidationMode.HistoricalResolve,valid with{EntitlementState=state},new(ProducerReferenceState.Allowed)).HttpStatus);
        Assert.Equal(status,FundingScenarioContractEvaluator.EvaluateBudgetBytes(BudgetBytes,FundingScenarioValidationMode.HistoricalResolve,valid with{ExplicitGrantState=state},new(ProducerReferenceState.Allowed)).HttpStatus);
    }

    [Theory]
    [InlineData(ProducerReferenceState.MissingOrInvisible,404)]
    [InlineData(ProducerReferenceState.IneligibleOrStale,409)]
    [InlineData(ProducerReferenceState.Unavailable,503)]
    [InlineData(ProducerReferenceState.Malformed,503)]
    [InlineData(ProducerReferenceState.Indeterminate,503)]
    [InlineData(ProducerReferenceState.UnsupportedVersion,503)]
    public void Producer_results_map_exactly_for_both_owners(ProducerReferenceState state,int status)
    {
        Assert.Equal(status,FundingScenarioContractEvaluator.EvaluateBudgetBytes(BudgetBytes,FundingScenarioValidationMode.NewReferenceEligibility,Security(FundingScenarioAtomicLane.Budgeting,BudgetBytes),new(state)).HttpStatus);
        Assert.Equal(status,FundingScenarioContractEvaluator.EvaluateScenarioBytes(ScenarioBytes,FundingScenarioReferenceKind.ScenarioVersion,FundingScenarioValidationMode.NewReferenceEligibility,Security(FundingScenarioAtomicLane.ScenarioPlanning,ScenarioBytes),new(state)).HttpStatus);
    }

    [Fact]
    public void Mode_cross_module_and_malformed_byte_pairs_fail_closed()
    {
        Assert.Equal(400,FundingScenarioContractEvaluator.EvaluateScenarioBytes(ScenarioBytes,FundingScenarioReferenceKind.ScenarioVersion,FundingScenarioValidationMode.CurrentSelectionEligibility,Security(FundingScenarioAtomicLane.ScenarioPlanning,ScenarioBytes),new(ProducerReferenceState.Allowed)).HttpStatus);
        Assert.Equal(400,FundingScenarioContractEvaluator.EvaluateScenarioBytes(ComparatorBytes,FundingScenarioReferenceKind.ComparatorOutput,FundingScenarioValidationMode.CurrentSelectionEligibility,Security(FundingScenarioAtomicLane.ScenarioPlanning,ComparatorBytes),new(ProducerReferenceState.Allowed)).HttpStatus);
        Assert.Equal(400,FundingScenarioContractEvaluator.EvaluateScenarioBytes(BudgetBytes,FundingScenarioReferenceKind.SelectedScenario,FundingScenarioValidationMode.HistoricalResolve,Security(FundingScenarioAtomicLane.ScenarioPlanning,BudgetBytes),new(ProducerReferenceState.Allowed)).HttpStatus);
        var malformed=Encoding.UTF8.GetBytes("{not-json");Assert.Equal(400,FundingScenarioContractEvaluator.EvaluateBudgetBytes(malformed,FundingScenarioValidationMode.HistoricalResolve,Security(FundingScenarioAtomicLane.Budgeting,malformed),new(ProducerReferenceState.Allowed)).HttpStatus);
        Assert.Equal(403,FundingScenarioContractEvaluator.EvaluateBudgetBytes(BudgetBytes,FundingScenarioValidationMode.HistoricalResolve,Security(FundingScenarioAtomicLane.Budgeting,BudgetBytes) with{OwnerModule="MOD-0138"},new(ProducerReferenceState.Allowed)).HttpStatus);
        Assert.Equal(403,FundingScenarioContractEvaluator.EvaluateScenarioBytes(ScenarioBytes,FundingScenarioReferenceKind.ScenarioVersion,FundingScenarioValidationMode.HistoricalResolve,Security(FundingScenarioAtomicLane.ScenarioPlanning,ScenarioBytes) with{OwnerModule="MOD-0136"},new(ProducerReferenceState.Allowed)).HttpStatus);
    }

    [Fact]
    public void Request_binding_is_canonical_dimension_sensitive_and_fixed_time_checked()
    {
        var profile=FundingScenarioAtomicLane.Budgeting;var valid=Security(profile,BudgetBytes);
        foreach(var context in new[]{valid with{TenantId=Guid.Empty},valid with{EffectiveActorId=Guid.Empty},valid with{Audience="wrong"},valid with{ClientId="wrong"},valid with{ProtocolScope="wrong"},valid with{RequestHash=new string('a',64)},valid with{Method="post"},valid with{Path="/x?query=y"},valid with{DelegatedProofValidated=false}})Assert.Equal(401,FundingScenarioContractEvaluator.EvaluateBudgetBytes(BudgetBytes,FundingScenarioValidationMode.HistoricalResolve,context,new(ProducerReferenceState.Allowed)).HttpStatus);
        var receiver=S2SOutboundReceiverProfiles.Budgeting;var baseline=FundingScenarioRequestBinding.Compute(receiver.Method,receiver.Path,Tenant,profile.OperationId,BudgetBytes);
        Assert.NotEqual(baseline,FundingScenarioRequestBinding.Compute("PUT",receiver.Path,Tenant,profile.OperationId,BudgetBytes));Assert.NotEqual(baseline,FundingScenarioRequestBinding.Compute(receiver.Method,"/other",Tenant,profile.OperationId,BudgetBytes));Assert.NotEqual(baseline,FundingScenarioRequestBinding.Compute(receiver.Method,receiver.Path,Guid.NewGuid(),profile.OperationId,BudgetBytes));Assert.NotEqual(baseline,FundingScenarioRequestBinding.Compute(receiver.Method,receiver.Path,Tenant,profile.OperationId,Encoding.UTF8.GetBytes("{}")));
    }

    [Fact]
    public void Freshness_and_revalidation_fences_map_stale_to_409_and_indeterminate_to_503()
    {
        var valid=Security(FundingScenarioAtomicLane.Budgeting,BudgetBytes);var changed=valid.RevalidatedFence with{AuthorizationVersion=2};
        Assert.Equal(409,FundingScenarioContractEvaluator.EvaluateBudgetBytes(BudgetBytes,FundingScenarioValidationMode.HistoricalResolve,valid with{FreshnessState=S2SFreshnessState.Stale},new(ProducerReferenceState.Allowed)).HttpStatus);
        Assert.Equal(409,FundingScenarioContractEvaluator.EvaluateBudgetBytes(BudgetBytes,FundingScenarioValidationMode.HistoricalResolve,valid with{RevalidatedFence=changed},new(ProducerReferenceState.Allowed)).HttpStatus);
        Assert.Equal(409,FundingScenarioContractEvaluator.EvaluateBudgetBytes(BudgetBytes,FundingScenarioValidationMode.HistoricalResolve,valid with{RevalidatedAtUtc=valid.ValidUntilUtc.AddTicks(1)},new(ProducerReferenceState.Allowed)).HttpStatus);
        foreach(var state in new[]{S2SFreshnessState.Unavailable,S2SFreshnessState.Malformed,S2SFreshnessState.Indeterminate})Assert.Equal(503,FundingScenarioContractEvaluator.EvaluateBudgetBytes(BudgetBytes,FundingScenarioValidationMode.HistoricalResolve,valid with{FreshnessState=state},new(ProducerReferenceState.Allowed)).HttpStatus);
    }

    [Fact] public async Task Delegated_actor_null_is_401_without_provider_read()=>await AssertDelegatedFailure(Security(FundingScenarioAtomicLane.Budgeting,BudgetBytes) with{DelegatedActorId=null});
    [Fact] public async Task Delegated_actor_empty_is_401_without_provider_read()=>await AssertDelegatedFailure(Security(FundingScenarioAtomicLane.Budgeting,BudgetBytes) with{DelegatedActorId=Guid.Empty});
    [Fact] public async Task Delegated_actor_mismatch_is_401_without_provider_read()=>await AssertDelegatedFailure(Security(FundingScenarioAtomicLane.Budgeting,BudgetBytes) with{DelegatedActorId=Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")});
    [Fact] public async Task Delegated_proof_false_is_401_without_provider_read()=>await AssertDelegatedFailure(Security(FundingScenarioAtomicLane.Budgeting,BudgetBytes) with{DelegatedProofValidated=false});

    [Fact]
    public async Task Exact_delegated_actor_binding_calls_authoritative_provider_once_and_remains_non_runtime()
    {
        var budgetPort=new CountingBudgetPort();var validator=new FundingScenarioContractValidator(budgetPort,new CountingScenarioPort());var result=await validator.ValidateBudgetAsync(BudgetBytes,FundingScenarioValidationMode.HistoricalResolve,Security(FundingScenarioAtomicLane.Budgeting,BudgetBytes));Assert.Equal(503,result.HttpStatus);Assert.True(result.ContractSatisfied);Assert.Equal(1,budgetPort.ReadCount);
    }

    private static async Task AssertDelegatedFailure(S2SFundingScenarioContextV1 context)
    {
        var budgetPort=new CountingBudgetPort();var validator=new FundingScenarioContractValidator(budgetPort,new CountingScenarioPort());var result=await validator.ValidateBudgetAsync(BudgetBytes,FundingScenarioValidationMode.HistoricalResolve,context);Assert.Equal(401,result.HttpStatus);Assert.False(result.ContractSatisfied);Assert.Equal(0,budgetPort.ReadCount);
    }

    private static S2SFundingScenarioContextV1 Security(FundingScenarioProducerProfile p,ReadOnlySpan<byte> body)
    {
        var fence=new S2SVersionFenceV1(1,1,1,"entitlement-v1");var observed=DateTimeOffset.Parse("2026-08-26T09:00:00Z");
        var receiver=S2SOutboundReceiverProfiles.ForOwner(p.OwnerModule);return new(S2SAuthenticationState.AuthenticatedS2SFamilyValidated,S2SAuthorizationState.Allowed,S2SAuthorizationState.Allowed,S2SFreshnessState.Current,Tenant,Actor,Actor,true,p.Audience,p.ClientId,p.OwnerModule,p.OperationId,p.Permission,p.ProtocolScope,receiver.Method,receiver.Path,FundingScenarioRequestBinding.Compute(receiver.Method,receiver.Path,Tenant,p.OperationId,body),fence,fence,observed,observed.AddSeconds(15),observed.AddSeconds(1));
    }

    private sealed class CountingBudgetPort:IBudgetVersionReferenceValidationPort
    {
        public int ReadCount{get;private set;}
        public ValueTask<ProducerReferenceValidationResult> ValidateAsync(BudgetReferenceValidationRequest request,S2SFundingScenarioContextV1 context,CancellationToken cancellationToken){cancellationToken.ThrowIfCancellationRequested();ReadCount++;return ValueTask.FromResult(new ProducerReferenceValidationResult(ProducerReferenceState.Allowed));}
    }
    private sealed class CountingScenarioPort:IScenarioPlanningReferenceValidationPort
    {
        public ValueTask<ProducerReferenceValidationResult> ValidateAsync(ScenarioReferenceValidationRequest request,S2SFundingScenarioContextV1 context,CancellationToken cancellationToken)=>ValueTask.FromResult(new ProducerReferenceValidationResult(ProducerReferenceState.Allowed));
    }
}
