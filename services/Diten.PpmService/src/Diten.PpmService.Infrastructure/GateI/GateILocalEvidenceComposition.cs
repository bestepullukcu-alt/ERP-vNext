using Diten.PpmService.Application.Features.BenefitCommitments.GateI.BenefitRealization;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.FundingScenario;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Diten.PpmService.Application.GateI;

namespace Diten.PpmService.Infrastructure.GateI;


public static class GateILocalEvidenceComposition
{
    public static IServiceCollection AddGateILocalEvidenceTestHost(
        this IServiceCollection services,
        Uri ownerBaseAddress,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(ownerBaseAddress);
        if (!ownerBaseAddress.IsAbsoluteUri || ownerBaseAddress.Scheme is not ("http" or "https"))
            throw new ArgumentException("The local-evidence owner base address must be absolute HTTP(S).", nameof(ownerBaseAddress));

        services.RemoveAll<IS2SOutboundProofProvider>();
        services.AddSingleton(S2SOutboundLocalEvidenceTestHost.CreateEphemeralProvider(timeProvider));
        services.AddHttpClient<GateIOwnerReferenceHttpClients>(client => client.BaseAddress = ownerBaseAddress);
        return services;
    }
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
        S2SFundingScenarioContextV1 context,
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
        S2SFundingScenarioContextV1 context,
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

internal sealed class DefaultOffGateIRelationshipAuthority(GateICompositionGate gate)
    : IGateIRelationshipAuthority
{
    public Task<GateIAuthorityValidationResult> ValidateAsync(
        GateIAuthorityValidationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var lane = request.Kind switch
        {
            GateIRelationshipKind.GoverningDecision or GateIRelationshipKind.SupportingDecision =>
                GateICompositionLane.DecisionTrace,
            GateIRelationshipKind.SelectedBudgetVersion or GateIRelationshipKind.ScenarioVersion
                or GateIRelationshipKind.ComparatorOutput or GateIRelationshipKind.SelectedScenario =>
                GateICompositionLane.FundingScenario,
            GateIRelationshipKind.BenefitOutcome => GateICompositionLane.BenefitRealization,
            _ => throw new ArgumentOutOfRangeException(nameof(request.Kind))
        };
        _ = gate.IsEnabled(lane);
        return Task.FromResult(new GateIAuthorityValidationResult(
            503,
            "ppm_gate_i_provider_not_composed",
            new string('0', 64),
            false));
    }
}
