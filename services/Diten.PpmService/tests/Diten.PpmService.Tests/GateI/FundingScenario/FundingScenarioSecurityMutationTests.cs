using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.FundingScenario;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.GateI.FundingScenario;
using Xunit;

namespace Diten.PpmService.Tests.GateI.FundingScenario;


public sealed class FundingScenarioSecurityMutationTests
{
    private static readonly Guid Tenant=Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),Actor=Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly InvestmentCaseContextV1 Context=new(InvestmentCaseContextV1.ExpectedContractName,"1.0",Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly SelectedBudgetVersionReferenceV1 Budget=new(SelectedBudgetVersionReferenceV1.ExpectedContractName,"1.0",Context,new(BudgetVersionReferenceV1.ExpectedContractName,"1.0",Guid.Parse("22222222-2222-2222-2222-222222222222"),Guid.Parse("33333333-3333-3333-3333-333333333333"),1));
    private static readonly ScenarioVersionReferenceV1 ScenarioTuple=new(ScenarioVersionReferenceV1.ExpectedContractName,"1.0",Guid.Parse("44444444-4444-4444-4444-444444444444"),Guid.Parse("55555555-5555-5555-5555-555555555555"),1);
    private static readonly InvestmentCaseScenarioVersionReferenceV1 Scenario=new(InvestmentCaseScenarioVersionReferenceV1.ExpectedContractName,"1.0",Context,ScenarioTuple);
    private static readonly byte[] BudgetBytes=Encoding.UTF8.GetBytes(Budget.ToExactJson()),ScenarioBytes=Encoding.UTF8.GetBytes(Scenario.ToExactJson());

    [Fact] public void Request_hash_guard_rejects_well_formed_changed_hash()=>Assert.Equal(401,FundingScenarioContractEvaluator.EvaluateBudgetBytes(BudgetBytes,FundingScenarioValidationMode.HistoricalResolve,Security(FundingScenarioAtomicLane.Budgeting,BudgetBytes) with{RequestHash=new string('a',64)},new(ProducerReferenceState.Allowed)).HttpStatus);
    [Fact] public void Freshness_guard_rejects_stale_fence()=>Assert.Equal(409,FundingScenarioContractEvaluator.EvaluateBudgetBytes(BudgetBytes,FundingScenarioValidationMode.HistoricalResolve,Security(FundingScenarioAtomicLane.Budgeting,BudgetBytes) with{FreshnessState=S2SFreshnessState.Stale},new(ProducerReferenceState.Allowed)).HttpStatus);
    [Fact] public void Current_selection_guard_rejects_analytical_scenario()=>Assert.Equal(400,FundingScenarioContractEvaluator.EvaluateScenarioBytes(ScenarioBytes,FundingScenarioReferenceKind.ScenarioVersion,FundingScenarioValidationMode.CurrentSelectionEligibility,Security(FundingScenarioAtomicLane.ScenarioPlanning,ScenarioBytes),new(ProducerReferenceState.Allowed)).HttpStatus);
    [Fact] public void Owner_guard_rejects_cross_module_context()=>Assert.Equal(403,FundingScenarioContractEvaluator.EvaluateBudgetBytes(BudgetBytes,FundingScenarioValidationMode.HistoricalResolve,Security(FundingScenarioAtomicLane.Budgeting,BudgetBytes) with{OwnerModule="MOD-0138"},new(ProducerReferenceState.Allowed)).HttpStatus);
    [Fact] public void Delegated_actor_binding_guard_rejects_effective_actor_mismatch()=>Assert.Equal(401,FundingScenarioContractEvaluator.EvaluateBudgetBytes(BudgetBytes,FundingScenarioValidationMode.HistoricalResolve,Security(FundingScenarioAtomicLane.Budgeting,BudgetBytes) with{DelegatedActorId=Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")},new(ProducerReferenceState.Allowed)).HttpStatus);
    [Fact] public void Runtime_guard_keeps_allowed_contract_non_executable()=>Assert.Equal(503,FundingScenarioContractEvaluator.EvaluateBudgetBytes(BudgetBytes,FundingScenarioValidationMode.HistoricalResolve,Security(FundingScenarioAtomicLane.Budgeting,BudgetBytes),new(ProducerReferenceState.Allowed)).HttpStatus);

    private static S2SFundingScenarioContextV1 Security(FundingScenarioProducerProfile p,ReadOnlySpan<byte> body)
    {
        var fence=new S2SVersionFenceV1(1,1,1,"entitlement-v1");var observed=DateTimeOffset.Parse("2026-08-26T09:00:00Z");var receiver=S2SOutboundReceiverProfiles.ForOwner(p.OwnerModule);return new(S2SAuthenticationState.AuthenticatedS2SFamilyValidated,S2SAuthorizationState.Allowed,S2SAuthorizationState.Allowed,S2SFreshnessState.Current,Tenant,Actor,Actor,true,p.Audience,p.ClientId,p.OwnerModule,p.OperationId,p.Permission,p.ProtocolScope,receiver.Method,receiver.Path,FundingScenarioRequestBinding.Compute(receiver.Method,receiver.Path,Tenant,p.OperationId,body),fence,fence,observed,observed.AddSeconds(15),observed.AddSeconds(1));
    }
}
