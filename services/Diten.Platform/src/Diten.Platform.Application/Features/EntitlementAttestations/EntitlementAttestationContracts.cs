using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Diten.Platform.Application.Features.EntitlementAttestations;

public enum EntitlementDecisionV1 { Active, Disabled, Expired, Missing, NotApplicable }
public enum EntitlementDecisionFailureV1 { ProviderDisabled, ProviderUnavailable, Timeout, MalformedAuthority, Indeterminate }

public readonly record struct EntitlementStateVersionV1(
    ulong PhysicalEntitlementVersion,
    ulong SubscriptionVersion,
    ulong ModuleApplicabilityVersion)
{
    public bool IsComplete => PhysicalEntitlementVersion > 0 && SubscriptionVersion > 0 && ModuleApplicabilityVersion > 0;
}

public sealed record EntitlementDecisionRequestV1(Guid TenantId, string ModuleCode, string RequestHash);
public sealed record EntitlementDecisionSnapshotV1(
    Guid TenantId, string ModuleCode, string RequestHash, EntitlementDecisionV1 Decision,
    EntitlementStateVersionV1 Version, DateTimeOffset ResolvedAtUtc);
public sealed record EntitlementAttestationV1(
    EntitlementDecisionSnapshotV1 Decision, DateTimeOffset ValidUntilUtc, string KeyId, string Token);

public abstract record EntitlementDecisionResultV1
{
    private EntitlementDecisionResultV1() { }
    public sealed record Authoritative(EntitlementDecisionSnapshotV1 Snapshot) : EntitlementDecisionResultV1;
    public sealed record ServiceUnavailable(EntitlementDecisionFailureV1 Failure) : EntitlementDecisionResultV1;
}

public interface IPlatformEntitlementDecisionProvider
{
    Task<EntitlementDecisionResultV1> DecideAsync(EntitlementDecisionRequestV1 request, CancellationToken cancellationToken = default);
}

public interface IEntitlementAttestationSigner
{
    EntitlementAttestationV1 Sign(EntitlementDecisionSnapshotV1 decision, DateTimeOffset issuedAtUtc, string tokenId);
}

public interface IEntitlementAttestationSigningIdentity
{
    string KeyId { get; }
    RSA Rsa { get; }
    bool IsTestOnly { get; }
}

public sealed record EntitlementAttestationValidationContext(Guid TenantId, string ModuleCode, string RequestHash, DateTimeOffset NowUtc, string KeyId, RSA PublicKey);

public static class EntitlementAttestationValidatorV1
{
    public static bool TryValidate(string token, EntitlementAttestationValidationContext context, out string failure)
    {
        failure = "invalid";
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3 || context.PublicKey.KeySize < 3072) return false;
            var headerBytes = Base64UrlCodec.Decode(parts[0]);
            var payloadBytes = Base64UrlCodec.Decode(parts[1]);
            using var header = JsonDocument.Parse(headerBytes);
            using var payload = JsonDocument.Parse(payloadBytes);
            var h = header.RootElement; var p = payload.RootElement;
            if (h.GetProperty("alg").GetString() != EntitlementAttestationContractV1.Algorithm ||
                h.GetProperty("typ").GetString() != EntitlementAttestationContractV1.Type ||
                h.GetProperty("kid").GetString() != context.KeyId) return false;
            if (p.GetProperty("iss").GetString() != EntitlementAttestationContractV1.Issuer ||
                p.GetProperty("aud").GetString() != EntitlementAttestationContractV1.Audience ||
                p.GetProperty("contract_id").GetString() != EntitlementAttestationContractV1.ContractId ||
                p.GetProperty("contract_version").GetString() != EntitlementAttestationContractV1.ContractVersion) return false;
            if (p.GetProperty("tenant_id").GetString() != context.TenantId.ToString("D") ||
                p.GetProperty("module_code").GetString() != context.ModuleCode ||
                p.GetProperty("request_hash").GetString() != context.RequestHash) return false;
            if (!Enum.TryParse<EntitlementDecisionV1>(p.GetProperty("decision").GetString(), false, out _)) return false;
            if (!p.GetProperty("physical_entitlement_version").TryGetUInt64(out var physical) || physical == 0 ||
                !p.GetProperty("subscription_version").TryGetUInt64(out var subscription) || subscription == 0 ||
                !p.GetProperty("module_applicability_version").TryGetUInt64(out var applicability) || applicability == 0) return false;
            var resolved = ParseExactTimestamp(p.GetProperty("resolved_at_utc").GetString());
            var issued = ParseExactTimestamp(p.GetProperty("iat").GetString());
            var validUntil = ParseExactTimestamp(p.GetProperty("valid_until_utc").GetString());
            if (validUntil - resolved > EntitlementAttestationContractV1.MaximumValidity || validUntil - issued > EntitlementAttestationContractV1.MaximumValidity || context.NowUtc >= validUntil) return false;
            var signature = Base64UrlCodec.Decode(parts[2]);
            if (!context.PublicKey.VerifyData(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)) return false;
            failure = string.Empty; return true;
        }
        catch { return false; }
    }

    private static DateTimeOffset ParseExactTimestamp(string? value)
    {
        if (value is null || value.Length != 24 || value[19] != '.' || value[^1] != 'Z' ||
            !DateTimeOffset.TryParseExact(value, "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed)) throw new FormatException();
        return parsed.ToUniversalTime();
    }
}

public static class EntitlementAttestationContractV1
{
    public const string ContractId = "platform.entitlement-attestation";
    public const string ContractVersion = "1.0";
    public const string Issuer = "diten-platform-service";
    public const string Audience = "diten-auth-service";
    public const string Type = "diten-entitlement-attestation+jwt";
    public const string Algorithm = "RS256";
    public static readonly TimeSpan MaximumValidity = TimeSpan.FromSeconds(15);

    public static string NormalizeModuleCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("ModuleCode must not contain leading or trailing whitespace.", nameof(value));
        var normalized = value.Normalize(NormalizationForm.FormC).ToUpperInvariant();
        if (!string.Equals(value, value.Normalize(NormalizationForm.FormC), StringComparison.Ordinal))
            throw new ArgumentException("ModuleCode must be Unicode NFC.", nameof(value));
        return normalized;
    }

    public static string ValidateRequestHash(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        byte[] bytes;
        try { bytes = Base64UrlCodec.Decode(value); }
        catch (FormatException) { throw new ArgumentException("RequestHash must be canonical base64url SHA-256.", nameof(value)); }
        if (bytes.Length != 32 || Base64UrlCodec.Encode(bytes) != value)
            throw new ArgumentException("RequestHash must be canonical base64url SHA-256.", nameof(value));
        return value;
    }
}

