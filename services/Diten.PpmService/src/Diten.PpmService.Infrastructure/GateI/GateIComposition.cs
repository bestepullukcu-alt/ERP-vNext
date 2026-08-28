using Diten.PpmService.Application.Features.BenefitCommitments.GateI.BenefitRealization;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.FundingScenario;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Diten.PpmService.Application.GateI;
using Diten.Platform.Common.Authorization.Fu16;

namespace Diten.PpmService.Infrastructure.GateI;

internal sealed class UnavailableFu16TrustedRequestContextAccessor : IFu16TrustedRequestContextAccessor
{
    public Fu16TrustedRequestContext? Current => null;

    public void Publish(Fu16TrustedRequestContext context) =>
        throw new InvalidOperationException("FU16 trusted request context is not composed.");
}

internal sealed class PlatformCommonGateITrustedMutationContextAccessor(
    IFu16TrustedRequestContextAccessor accessor) : IGateITrustedMutationContextAccessor
{
    public GateITrustedMutationContext? Current
    {
        get
        {
            Fu16TrustedRequestContext trusted;
            try
            {
                trusted = accessor.Current?.Validate()
                    ?? throw new ArgumentException("FU16 trusted context is unavailable.");
            }
            catch (ArgumentException)
            {
                return null;
            }

            return new GateITrustedMutationContext(
                trusted.TenantId,
                trusted.EffectiveActorId,
                trusted.DelegatedActorId,
                trusted.DelegationId,
                trusted.ServicePrincipalId,
                trusted.CredentialId,
                trusted.ClientId,
                trusted.Issuer,
                trusted.Audience,
                trusted.TokenType,
                trusted.ProtocolScope,
                trusted.OperationId,
                trusted.Permissions,
                trusted.RequestHash,
                trusted.TenantGrantVersion,
                trusted.ServicePrincipalVersion,
                trusted.CredentialGeneration);
        }
    }
}

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

        services.RemoveAll<IFu16OutboundProofProvider>();
        services.AddSingleton(Fu16OutboundLocalEvidenceTestHost.CreateEphemeralProvider(timeProvider));
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
