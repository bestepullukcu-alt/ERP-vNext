using System.Buffers;
using System.Text.Json;

namespace Diten.PpmService.Domain.GateI.DecisionTrace;


public sealed record GoverningDecisionReferenceV1 : IDecisionTraceReferenceV1
{
    public GoverningDecisionReferenceV1(InvestmentCaseContextV1 investmentCaseContext, DecisionRevisionReferenceV1 decisionRevisionReference)
    { InvestmentCaseContext = investmentCaseContext ?? throw new DecisionTraceContractException("InvestmentCaseContext is required."); DecisionRevisionReference = decisionRevisionReference ?? throw new DecisionTraceContractException("DecisionRevisionReference is required."); }
    public string ContractName => DecisionTraceContractNames.GoverningDecisionReference;
    public string ContractVersion => DecisionTraceContractNames.Version;
    public InvestmentCaseContextV1 InvestmentCaseContext { get; }
    public DecisionRevisionReferenceV1 DecisionRevisionReference { get; }
}
