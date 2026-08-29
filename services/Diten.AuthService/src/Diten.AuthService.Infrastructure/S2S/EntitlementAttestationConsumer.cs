using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.AuthService.Application.S2S;

namespace Diten.AuthService.Infrastructure.S2S;

public sealed class EntitlementAttestationConsumerOptions
{
    public bool Enabled { get; set; }
    public bool RejectTestIdentities { get; set; } = true;
}

public sealed class EntitlementAttestationConsumer
{
    private const int MaximumTokenBytes = 16 * 1024;
    private readonly IPlatformEntitlementAttestationProvider _provider;
    private readonly IEntitlementAttestationTrustedKeyProvider _keys;
    private readonly IS2SAuthorizationTransactionSession _localAuthorization;
    private readonly IEntitlementStateVersionFence _versionFence;
    private readonly EntitlementAttestationConsumerOptions _options;
    private readonly TimeProvider _time;

    public EntitlementAttestationConsumer(
        IPlatformEntitlementAttestationProvider provider,
        IEntitlementAttestationTrustedKeyProvider keys,
        IS2SAuthorizationTransactionSession localAuthorization,
        IEntitlementStateVersionFence versionFence,
        EntitlementAttestationConsumerOptions options,
        TimeProvider time)
    {
        _provider = provider;
        _keys = keys;
        _localAuthorization = localAuthorization;
        _versionFence = versionFence;
        _options = options;
        _time = time;
    }

    public async Task<EntitlementAttestationValidationResult> EnforceAsync(
        EntitlementAttestationRequestV1 request,
        S2SLocalAuthorizationSnapshot localSnapshot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_options.Enabled)
            return EntitlementAttestationValidationResult.Unavailable("entitlement_attestation_consumer_disabled");

        if (request.TenantId == Guid.Empty ||
            !IsNormalizedModuleCode(request.ModuleCode) ||
            !IsCanonicalHash(request.RequestHash))
            return EntitlementAttestationValidationResult.Unauthorized("entitlement_attestation_request_binding_invalid");

        EntitlementAttestationProviderResultV1 provided;
        try
        {
            provided = await _provider.GetAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return EntitlementAttestationValidationResult.Unavailable("entitlement_attestation_provider_timeout");
        }
        catch
        {
            return EntitlementAttestationValidationResult.Unavailable("entitlement_attestation_provider_unavailable");
        }

        if (provided is EntitlementAttestationProviderResultV1.Failed failed)
            return EntitlementAttestationValidationResult.Unavailable(
                $"entitlement_attestation_provider_{failed.Failure.ToString().ToLowerInvariant()}");

        var token = ((EntitlementAttestationProviderResultV1.Attested)provided).Token;
        var parsed = ReadHeaderAndPayload(token);
        if (parsed.Failure is not null)
            return EntitlementAttestationValidationResult.Unauthorized(parsed.Failure);

        var resolution = await _keys.ResolveAsync(
            EntitlementAttestationContractV1.Issuer,
            parsed.Kid!,
            cancellationToken);

        if (resolution.Kind is EntitlementAttestationKeyResolutionKind.Unavailable or
            EntitlementAttestationKeyResolutionKind.Indeterminate)
            return EntitlementAttestationValidationResult.Unavailable("entitlement_attestation_key_authority_unavailable");

        if (resolution.Kind != EntitlementAttestationKeyResolutionKind.Resolved || resolution.Key is null)
            return EntitlementAttestationValidationResult.Unauthorized("entitlement_attestation_kid_invalid");

        var key = resolution.Key;
        if (!key.Active ||
            !string.Equals(key.Issuer, EntitlementAttestationContractV1.Issuer, StringComparison.Ordinal) ||
            !string.Equals(key.Kid, parsed.Kid, StringComparison.Ordinal))
            return EntitlementAttestationValidationResult.Unauthorized("entitlement_attestation_kid_invalid");

        if ((_options.RejectTestIdentities && key.IsTestOnly) || key.PublicKey.KeySize < 3072)
            return EntitlementAttestationValidationResult.Unauthorized("entitlement_attestation_key_profile_invalid");

        if (!Verify(token, key.PublicKey))
            return EntitlementAttestationValidationResult.Unauthorized("entitlement_attestation_signature_invalid");