public sealed class EntitlementAttestationSigner : IEntitlementAttestationSigner
{
    private readonly IEntitlementAttestationSigningIdentity identity;
    public EntitlementAttestationSigner(IEntitlementAttestationSigningIdentity identity)
    {
        this.identity = identity ?? throw new ArgumentNullException(nameof(identity));
        if (identity.IsTestOnly) throw new InvalidOperationException("A test-only signing identity cannot enter production composition.");
        if (identity.Rsa.KeySize < 3072) throw new InvalidOperationException("Entitlement attestation RSA keys must be at least 3072 bits.");
        if (string.IsNullOrWhiteSpace(identity.KeyId)) throw new InvalidOperationException("An exact signing kid is required.");
    }

    public EntitlementAttestationV1 Sign(EntitlementDecisionSnapshotV1 decision, DateTimeOffset issuedAtUtc, string tokenId)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (decision.TenantId == Guid.Empty) throw new InvalidOperationException("A non-empty TenantId binding is required.");
        var normalizedModule = EntitlementAttestationContractV1.NormalizeModuleCode(decision.ModuleCode);
        if (!string.Equals(normalizedModule, decision.ModuleCode, StringComparison.Ordinal)) throw new InvalidOperationException("The signed ModuleCode must already be normalized.");
        EntitlementAttestationContractV1.ValidateRequestHash(decision.RequestHash);
        if (!decision.Version.IsComplete) throw new InvalidOperationException("A complete positive version vector is required.");
        if (string.IsNullOrWhiteSpace(tokenId)) throw new ArgumentException("Token id is required.", nameof(tokenId));
        var iat = issuedAtUtc.ToUniversalTime();
        var validUntil = iat.Add(EntitlementAttestationContractV1.MaximumValidity);
        var header = CanonicalHeader(identity.KeyId);
        var payload = CanonicalPayload(decision, iat, validUntil, tokenId);
        var signingInput = $"{Base64UrlCodec.Encode(header)}.{Base64UrlCodec.Encode(payload)}";
        var signature = identity.Rsa.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return new(decision, validUntil, identity.KeyId, $"{signingInput}.{Base64UrlCodec.Encode(signature)}");
    }

    public static byte[] CanonicalHeader(string kid) => Encoding.UTF8.GetBytes(
        $"{{\"alg\":\"RS256\",\"kid\":{JsonSerializer.Serialize(kid)},\"typ\":\"{EntitlementAttestationContractV1.Type}\"}}");

    public static byte[] CanonicalPayload(EntitlementDecisionSnapshotV1 value, DateTimeOffset iat, DateTimeOffset validUntil, string jti)
    {
        static string Q(string value) => JsonSerializer.Serialize(value);
        static string T(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);
        var v = value.Version;
        var json = "{" +
            $"\"aud\":\"{EntitlementAttestationContractV1.Audience}\"," +
            $"\"contract_id\":\"{EntitlementAttestationContractV1.ContractId}\"," +
            $"\"contract_version\":\"{EntitlementAttestationContractV1.ContractVersion}\"," +
            $"\"decision\":{Q(value.Decision.ToString())}," +
            $"\"iat\":{Q(T(iat))}," +
            $"\"iss\":\"{EntitlementAttestationContractV1.Issuer}\"," +
            $"\"jti\":{Q(jti)}," +
            $"\"module_applicability_version\":{v.ModuleApplicabilityVersion}," +
            $"\"module_code\":{Q(value.ModuleCode)}," +
            $"\"physical_entitlement_version\":{v.PhysicalEntitlementVersion}," +
            $"\"request_hash\":{Q(value.RequestHash)}," +
            $"\"resolved_at_utc\":{Q(T(value.ResolvedAtUtc))}," +
            $"\"subscription_version\":{v.SubscriptionVersion}," +
            $"\"tenant_id\":{Q(value.TenantId.ToString("D"))}," +
            $"\"valid_until_utc\":{Q(T(validUntil))}" + "}";
        return Encoding.UTF8.GetBytes(json);
    }
}

internal static class Base64UrlCodec
{
    public static string Encode(ReadOnlySpan<byte> value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public static byte[] Decode(string value)
    {
        if (value.Contains('=') || value.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))) throw new FormatException();
        var text = value.Replace('-', '+').Replace('_', '/');
        text += (text.Length % 4) switch { 0 => "", 2 => "==", 3 => "=", _ => throw new FormatException() };
        return Convert.FromBase64String(text);
    }
}
