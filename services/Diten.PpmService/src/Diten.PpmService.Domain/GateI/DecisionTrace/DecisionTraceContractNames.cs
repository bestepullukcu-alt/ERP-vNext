using System.Buffers;
using System.Text.Json;

namespace Diten.PpmService.Domain.GateI.DecisionTrace;


public static class DecisionTraceContractNames
{
    public const string Version = "1.0";
    public const string InvestmentCaseContext = "ppm.investment-case-context";
    public const string GoverningDecisionReference = "ppm.investment-case-governing-decision-reference";
    public const string SupportingDecisionReference = "ppm.investment-case-supporting-decision-reference";
    public const string DecisionRevisionReference = "management-governance.decision-reference";
}