        var payload = parsed.Payload!.Value;
        if (!ExactString(payload, "iss", EntitlementAttestationContractV1.Issuer))
            return EntitlementAttestationValidationResult.Unauthorized("entitlement_attestation_issuer_invalid");
        if (!ExactSingleAudience(payload))
            return EntitlementAttestationValidationResult.Unauthorized("entitlement_attestation_audience_invalid");
        if (!ExactString(payload, "contract_id", EntitlementAttestationContractV1.ContractId) ||
            !ExactString(payload, "contract_version", EntitlementAttestationContractV1.ContractVersion))
            return EntitlementAttestationValidationResult.Unauthorized("entitlement_attestation_version_invalid");
        if (!ExactString(payload, "tenant_id", request.TenantId.ToString("D")) ||
            !ExactString(payload, "module_code", request.ModuleCode) ||
            !ExactString(payload, "request_hash", request.RequestHash))
            return EntitlementAttestationValidationResult.Unauthorized("entitlement_attestation_binding_invalid");

        if (!TryReadVersion(payload, out var version) ||
            !TryTimestamp(payload, "resolved_at_utc", out var resolved) ||
            !TryTimestamp(payload, "iat", out var issued) ||
            !TryTimestamp(payload, "valid_until_utc", out var validUntil))
            return EntitlementAttestationValidationResult.Unavailable("entitlement_attestation_malformed");

        var now = _time.GetUtcNow();
        if (issued < resolved ||
            validUntil <= issued ||
            validUntil - issued > EntitlementAttestationContractV1.MaximumValidity ||
            validUntil - resolved > EntitlementAttestationContractV1.MaximumValidity ||
            now < issued ||
            now >= validUntil)
            return EntitlementAttestationValidationResult.Unavailable("entitlement_attestation_expired_or_invalid_time");

        if (!TryDecision(payload, out var decision))
            return EntitlementAttestationValidationResult.Unavailable("entitlement_attestation_indeterminate");

        var fence = await _versionFence.ObserveAsync(
            request.TenantId,
            request.ModuleCode,
            version,
            cancellationToken);

        if (fence != EntitlementVersionFenceResult.Accepted)
        {
            return fence switch
            {
                EntitlementVersionFenceResult.Older =>
                    EntitlementAttestationValidationResult.Conflict("entitlement_attestation_version_older"),
                EntitlementVersionFenceResult.Incomparable =>
                    EntitlementAttestationValidationResult.Unavailable("entitlement_attestation_version_incomparable"),
                EntitlementVersionFenceResult.AuthorityUnavailable =>
                    EntitlementAttestationValidationResult.Unavailable("entitlement_attestation_version_authority_unavailable"),
                _ => EntitlementAttestationValidationResult.Unavailable("entitlement_attestation_version_unknown")
            };
        }

        if (decision != EntitlementAttestationDecisionV1.Active)
            return EntitlementAttestationValidationResult.Forbidden(
                $"entitlement_{decision.ToString().ToLowerInvariant()}",
                decision,
                version,
                validUntil);

