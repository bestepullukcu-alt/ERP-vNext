using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.AuthService.Application.S2S;
using Diten.AuthService.Infrastructure.S2S;

namespace Diten.AuthService.Application.Tests.S2S;

public sealed class EntitlementAttestationConsumerTests : IDisposable
{
    private static readonly Guid Tenant = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly DateTimeOffset Issued = new(2026, 8, 25, 10, 11, 12, 345, TimeSpan.Zero);
    private static readonly string RequestHash = B64(SHA256.HashData(Encoding.UTF8.GetBytes("fixture")));
    private const string KeyId = "key-2026-01";
    private readonly RSA _rsa = RSA.Create(3072);

    [Fact]
    public async Task Canonical_producer_fixture_is_compatible_and_runs_remaining_local_gate()
    {
        var local = new Local(S2SLocalAuthorizationResultKind.Accepted);
        var result = await Consumer(Token(), local: local).EnforceAsync(Request(), Snapshot(), default);

        Assert.Equal(EntitlementAttestationOutcomeKind.Continue, result.Kind);
        Assert.Equal(new EntitlementStateVersionV1(3, 5, 7), result.Version);
        Assert.Equal(1, local.Calls);
        Assert.Equal(
            "607a718d4cc46e5512632a1452d433f4f06799c433082fc39a62fd5e266ea4ed",
            Convert.ToHexString(SHA256.HashData(CanonicalPayload())).ToLowerInvariant());
    }

    [Theory]
    [InlineData(EntitlementAttestationDecisionV1.Disabled)]
    [InlineData(EntitlementAttestationDecisionV1.Expired)]
    [InlineData(EntitlementAttestationDecisionV1.Missing)]
    [InlineData(EntitlementAttestationDecisionV1.NotApplicable)]
    public async Task Four_authoritative_denials_are_typed_403(EntitlementAttestationDecisionV1 decision)
    {
        var local = new Local(S2SLocalAuthorizationResultKind.Accepted);
        var result = await Consumer(Token(decision), local: local).EnforceAsync(Request(), Snapshot(), default);

        Assert.Equal(EntitlementAttestationOutcomeKind.Forbidden, result.Kind);
        Assert.Equal(0, local.Calls);
    }

