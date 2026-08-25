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
    private readonly IFu16AuthorizationTransactionSession _localAuthorization;
    private readonly IEntitlementStateVersionFence _versionFence;
    private readonly EntitlementAttestationConsumerOptions _options;
    private readonly TimeProvider _time;

    public EntitlementAttestationConsumer(IPlatformEntitlementAttestationProvider provider, IEntitlementAttestationTrustedKeyProvider keys,
        IFu16AuthorizationTransactionSession localAuthorization, IEntitlementStateVersionFence versionFence,
        EntitlementAttestationConsumerOptions options, TimeProvider time)
    { _provider = provider; _keys = keys; _localAuthorization = localAuthorization; _versionFence = versionFence; _options = options; _time = time; }

    public async Task<EntitlementAttestationValidationResult> EnforceAsync(
        EntitlementAttestationRequestV1 request, Fu16LocalAuthorizationSnapshot localSnapshot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_options.Enabled) return EntitlementAttestationValidationResult.Unavailable("entitlement_attestation_consumer_disabled");
        if (request.TenantId == Guid.Empty || !IsNormalizedModuleCode(request.ModuleCode) || !IsCanonicalHash(request.RequestHash))
            return EntitlementAttestationValidationResult.Unauthorized("entitlement_attestation_request_binding_invalid");

        EntitlementAttestationProviderResultV1 provided;
        try { provided = await _provider.GetAsync(request, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return EntitlementAttestationValidationResult.Unavailable("entitlement_attestation_provider_timeout"); }
        catch { return EntitlementAttestationValidationResult.Unavailable("entitlement_attestation_provider_unavailable"); }
        if (provided is EntitlementAttestationProviderResultV1.Failed failed)
            return EntitlementAttestationValidationResult.Unavailable($"entitlement_attestation_provider_{failed.Failure.ToString().ToLowerInvariant()}");

        var token = ((EntitlementAttestationProviderResultV1.Attested)provided).Token;
        var parsed = ReadHeaderAndPayload(token);
        if (parsed.Failure is not null) return EntitlementAttestationValidationResult.Unauthorized(parsed.Failure);

        var resolution = await _keys.ResolveAsync(EntitlementAttestationContractV1.Issuer, parsed.Kid!, cancellationToken);
        if (resolution.Kind is EntitlementAttestationKeyResolutionKind.Unavailable or EntitlementAttestationKeyResolutionKind.Indeterminate)
            return EntitlementAttestationValidationResult.Unavailable("entitlement_attestation_key_authority_unavailable");
        if (resolution.Kind != EntitlementAttestationKeyResolutionKind.Resolved || resolution.Key is null)
            return EntitlementAttestationValidationResult.Unauthorized("entitlement_attestation_kid_invalid");
        var key = resolution.Key;
        if (!key.Active || !string.Equals(key.Issuer, EntitlementAttestationContractV1.Issuer, StringComparison.Ordinal) ||
            !string.Equals(key.Kid, parsed.Kid, StringComparison.Ordinal))
            return EntitlementAttestationValidationResult.Unauthorized("entitlement_attestation_kid_invalid");
        if ((_options.RejectTestIdentities && key.IsTestOnly) || key.PublicKey.KeySize < 3072)
            return EntitlementAttestationValidationResult.Unauthorized("entitlement_attestation_key_profile_invalid");
        if (!Verify(token, key.PublicKey)) return EntitlementAttestationValidationResult.Unauthorized("entitlement_attestation_signature_invalid");

        var p = parsed.Payload!.Value;
        if (!ExactString(p, "iss", EntitlementAttestationContractV1.Issuer)) return EntitlementAttestationValidationResult.Unauthorized("entitlement_attestation_issuer_invalid");
        if (!ExactSingleAudience(p)) return EntitlementAttestationValidationResult.Unauthorized("entitlement_attestation_audience_invalid");
        if (!ExactString(p, "contract_id", EntitlementAttestationContractV1.ContractId) || !ExactString(p, "contract_version", EntitlementAttestationContractV1.ContractVersion))
            return EntitlementAttestationValidationResult.Unauthorized("entitlement_attestation_version_invalid");
        if (!ExactString(p, "tenant_id", request.TenantId.ToString("D")) || !ExactString(p, "module_code", request.ModuleCode) || !ExactString(p, "request_hash", request.RequestHash))
            return EntitlementAttestationValidationResult.Unauthorized("entitlement_attestation_binding_invalid");

        if (!TryReadVersion(p, out var version) || !TryTimestamp(p, "resolved_at_utc", out var resolved) ||
            !TryTimestamp(p, "iat", out var issued) || !TryTimestamp(p, "valid_until_utc", out var validUntil))
            return EntitlementAttestationValidationResult.Unavailable("entitlement_attestation_malformed");
        var now = _time.GetUtcNow();
        if (issued < resolved || validUntil <= issued || validUntil - issued > EntitlementAttestationContractV1.MaximumValidity ||
            validUntil - resolved > EntitlementAttestationContractV1.MaximumValidity || now < issued || now >= validUntil)
            return EntitlementAttestationValidationResult.Unavailable("entitlement_attestation_expired_or_invalid_time");
        if (!TryDecision(p, out var decision)) return EntitlementAttestationValidationResult.Unavailable("entitlement_attestation_indeterminate");
        var fence = await _versionFence.ObserveAsync(request.TenantId, request.ModuleCode, version, cancellationToken);
        if (fence != EntitlementVersionFenceResult.Accepted)
            return EntitlementAttestationValidationResult.Unavailable($"entitlement_attestation_version_{fence.ToString().ToLowerInvariant()}");
        if (decision != EntitlementAttestationDecisionV1.Active)
            return EntitlementAttestationValidationResult.Forbidden($"entitlement_{decision.ToString().ToLowerInvariant()}", decision, version, validUntil);

        var local = await _localAuthorization.ValidateAndConsumeAsync(localSnapshot, cancellationToken);
        return local.Kind switch
        {
            Fu16LocalAuthorizationResultKind.Accepted => EntitlementAttestationValidationResult.Continue(decision, version, validUntil),
            Fu16LocalAuthorizationResultKind.StaleOrConcurrent => EntitlementAttestationValidationResult.Conflict("auth_authorization_state_changed"),
            Fu16LocalAuthorizationResultKind.Unauthorized => EntitlementAttestationValidationResult.Unauthorized("auth_local_identity_invalid"),
            Fu16LocalAuthorizationResultKind.Forbidden => EntitlementAttestationValidationResult.Forbidden("auth_local_grant_denied", decision, version, validUntil),
            _ => EntitlementAttestationValidationResult.Unavailable("auth_local_authority_unavailable")
        };
    }

    private static (string? Kid, JsonElement? Payload, string? Failure) ReadHeaderAndPayload(string token)
    {
        if (string.IsNullOrEmpty(token) || Encoding.UTF8.GetByteCount(token) > MaximumTokenBytes) return (null, null, "entitlement_attestation_malformed");
        var parts = token.Split('.'); if (parts.Length != 3 || parts.Any(string.IsNullOrEmpty)) return (null, null, "entitlement_attestation_malformed");
        try
        {
            using var hd = JsonDocument.Parse(Decode(parts[0])); using var pd = JsonDocument.Parse(Decode(parts[1]));
            var props = hd.RootElement.EnumerateObject().ToArray();
            if (props.Any(x => x.Name is not ("alg" or "kid" or "typ")) || props.GroupBy(x => x.Name).Any(x => x.Count() != 1)) return (null, null, "entitlement_attestation_header_invalid");
            string? Get(string n) => props.SingleOrDefault(x => x.NameEquals(n)).Value.ValueKind == JsonValueKind.String ? props.Single(x => x.NameEquals(n)).Value.GetString() : null;
            if (!string.Equals(Get("alg"), EntitlementAttestationContractV1.Algorithm, StringComparison.Ordinal)) return (null, null, "entitlement_attestation_alg_invalid");
            if (!string.Equals(Get("typ"), EntitlementAttestationContractV1.Type, StringComparison.Ordinal)) return (null, null, "entitlement_attestation_typ_invalid");
            var kid = Get("kid"); if (string.IsNullOrWhiteSpace(kid) || kid != kid.Trim()) return (null, null, "entitlement_attestation_kid_invalid");
            return (kid, pd.RootElement.Clone(), null);
        }
        catch (Exception e) when (e is FormatException or JsonException or InvalidOperationException) { return (null, null, "entitlement_attestation_malformed"); }
    }

    private static bool Verify(string token, RSA rsa) { var p = token.Split('.'); try { return rsa.VerifyData(Encoding.ASCII.GetBytes($"{p[0]}.{p[1]}"), Decode(p[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1); } catch { return false; } }
    private static bool ExactString(JsonElement p, string name, string expected) => p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && string.Equals(v.GetString(), expected, StringComparison.Ordinal);
    private static bool ExactSingleAudience(JsonElement p) => p.TryGetProperty("aud", out var v) && v.ValueKind == JsonValueKind.String && string.Equals(v.GetString(), EntitlementAttestationContractV1.Audience, StringComparison.Ordinal);
    private static bool TryReadVersion(JsonElement p, out EntitlementStateVersionV1 v) { v = default; return p.TryGetProperty("physical_entitlement_version", out var a) && a.TryGetUInt64(out var av) && av > 0 && p.TryGetProperty("subscription_version", out var b) && b.TryGetUInt64(out var bv) && bv > 0 && p.TryGetProperty("module_applicability_version", out var c) && c.TryGetUInt64(out var cv) && cv > 0 && (v = new(av,bv,cv)).IsComplete; }
    private static bool TryDecision(JsonElement p, out EntitlementAttestationDecisionV1 d)
    { d = default; return p.TryGetProperty("decision", out var v) && v.ValueKind == JsonValueKind.String && Enum.TryParse(v.GetString(), false, out d); }
    private static bool TryTimestamp(JsonElement p, string name, out DateTimeOffset value) { value = default; return p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && DateTimeOffset.TryParseExact(v.GetString(), "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out value); }
    private static bool IsNormalizedModuleCode(string value) => !string.IsNullOrWhiteSpace(value) && value == value.Trim() && value == value.Normalize(NormalizationForm.FormC) && value == value.ToUpperInvariant();
    private static bool IsCanonicalHash(string value) { try { var b = Decode(value); return b.Length == 32 && Encode(b) == value; } catch { return false; } }
    private static byte[] Decode(string value) { if (value.Contains('=') || value.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))) throw new FormatException(); var s=value.Replace('-','+').Replace('_','/'); s += (s.Length%4) switch { 0=>"",2=>"==",3=>"=",_=>throw new FormatException()}; return Convert.FromBase64String(s); }
    private static string Encode(ReadOnlySpan<byte> value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+','-').Replace('/','_');
}
