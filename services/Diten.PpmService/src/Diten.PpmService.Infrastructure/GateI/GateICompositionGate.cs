using Diten.PpmService.Application.Features.BenefitCommitments.GateI.BenefitRealization;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.FundingScenario;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Diten.PpmService.Application.GateI;

namespace Diten.PpmService.Infrastructure.GateI;


public sealed class GateICompositionGate(IConfiguration configuration)
    : IGateIDecisionTraceLifecyclePolicy
{
    public const string CommonFlag = "GateI:Composition:Enabled";
    public const string DecisionTraceFlag = "GateI:DecisionTrace:Enabled";
    public const string FundingScenarioFlag = "GateI:FundingScenario:Enabled";
    public const string BenefitRealizationFlag = "GateI:BenefitRealization:Enabled";

    public bool RequiresGoverningDecision => IsEnabled(GateICompositionLane.DecisionTrace);

    public bool IsEnabled(GateICompositionLane lane)
    {
        if (!configuration.GetValue(CommonFlag, false)) return false;
        return configuration.GetValue(lane switch
        {
            GateICompositionLane.DecisionTrace => DecisionTraceFlag,
            GateICompositionLane.FundingScenario => FundingScenarioFlag,
            GateICompositionLane.BenefitRealization => BenefitRealizationFlag,
            _ => throw new ArgumentOutOfRangeException(nameof(lane))
        }, false);
    }
}
