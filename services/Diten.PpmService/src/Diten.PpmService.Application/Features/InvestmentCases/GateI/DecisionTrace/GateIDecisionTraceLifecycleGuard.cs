using System.Text;
using System.Text.Json;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.GateI.DecisionTrace;

namespace Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;


public static class GateIDecisionTraceLifecycleGuard
{
    public static void Validate(
        InvestmentCase investmentCase,
        InvestmentCaseLifecycleState target,
        IGateIDecisionTraceLifecyclePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(investmentCase);
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.RequiresGoverningDecision
            && investmentCase.LifecycleState == InvestmentCaseLifecycleState.UnderAnalysis
            && target == InvestmentCaseLifecycleState.Closed
            && investmentCase.GoverningDecisionReference is null)
        {
            throw new InvalidOperationException(
                "A governing decision reference is required before closing the investment case while Gate I Decision Trace is enabled.");
        }
    }
}
