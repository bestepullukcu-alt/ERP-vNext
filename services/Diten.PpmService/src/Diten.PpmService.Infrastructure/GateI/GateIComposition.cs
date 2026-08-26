using Diten.PpmService.Application.Features.BenefitCommitments.GateI.BenefitRealization;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.FundingScenario;
using Microsoft.Extensions.Configuration;

namespace Diten.PpmService.Infrastructure.GateI;

public enum GateICompositionLane
{
    DecisionTrace,
    FundingScenario,
    BenefitRealization
}

public sealed record GateICompositionResult(
    int StatusCode,
    string StableCode,
    int ProviderCalls,
    int RelationshipWrites,
    int ReceiptWrites,
    int AuditIntentWrites,
    int OutboxWrites);

public sealed class GateICompositionGate(IConfiguration configuration)
{
    public const string CommonFlag = "GateI:Composition:Enabled";
    public const string DecisionTraceFlag = "GateI:DecisionTrace:Enabled";
    public const string FundingScenarioFlag = "GateI:FundingScenario:Enabled";
    public const string BenefitRealizationFlag = "GateI:BenefitRealization:Enabled";

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

internal sealed class DefaultOffDecisionReferenceValidationPort(GateICompositionGate gate)
    : IDecisionReferenceValidationPort
{
    public Task<DecisionReferenceProviderResult> ValidateAsync(
        DecisionTraceValidationRequest request,
        DecisionTraceTrustedContext trustedContext,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = gate.IsEnabled(GateICompositionLane.DecisionTrace);
        return Task.FromResult(new DecisionReferenceProviderResult(DecisionReferenceProviderResultKind.Unavailable));
    }
}

internal sealed class DefaultOffBudgetVersionReferenceValidationPort(GateICompositionGate gate)
    : IBudgetVersionReferenceValidationPort
{
    public ValueTask<ProducerReferenceValidationResult> ValidateAsync(
        BudgetReferenceValidationRequest request,
        Fu16FundingScenarioContextV1 context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = gate.IsEnabled(GateICompositionLane.FundingScenario);
        return ValueTask.FromResult(new ProducerReferenceValidationResult(ProducerReferenceState.Unavailable));
    }
}

internal sealed class DefaultOffScenarioPlanningReferenceValidationPort(GateICompositionGate gate)
    : IScenarioPlanningReferenceValidationPort
{
    public ValueTask<ProducerReferenceValidationResult> ValidateAsync(
        ScenarioReferenceValidationRequest request,
        Fu16FundingScenarioContextV1 context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = gate.IsEnabled(GateICompositionLane.FundingScenario);
        return ValueTask.FromResult(new ProducerReferenceValidationResult(ProducerReferenceState.Unavailable));
    }
}

internal sealed class DefaultOffOutcomeReferenceAuthorityPort(GateICompositionGate gate)
    : IOutcomeReferenceAuthorityPort
{
    public Task<OutcomeReferenceAuthorityResult> ValidateAsync(
        OutcomeReferenceAuthorityRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = gate.IsEnabled(GateICompositionLane.BenefitRealization);
        return Task.FromResult(new OutcomeReferenceAuthorityResult(OutcomeReferenceAuthorityDisposition.Unavailable));
    }
}
