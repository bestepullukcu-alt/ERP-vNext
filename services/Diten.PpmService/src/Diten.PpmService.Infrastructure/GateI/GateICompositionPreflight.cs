using Diten.PpmService.Application.Features.BenefitCommitments.GateI.BenefitRealization;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.FundingScenario;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Diten.PpmService.Application.GateI;

namespace Diten.PpmService.Infrastructure.GateI;


public sealed class GateICompositionPreflight(GateICompositionGate gate)
{
    public const string Disabled = "ppm_gate_i_composition_disabled";
    public const string ProviderUnavailable = "ppm_gate_i_provider_not_composed";
    public const string ExcludedApprovalRequired = "ppm_gate_i_mod_0023_excluded_v1";

    public GateICompositionResult Evaluate(GateICompositionLane lane, bool requiresExcludedApproval)
    {
        if (requiresExcludedApproval) return Fail(ExcludedApprovalRequired);
        return gate.IsEnabled(lane) ? Fail(ProviderUnavailable) : Fail(Disabled);
    }

    private static GateICompositionResult Fail(string code) =>
        new(503, code, 0, 0, 0, 0, 0);
}
