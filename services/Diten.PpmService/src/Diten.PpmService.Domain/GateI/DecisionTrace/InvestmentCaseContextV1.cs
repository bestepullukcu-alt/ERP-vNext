using System.Buffers;
using System.Text.Json;

namespace Diten.PpmService.Domain.GateI.DecisionTrace;


public sealed record InvestmentCaseContextV1
{
    public InvestmentCaseContextV1(Guid investmentCaseId) => InvestmentCaseId = DecisionTraceGuard.Id(investmentCaseId, nameof(investmentCaseId));
    public string ContractName => DecisionTraceContractNames.InvestmentCaseContext;
    public string ContractVersion => DecisionTraceContractNames.Version;
    public Guid InvestmentCaseId { get; }
}