        var local = await _localAuthorization.ValidateAndConsumeAsync(localSnapshot, cancellationToken);
        return local.Kind switch
        {
            S2SLocalAuthorizationResultKind.Accepted =>
                EntitlementAttestationValidationResult.Continue(decision, version, validUntil),
            S2SLocalAuthorizationResultKind.StaleOrConcurrent =>
                EntitlementAttestationValidationResult.Conflict("auth_authorization_state_changed"),
            S2SLocalAuthorizationResultKind.Unauthorized =>
                EntitlementAttestationValidationResult.Unauthorized("auth_local_identity_invalid"),
            S2SLocalAuthorizationResultKind.Forbidden =>
                EntitlementAttestationValidationResult.Forbidden("auth_local_grant_denied", decision, version, validUntil),
            _ => EntitlementAttestationValidationResult.Unavailable("auth_local_authority_unavailable")
        };
    }

    private static (string? Kid, JsonElement? Payload, string? Failure) ReadHeaderAndPayload(string token)
    {
        if (string.IsNullOrEmpty(token) || Encoding.UTF8.GetByteCount(token) > MaximumTokenBytes)
            return (null, null, "entitlement_attestation_malformed");

        var parts = token.Split('.');
        if (parts.Length != 3 || parts.Any(string.IsNullOrEmpty))
            return (null, null, "entitlement_attestation_malformed");

        try
        {
            using var headerDocument = JsonDocument.Parse(Decode(parts[0]));
            using var payloadDocument = JsonDocument.Parse(Decode(parts[1]));
            var properties = headerDocument.RootElement.EnumerateObject().ToArray();
            if (properties.Any(x => x.Name is not ("alg" or "kid" or "typ")) ||
                properties.GroupBy(x => x.Name).Any(x => x.Count() != 1))
                return (null, null, "entitlement_attestation_header_invalid");

            string? Get(string name) =>
                properties.SingleOrDefault(x => x.NameEquals(name)).Value.ValueKind == JsonValueKind.String
                    ? properties.Single(x => x.NameEquals(name)).Value.GetString()
                    : null;

            if (!string.Equals(Get("alg"), EntitlementAttestationContractV1.Algorithm, StringComparison.Ordinal))
                return (null, null, "entitlement_attestation_alg_invalid");
            if (!string.Equals(Get("typ"), EntitlementAttestationContractV1.Type, StringComparison.Ordinal))
                return (null, null, "entitlement_attestation_typ_invalid");

            var kid = Get("kid");
            if (string.IsNullOrWhiteSpace(kid) || kid != kid.Trim())
                return (null, null, "entitlement_attestation_kid_invalid");

            return (kid, payloadDocument.RootElement.Clone(), null);
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException)
        {
            return (null, null, "entitlement_attestation_malformed");
        }
    }

    private static bool Verify(string token, RSA rsa)
    {
        var parts = token.Split('.');
        try
        {
            return rsa.VerifyData(
                Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"),
                Decode(parts[2]),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }

    private static bool ExactString(JsonElement payload, string name, string expected) =>
        payload.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        string.Equals(value.GetString(), expected, StringComparison.Ordinal);

    private static bool ExactSingleAudience(JsonElement payload) =>
        payload.TryGetProperty("aud", out var value) &&
        value.ValueKind == JsonValueKind.String &&
        string.Equals(value.GetString(), EntitlementAttestationContractV1.Audience, StringComparison.Ordinal);

    private static bool TryReadVersion(JsonElement payload, out EntitlementStateVersionV1 version)
    {
        version = default;
        return payload.TryGetProperty("physical_entitlement_version", out var physical) &&
               physical.TryGetUInt64(out var physicalValue) && physicalValue > 0 &&
               payload.TryGetProperty("subscription_version", out var subscription) &&
               subscription.TryGetUInt64(out var subscriptionValue) && subscriptionValue > 0 &&
               payload.TryGetProperty("module_applicability_version", out var applicability) &&
               applicability.TryGetUInt64(out var applicabilityValue) && applicabilityValue > 0 &&
               (version = new(physicalValue, subscriptionValue, applicabilityValue)).IsComplete;
    }

    private static bool TryDecision(JsonElement payload, out EntitlementAttestationDecisionV1 decision)
    {
        decision = default;
        return payload.TryGetProperty("decision", out var value) &&
               value.ValueKind == JsonValueKind.String &&
               Enum.TryParse(value.GetString(), false, out decision);
    }

    private static bool TryTimestamp(JsonElement payload, string name, out DateTimeOffset value)
    {
        value = default;
        return payload.TryGetProperty(name, out var element) &&
               element.ValueKind == JsonValueKind.String &&
               DateTimeOffset.TryParseExact(
                   element.GetString(),
                   "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.AssumeUniversal,
                   out value);
    }

    private static bool IsNormalizedModuleCode(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value == value.Trim() &&
        value == value.Normalize(NormalizationForm.FormC) &&
        value == value.ToUpperInvariant();

    private static bool IsCanonicalHash(string value)
    {
        try
        {
            var bytes = Decode(value);
            return bytes.Length == 32 && Encode(bytes) == value;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] Decode(string value)
    {
        if (value.Contains('=') || value.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
            throw new FormatException();

        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch
        {
            0 => "",
            2 => "==",
            3 => "=",
            _ => throw new FormatException()
        };
        return Convert.FromBase64String(padded);
    }

    private static string Encode(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