    [Theory]
    [InlineData("iss", "wrong")]
    [InlineData("aud", "wrong")]
    [InlineData("contract_id", "diten-delegated-actor-proof")]
    [InlineData("contract_version", "2.0")]
    [InlineData("tenant_id", "22222222-2222-2222-2222-222222222222")]
    [InlineData("module_code", "MDM")]
    [InlineData("request_hash", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Signed_wrong_identity_or_binding_is_401(string claim, string replacement)
    {
        var result = await Consumer(ReplaceAndSign(Token(), claim, replacement, 1))
            .EnforceAsync(Request(), Snapshot(), default);

        Assert.Equal(EntitlementAttestationOutcomeKind.Unauthorized, result.Kind);
    }

    [Theory]
    [InlineData("typ", "diten-delegated-actor-proof+jwt")]
    [InlineData("alg", "HS256")]
    [InlineData("kid", "wrong")]
    public async Task Token_family_header_and_key_are_isolated(string claim, string replacement)
    {
        var result = await Consumer(ReplaceAndSign(Token(), claim, replacement, 0))
            .EnforceAsync(Request(), Snapshot(), default);

        Assert.Equal(EntitlementAttestationOutcomeKind.Unauthorized, result.Kind);
    }

    [Fact]
    public async Task Bad_signature_and_rsa2048_are_401()
    {
        var bad = Token()[..^1] + (Token()[^1] == 'A' ? "B" : "A");
        Assert.Equal(
            EntitlementAttestationOutcomeKind.Unauthorized,
            (await Consumer(bad).EnforceAsync(Request(), Snapshot(), default)).Kind);

        using var small = RSA.Create(2048);
        Assert.Equal(
            EntitlementAttestationOutcomeKind.Unauthorized,
            (await Consumer(Resign(Token(), small), rsa: small).EnforceAsync(Request(), Snapshot(), default)).Kind);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Test_only_identity_is_rejected_in_production_active_and_previous_slots(bool active)
    {
        const string testKeyId = "test-key-2026-01";
        var key = new EntitlementAttestationTrustedKey(
            EntitlementAttestationContractV1.Issuer,
            testKeyId,
            _rsa,
            active,
            true);

        var result = await Consumer(Token(keyId: testKeyId), keys: new Keys(key))
            .EnforceAsync(Request(), Snapshot(), default);

        Assert.Equal(EntitlementAttestationOutcomeKind.Unauthorized, result.Kind);
    }

    [Fact]
    public async Task Hard_boundary_future_and_expired_are_503_without_skew_extension()
    {
        Assert.Equal(
            EntitlementAttestationOutcomeKind.Continue,
            (await Consumer(Token(), now: Issued.AddSeconds(14.999)).EnforceAsync(Request(), Snapshot(), default)).Kind);
        Assert.Equal(
            EntitlementAttestationOutcomeKind.ServiceUnavailable,
            (await Consumer(Token(), now: Issued.AddSeconds(15)).EnforceAsync(Request(), Snapshot(), default)).Kind);
        Assert.Equal(
            EntitlementAttestationOutcomeKind.ServiceUnavailable,
            (await Consumer(Token(), now: Issued.AddMilliseconds(-1)).EnforceAsync(Request(), Snapshot(), default)).Kind);
    }

    [Theory]
    [InlineData(EntitlementAttestationProviderFailureV1.Disabled)]
    [InlineData(EntitlementAttestationProviderFailureV1.Unavailable)]
    [InlineData(EntitlementAttestationProviderFailureV1.Timeout)]
    [InlineData(EntitlementAttestationProviderFailureV1.Malformed)]
    [InlineData(EntitlementAttestationProviderFailureV1.Indeterminate)]
    public async Task Provider_failures_are_503_and_never_run_local_authorization(
        EntitlementAttestationProviderFailureV1 failure)
    {
        var local = new Local(S2SLocalAuthorizationResultKind.Accepted);
        var result = await Consumer(null, providerFailure: failure, local: local)
            .EnforceAsync(Request(), Snapshot(), default);

        Assert.Equal(EntitlementAttestationOutcomeKind.ServiceUnavailable, result.Kind);
        Assert.Equal(0, local.Calls);
    }

    [Fact]
    public async Task Older_platform_version_is_exact_409()
    {
        var result = await Consumer(Token(), fence: EntitlementVersionFenceResult.Older)
            .EnforceAsync(Request(), Snapshot(), default);

        Assert.Equal(EntitlementAttestationOutcomeKind.Conflict, result.Kind);
        Assert.Equal("entitlement_attestation_version_older", result.Code);
    }

    [Theory]
    [InlineData(EntitlementVersionFenceResult.Incomparable, "entitlement_attestation_version_incomparable")]
    [InlineData(EntitlementVersionFenceResult.AuthorityUnavailable, "entitlement_attestation_version_authority_unavailable")]
    public async Task Version_incomparability_or_uncertainty_is_exact_503(
        EntitlementVersionFenceResult fence,
        string expectedCode)
    {
        var result = await Consumer(Token(), fence: fence).EnforceAsync(Request(), Snapshot(), default);

        Assert.Equal(EntitlementAttestationOutcomeKind.ServiceUnavailable, result.Kind);
        Assert.Equal(expectedCode, result.Code);
    }

    [Theory]
    [InlineData(S2SLocalAuthorizationResultKind.StaleOrConcurrent, EntitlementAttestationOutcomeKind.Conflict)]
    [InlineData(S2SLocalAuthorizationResultKind.Unauthorized, EntitlementAttestationOutcomeKind.Unauthorized)]
    [InlineData(S2SLocalAuthorizationResultKind.Forbidden, EntitlementAttestationOutcomeKind.Forbidden)]
    [InlineData(S2SLocalAuthorizationResultKind.AuthorityUnavailable, EntitlementAttestationOutcomeKind.ServiceUnavailable)]
    public async Task Local_principal_credential_membership_grant_version_freshness_is_typed(
        S2SLocalAuthorizationResultKind localKind,
        EntitlementAttestationOutcomeKind expected)
    {
        Assert.Equal(
            expected,
            (await Consumer(Token(), local: new Local(localKind)).EnforceAsync(Request(), Snapshot(), default)).Kind);
    }

    [Fact]
    public async Task Cancellation_propagates_and_default_is_disabled()
    {
        Assert.False(new EntitlementAttestationConsumerOptions().Enabled);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Consumer(Token()).EnforceAsync(Request(), Snapshot(), cancellation.Token));
    }

    [Fact]
    public async Task Exact_case_and_cross_tenant_bindings_do_not_disclose()
    {
        Assert.Equal(
            EntitlementAttestationOutcomeKind.Unauthorized,
            (await Consumer(Token()).EnforceAsync(Request() with { ModuleCode = "ppm" }, Snapshot(), default)).Kind);
        Assert.Equal(
            EntitlementAttestationOutcomeKind.Unauthorized,
            (await Consumer(Token()).EnforceAsync(Request() with { TenantId = Guid.NewGuid() }, Snapshot(), default)).Kind);
    }

    [Fact]
    public async Task Default_disabled_consumer_returns_503_without_provider_or_local_reads()
    {
        var provider = new Provider(Token(), null);
        var local = new Local(S2SLocalAuthorizationResultKind.Accepted);
        var consumer = new EntitlementAttestationConsumer(
            provider,
            new Keys(new(EntitlementAttestationContractV1.Issuer, KeyId, _rsa, true, false)),
            local,
            new Fence(EntitlementVersionFenceResult.Accepted),
            new EntitlementAttestationConsumerOptions(),
            new Clock(Issued.AddSeconds(1)));

        var result = await consumer.EnforceAsync(Request(), Snapshot(), default);

        Assert.Equal(EntitlementAttestationOutcomeKind.ServiceUnavailable, result.Kind);
        Assert.Equal("entitlement_attestation_consumer_disabled", result.Code);
        Assert.Equal(0, provider.Calls);
        Assert.Equal(0, local.Calls);
    }

    private EntitlementAttestationConsumer Consumer(
        string? token,
        RSA? rsa = null,
        EntitlementAttestationProviderFailureV1? providerFailure = null,
        Local? local = null,
        EntitlementVersionFenceResult fence = EntitlementVersionFenceResult.Accepted,
        DateTimeOffset? now = null,
        IEntitlementAttestationTrustedKeyProvider? keys = null) =>
        new(
            new Provider(token, providerFailure),
            keys ?? new Keys(new(EntitlementAttestationContractV1.Issuer, KeyId, rsa ?? _rsa, true, false)),
            local ?? new Local(S2SLocalAuthorizationResultKind.Accepted),
            new Fence(fence),
            new EntitlementAttestationConsumerOptions { Enabled = true },
            new Clock(now ?? Issued.AddSeconds(1)));

    private static EntitlementAttestationRequestV1 Request() => new(Tenant, "PPM", RequestHash);

    private static S2SLocalAuthorizationSnapshot Snapshot() =>
        new(Guid.NewGuid(), 4, 3, Guid.NewGuid(), 8, Guid.NewGuid(), 9, 10, "jti", "nonce");

    private string Token(
        EntitlementAttestationDecisionV1 decision = EntitlementAttestationDecisionV1.Active,
        RSA? signer = null,
        string keyId = KeyId)
    {
        var header = Encoding.UTF8.GetBytes(
            $"{{\"alg\":\"RS256\",\"kid\":\"{keyId}\",\"typ\":\"diten-entitlement-attestation+jwt\"}}");
        var payload = CanonicalPayload(decision);
        var signingInput = $"{B64(header)}.{B64(payload)}";
        var signature = (signer ?? _rsa).SignData(
            Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return $"{signingInput}.{B64(signature)}";
    }

    private static byte[] CanonicalPayload(
        EntitlementAttestationDecisionV1 decision = EntitlementAttestationDecisionV1.Active) =>
        Encoding.UTF8.GetBytes(
            $"{{\"aud\":\"diten-auth-service\",\"contract_id\":\"platform.entitlement-attestation\",\"contract_version\":\"1.0\",\"decision\":\"{decision}\",\"iat\":\"2026-08-25T10:11:12.345Z\",\"iss\":\"diten-platform-service\",\"jti\":\"fixture-jti\",\"module_applicability_version\":7,\"module_code\":\"PPM\",\"physical_entitlement_version\":3,\"request_hash\":\"{RequestHash}\",\"resolved_at_utc\":\"2026-08-25T10:11:12.345Z\",\"subscription_version\":5,\"tenant_id\":\"11111111-2222-3333-4444-555555555555\",\"valid_until_utc\":\"2026-08-25T10:11:27.345Z\"}}");

    private string ReplaceAndSign(string token, string claim, string replacement, int part)
    {
        var pieces = token.Split('.');
        using var document = JsonDocument.Parse(Decode(pieces[part]));
        var text = Encoding.UTF8.GetString(Decode(pieces[part]));
        var original = document.RootElement.GetProperty(claim).GetString()!;
        pieces[part] = B64(Encoding.UTF8.GetBytes(
            text.Replace($"\"{original}\"", $"\"{replacement}\"", StringComparison.Ordinal)));
        var input = $"{pieces[0]}.{pieces[1]}";
        pieces[2] = B64(_rsa.SignData(
            Encoding.ASCII.GetBytes(input),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1));
        return string.Join('.', pieces);
    }

    private static string Resign(string token, RSA signer)
    {
        var pieces = token.Split('.');
        var input = $"{pieces[0]}.{pieces[1]}";
        pieces[2] = B64(signer.SignData(
            Encoding.ASCII.GetBytes(input),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1));
        return string.Join('.', pieces);
    }

    private static string B64(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Decode(string value)
    {
        value = value.Replace('-', '+').Replace('_', '/');
        value += new string('=', (4 - value.Length % 4) % 4);
        return Convert.FromBase64String(value);
    }

    public void Dispose() => _rsa.Dispose();

    private sealed class Provider(string? token, EntitlementAttestationProviderFailureV1? failure)
        : IPlatformEntitlementAttestationProvider
    {
        public int Calls { get; private set; }

        public Task<EntitlementAttestationProviderResultV1> GetAsync(
            EntitlementAttestationRequestV1 request,
            CancellationToken cancellationToken)
        {
            Calls++;
            EntitlementAttestationProviderResultV1 result = failure is null
                ? new EntitlementAttestationProviderResultV1.Attested(token!)
                : new EntitlementAttestationProviderResultV1.Failed(failure.Value);
            return Task.FromResult(result);
        }
    }

    private sealed class Keys(EntitlementAttestationTrustedKey key)
        : IEntitlementAttestationTrustedKeyProvider
    {
        public Task<EntitlementAttestationKeyResolution> ResolveAsync(
            string issuer,
            string kid,
            CancellationToken cancellationToken) =>
            Task.FromResult(new EntitlementAttestationKeyResolution(
                kid == key.Kid
                    ? EntitlementAttestationKeyResolutionKind.Resolved
                    : EntitlementAttestationKeyResolutionKind.Unknown,
                kid == key.Kid ? key : null));
    }

    private sealed class Fence(EntitlementVersionFenceResult result) : IEntitlementStateVersionFence
    {
        public Task<EntitlementVersionFenceResult> ObserveAsync(
            Guid tenantId,
            string moduleCode,
            EntitlementStateVersionV1 version,
            CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class Local(S2SLocalAuthorizationResultKind result) : IS2SAuthorizationTransactionSession
    {
        public int Calls { get; private set; }

        public Task<S2SLocalAuthorizationResult> ValidateAndConsumeAsync(
            S2SLocalAuthorizationSnapshot snapshot,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new S2SLocalAuthorizationResult(result));
        }
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
