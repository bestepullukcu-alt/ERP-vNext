using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.GateI.DecisionTrace;

namespace Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;

public interface IGateIDecisionTraceLifecyclePolicy
{
    bool RequiresGoverningDecision { get; }
}

public sealed class DisabledGateIDecisionTraceLifecyclePolicy : IGateIDecisionTraceLifecyclePolicy
{
    public static DisabledGateIDecisionTraceLifecyclePolicy Instance { get; } = new();
    private DisabledGateIDecisionTraceLifecyclePolicy() { }
    public bool RequiresGoverningDecision => false;
}

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

public static class DecisionTraceProducerProfile
{
    public const string Operation = "decision-registry.decision-references.validate.v1";
    public const string Permission = "management-governance.decision-references.validate";
    public const string Audience = "diten-management-governance-service";
    public const string ClientId = "diten.management-governance";
    public const string Issuer = "diten-auth-service";
    public const string OwnerModule = "MOD-0007";
    public const string ProtocolScope = "diten.s2s.delegated.invoke";
    public const string TokenFamily = "DelegatedActorProofV1";
}

public enum TrustedAuthorityState { Current, Denied, Stale, Unavailable }

public sealed record DecisionTraceTrustedContext(
    Guid TenantId, Guid EffectiveActorId, Guid CorrelationId,
    string Issuer, string Audience, string ClientId, string TokenFamily, string ProtocolScope,
    string OwnerModule, string Operation, string Permission, string RequestHash,
    bool AuthenticatedServiceFamily, bool TokenFamilyValidated, bool DelegatedActorProofValidated,
    Guid DelegatedActorId, TrustedAuthorityState EntitlementState, TrustedAuthorityState ExplicitTenantGrantState,
    TrustedAuthorityState PrincipalFreshness, TrustedAuthorityState CredentialFreshness, TrustedAuthorityState AuthorizationFreshness)
{
    public bool HasRequiredIdentifiers => TenantId != Guid.Empty && EffectiveActorId != Guid.Empty && CorrelationId != Guid.Empty;
}

public sealed record DecisionTraceValidationRequest(IDecisionTraceReferenceV1 Reference, DecisionTraceValidationMode Mode);
public sealed record DecisionTraceRequestBindingInput(string Method, string Path);

public static class DecisionTraceRequestBinding
{
    private static readonly byte[] DomainSeparator = Encoding.UTF8.GetBytes("DITEN-S2S-REQUEST-V1");

    public static string Compute(DecisionTraceRequestBindingInput input, Guid tenantId, DecisionTraceValidationRequest request)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(request.Reference);
        if (string.IsNullOrEmpty(input.Method) || !string.Equals(input.Method, input.Method.ToUpperInvariant(), StringComparison.Ordinal)) throw new ArgumentException("HTTP method must be exact uppercase.", nameof(input));
        if (string.IsNullOrEmpty(input.Path) || !input.Path.StartsWith("/", StringComparison.Ordinal) || input.Path.Contains("?", StringComparison.Ordinal)) throw new ArgumentException("Request path must be exact and query-free.", nameof(input));
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant identity is required.", nameof(tenantId));
        using var stream = new MemoryStream();
        stream.Write(DomainSeparator);
        WriteField(stream, Encoding.UTF8.GetBytes(input.Method));
        WriteField(stream, Encoding.UTF8.GetBytes(input.Path));
        WriteField(stream, Encoding.UTF8.GetBytes(tenantId.ToString("D")));
        WriteField(stream, Encoding.UTF8.GetBytes(DecisionTraceProducerProfile.Operation));
        WriteField(stream, SHA256.HashData(CanonicalBody(request)));
        return Base64Url(SHA256.HashData(stream.ToArray()));
    }

    public static bool FixedTimeMatches(string? supplied, string expected)
    {
        if (!TryDecode(supplied, out var actual) || !TryDecode(expected, out var expectedBytes) || actual.Length != expectedBytes.Length) return false;
        return CryptographicOperations.FixedTimeEquals(actual, expectedBytes);
    }

    private static byte[] CanonicalBody(DecisionTraceValidationRequest request)
    {
        using var stream = new MemoryStream(); using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject(); writer.WriteString("Mode", request.Mode.ToString()); writer.WritePropertyName("Reference"); writer.WriteRawValue(DecisionTraceReferenceCodec.Serialize(request.Reference)); writer.WriteEndObject(); writer.Flush();
        return stream.ToArray();
    }
    private static void WriteField(Stream stream, ReadOnlySpan<byte> value) { Span<byte> length = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(length, value.Length); stream.Write(length); stream.Write(value); }
    private static string Base64Url(ReadOnlySpan<byte> value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static bool TryDecode(string? value, out byte[] bytes)
    {
        bytes = [];
        if (value is null || value.Length != 43 || value.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))) return false;
        try { bytes = Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + "="); return bytes.Length == 32; } catch (FormatException) { return false; }
    }
}

public enum DecisionReferenceDisposition { Published, Withdrawn, Superseded }
public enum DecisionReferenceProviderResultKind { Resolved, AuthenticationFailure, PermissionDenied, NotFound, Ineligible, Stale, Conflict, UnsupportedVersion, Timeout, Unavailable, Malformed, Indeterminate }
public sealed record DecisionReferenceProviderResult(DecisionReferenceProviderResultKind Kind, DecisionRevisionReferenceV1? Reference = null, DecisionTraceValidationMode? Mode = null, bool? Resolved = null, bool? EligibleForNewReference = null, DecisionReferenceDisposition? Disposition = null);
public interface IDecisionReferenceValidationPort { Task<DecisionReferenceProviderResult> ValidateAsync(DecisionTraceValidationRequest request, DecisionTraceTrustedContext trustedContext, CancellationToken cancellationToken); }

public static class DecisionTraceFailureCodes
{
    public const string MalformedRequest = "gate_i_decision_trace_malformed_request";
    public const string AuthenticationFailure = "gate_i_decision_trace_authentication_failure";
    public const string PermissionDenied = "gate_i_decision_trace_permission_denied";
    public const string NotFound = "gate_i_decision_trace_not_found";
    public const string Conflict = "gate_i_decision_trace_conflict";
    public const string DependencyUnavailable = "gate_i_decision_trace_dependency_unavailable";
    public const string NonRuntimeContractOnly = "gate_i_decision_trace_non_runtime_contract_only";
}

public sealed record DecisionTraceValidationOutcome(int StatusCode, string? FailureCode, DecisionRevisionReferenceV1? Reference, DecisionTraceValidationMode? Mode, bool? Resolved, bool? EligibleForNewReference, DecisionReferenceDisposition? Disposition) { public bool IsSuccess => StatusCode == 200; }

public sealed class DecisionTraceValidationService(IDecisionReferenceValidationPort provider)
{
    public async Task<DecisionTraceValidationOutcome> ValidateAsync(DecisionTraceValidationRequest? request, DecisionTraceRequestBindingInput bindingInput, DecisionTraceTrustedContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!Authenticated(context)) return Failure(401, DecisionTraceFailureCodes.AuthenticationFailure);
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

public static class DecisionTraceReadOnlyContract
{
    public const bool NonRuntimeContractOnly = true;
    public const bool RequiresIdempotencyKey = false;
    public const bool PersistsReceipt = false;
    public const bool PersistsAuditIntent = false;
    public const bool PersistsOutbox = false;
    public const bool PersistsCache = false;
    public const bool UsesLastKnownGoodAllow = false;
    public const bool AccessesProducerPersistence = false;
    public const bool MutatesInvestmentCase = false;
}
