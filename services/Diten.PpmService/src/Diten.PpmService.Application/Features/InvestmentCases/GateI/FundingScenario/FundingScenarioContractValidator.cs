using System.Security.Cryptography;
using System.Text;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.GateI.FundingScenario;

namespace Diten.PpmService.Application.Features.InvestmentCases.GateI.FundingScenario;


public sealed class FundingScenarioContractValidator(IBudgetVersionReferenceValidationPort budgetPort,IScenarioPlanningReferenceValidationPort scenarioPort)
{
    public async ValueTask<FundingScenarioContractResult> ValidateBudgetAsync(ReadOnlyMemory<byte> wrapperUtf8,FundingScenarioValidationMode mode,S2SFundingScenarioContextV1 context,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();var preflight=FundingScenarioContractEvaluator.EvaluateBudgetBytes(wrapperUtf8,mode,context,new(ProducerReferenceState.Allowed));if(!preflight.ContractSatisfied)return preflight;
        var wrapper=SelectedBudgetVersionReferenceV1.ParseExact(Encoding.UTF8.GetString(wrapperUtf8.Span));var producer=await budgetPort.ValidateAsync(new(wrapper,mode),context,cancellationToken).ConfigureAwait(false);return FundingScenarioContractEvaluator.EvaluateBudgetBytes(wrapperUtf8,mode,context,producer);
    }
    public async ValueTask<FundingScenarioContractResult> ValidateScenarioAsync(ReadOnlyMemory<byte> wrapperUtf8,FundingScenarioReferenceKind kind,FundingScenarioValidationMode mode,S2SFundingScenarioContextV1 context,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();var preflight=FundingScenarioContractEvaluator.EvaluateScenarioBytes(wrapperUtf8,kind,mode,context,new(ProducerReferenceState.Allowed));if(!preflight.ContractSatisfied)return preflight;var json=Encoding.UTF8.GetString(wrapperUtf8.Span);object wrapper=kind switch{FundingScenarioReferenceKind.ScenarioVersion=>InvestmentCaseScenarioVersionReferenceV1.ParseExact(json),FundingScenarioReferenceKind.ComparatorOutput=>InvestmentCaseComparatorOutputReferenceV1.ParseExact(json),FundingScenarioReferenceKind.SelectedScenario=>SelectedScenarioReferenceV1.ParseExact(json),_=>throw new FormatException("unsupported_kind")};var producer=await scenarioPort.ValidateAsync(new(wrapper,kind,mode),context,cancellationToken).ConfigureAwait(false);return FundingScenarioContractEvaluator.EvaluateScenarioBytes(wrapperUtf8,kind,mode,context,producer);
    }
}
