using System.Text;
using System.Text.Json;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.GateI.DecisionTrace;

namespace Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;


public sealed class DecisionTraceValidationService(IDecisionReferenceValidationPort provider)
{
    public async Task<DecisionTraceValidationOutcome> ValidateAsync(DecisionTraceValidationRequest? request, DecisionTraceRequestBindingInput bindingInput, DecisionTraceTrustedContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!Authenticated(context)) return Failure(401, DecisionTraceFailureCodes.AuthenticationFailure);
        if (!Exact(bindingInput.Method, S2SOutboundReceiverProfiles.DecisionRegistry.Method)
            || !Exact(bindingInput.Path, S2SOutboundReceiverProfiles.DecisionRegistry.Path))
            return Failure(401, DecisionTraceFailureCodes.AuthenticationFailure);
        if (request?.Reference is null || request.Mode is not (DecisionTraceValidationMode.HistoricalResolve or DecisionTraceValidationMode.NewReferenceEligibility) || !IsKnownWrapper(request.Reference)) return Failure(400, DecisionTraceFailureCodes.MalformedRequest);
        string expectedHash;
        try { expectedHash = DecisionTraceRequestBinding.Compute(bindingInput, context.TenantId, request); } catch (ArgumentException) { return Failure(400, DecisionTraceFailureCodes.MalformedRequest); }
        if (!DecisionTraceRequestBinding.FixedTimeMatches(context.RequestHash, expectedHash)) return Failure(401, DecisionTraceFailureCodes.AuthenticationFailure);
        var denied = Authorize(context); if (denied is not null) return denied;
        DecisionReferenceProviderResult result;
        try { result = await provider.ValidateAsync(request, context, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return Failure(503, DecisionTraceFailureCodes.DependencyUnavailable); }
        if (result is null) return Failure(503, DecisionTraceFailureCodes.DependencyUnavailable);
        var mapped = Map(request, result);
        if (mapped.IsSuccess && DecisionTraceReadOnlyContract.NonRuntimeContractOnly) return new(503, DecisionTraceFailureCodes.NonRuntimeContractOnly, null, null, null, null, null);
        return mapped;
    }

    private static bool Authenticated(DecisionTraceTrustedContext value) => value.HasRequiredIdentifiers && value.AuthenticatedServiceFamily && value.TokenFamilyValidated && value.DelegatedActorProofValidated && value.DelegatedActorId != Guid.Empty && value.DelegatedActorId == value.EffectiveActorId && Exact(value.Issuer, DecisionTraceProducerProfile.Issuer) && Exact(value.Audience, DecisionTraceProducerProfile.Audience) && Exact(value.ClientId, DecisionTraceProducerProfile.ClientId) && Exact(value.TokenFamily, DecisionTraceProducerProfile.TokenFamily) && Exact(value.ProtocolScope, DecisionTraceProducerProfile.ProtocolScope);
    private static DecisionTraceValidationOutcome? Authorize(DecisionTraceTrustedContext value)
    {
        if (value.PrincipalFreshness == TrustedAuthorityState.Unavailable || value.CredentialFreshness == TrustedAuthorityState.Unavailable || value.AuthorizationFreshness == TrustedAuthorityState.Unavailable || value.EntitlementState == TrustedAuthorityState.Unavailable || value.ExplicitTenantGrantState == TrustedAuthorityState.Unavailable) return Failure(503, DecisionTraceFailureCodes.DependencyUnavailable);
        if (value.PrincipalFreshness == TrustedAuthorityState.Stale || value.CredentialFreshness == TrustedAuthorityState.Stale || value.AuthorizationFreshness == TrustedAuthorityState.Stale) return Failure(409, DecisionTraceFailureCodes.Conflict);
        if (!Exact(value.OwnerModule, DecisionTraceProducerProfile.OwnerModule) || !Exact(value.Operation, DecisionTraceProducerProfile.Operation) || !Exact(value.Permission, DecisionTraceProducerProfile.Permission) || value.EntitlementState != TrustedAuthorityState.Current || value.ExplicitTenantGrantState != TrustedAuthorityState.Current) return Failure(403, DecisionTraceFailureCodes.PermissionDenied);
        return null;
    }

    private static DecisionTraceValidationOutcome Map(DecisionTraceValidationRequest request, DecisionReferenceProviderResult result) => result.Kind switch
    {
        DecisionReferenceProviderResultKind.AuthenticationFailure => Failure(401, DecisionTraceFailureCodes.AuthenticationFailure),
        DecisionReferenceProviderResultKind.PermissionDenied => Failure(403, DecisionTraceFailureCodes.PermissionDenied),
        DecisionReferenceProviderResultKind.NotFound => Failure(404, DecisionTraceFailureCodes.NotFound),
        DecisionReferenceProviderResultKind.Ineligible or DecisionReferenceProviderResultKind.Stale or DecisionReferenceProviderResultKind.Conflict => Failure(409, DecisionTraceFailureCodes.Conflict),
        DecisionReferenceProviderResultKind.UnsupportedVersion or DecisionReferenceProviderResultKind.Timeout or DecisionReferenceProviderResultKind.Unavailable or DecisionReferenceProviderResultKind.Malformed or DecisionReferenceProviderResultKind.Indeterminate => Failure(503, DecisionTraceFailureCodes.DependencyUnavailable),
        DecisionReferenceProviderResultKind.Resolved => MapResolved(request, result), _ => Failure(503, DecisionTraceFailureCodes.DependencyUnavailable)
    };
    private static DecisionTraceValidationOutcome MapResolved(DecisionTraceValidationRequest request, DecisionReferenceProviderResult result)
    {
        if (result.Reference is null || result.Mode != request.Mode || result.Resolved != true || result.Disposition is null || result.Reference != request.Reference.DecisionRevisionReference) return Failure(503, DecisionTraceFailureCodes.DependencyUnavailable);
        if (request.Mode == DecisionTraceValidationMode.NewReferenceEligibility && (result.EligibleForNewReference != true || result.Disposition != DecisionReferenceDisposition.Published)) return Failure(409, DecisionTraceFailureCodes.Conflict);
        if (result.EligibleForNewReference is null) return Failure(503, DecisionTraceFailureCodes.DependencyUnavailable);
        return new(200, null, result.Reference, result.Mode, result.Resolved, result.EligibleForNewReference, result.Disposition);
    }
    private static bool IsKnownWrapper(IDecisionTraceReferenceV1 reference) => reference is GoverningDecisionReferenceV1 && Exact(reference.ContractName, DecisionTraceContractNames.GoverningDecisionReference) || reference is SupportingDecisionReferenceV1 && Exact(reference.ContractName, DecisionTraceContractNames.SupportingDecisionReference);
    private static bool Exact(string? left, string right) => string.Equals(left, right, StringComparison.Ordinal);
    private static DecisionTraceValidationOutcome Failure(int status, string code) => new(status, code, null, null, null, null, null);
}
