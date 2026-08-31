using System.Buffers;
using System.Text.Json;

namespace Diten.PpmService.Domain.GateI.DecisionTrace;


public sealed record SupportingDecisionReferenceV1 : IDecisionTraceReferenceV1
{
    public SupportingDecisionReferenceV1(InvestmentCaseContextV1 investmentCaseContext, DecisionRevisionReferenceV1 decisionRevisionReference)
    { InvestmentCaseContext = investmentCaseContext ?? throw new DecisionTraceContractException("InvestmentCaseContext is required."); DecisionRevisionReference = decisionRevisionReference ?? throw new DecisionTraceContractException("DecisionRevisionReference is required."); }
    public string ContractName => DecisionTraceContractNames.SupportingDecisionReference;
    public string ContractVersion => DecisionTraceContractNames.Version;
    public InvestmentCaseContextV1 InvestmentCaseContext { get; }
    public DecisionRevisionReferenceV1 DecisionRevisionReference { get; }
}
