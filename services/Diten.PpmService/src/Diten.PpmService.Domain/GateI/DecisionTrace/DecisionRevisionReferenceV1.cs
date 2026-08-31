using System.Buffers;
using System.Text.Json;

namespace Diten.PpmService.Domain.GateI.DecisionTrace;


public sealed record DecisionRevisionReferenceV1
{
    public DecisionRevisionReferenceV1(Guid decisionId, Guid decisionRevisionId, int decisionRevisionNumber)
    {
        DecisionId = DecisionTraceGuard.Id(decisionId, nameof(decisionId));
        DecisionRevisionId = DecisionTraceGuard.Id(decisionRevisionId, nameof(decisionRevisionId));
        DecisionRevisionNumber = decisionRevisionNumber > 0 ? decisionRevisionNumber : throw new DecisionTraceContractException("DecisionRevisionNumber must be a positive integer.");
    }
    public string ContractName => DecisionTraceContractNames.DecisionRevisionReference;
    public string ContractVersion => DecisionTraceContractNames.Version;
    public Guid DecisionId { get; }
    public Guid DecisionRevisionId { get; }
    public int DecisionRevisionNumber { get; }
}
