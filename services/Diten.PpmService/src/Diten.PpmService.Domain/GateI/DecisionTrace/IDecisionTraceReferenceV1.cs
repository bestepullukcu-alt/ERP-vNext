using System.Buffers;
using System.Text.Json;

namespace Diten.PpmService.Domain.GateI.DecisionTrace;


public interface IDecisionTraceReferenceV1
{
    string ContractName { get; }
    string ContractVersion { get; }
    InvestmentCaseContextV1 InvestmentCaseContext { get; }
    DecisionRevisionReferenceV1 DecisionRevisionReference { get; }
}
