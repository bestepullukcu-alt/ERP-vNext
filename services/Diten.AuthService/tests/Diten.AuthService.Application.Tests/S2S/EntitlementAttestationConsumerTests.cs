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
    private static readonly string Hash = B64(SHA256.HashData(Encoding.UTF8.GetBytes("fixture")));
    private readonly RSA _rsa = RSA.Create(3072);

    [Fact]
    public async Task Exact_producer_fixture_is_compatible_and_runs_remaining_local_gate()
    {
        var local = new Local(Fu16LocalAuthorizationResultKind.Accepted);
        var result = await Consumer(Token(), local: local).EnforceAsync(Request(), Snapshot(), default);
        Assert.Equal(EntitlementAttestationOutcomeKind.Continue, result.Kind);
        Assert.Equal(new EntitlementStateVersionV1(3, 5, 7), result.Version);
        Assert.Equal(1, local.Calls);
        Assert.Equal("607a718d4cc46e5512632a1452d433f4f06799c433082fc39a62fd5e266ea4ed", Convert.ToHexString(SHA256.HashData(Payload())).ToLowerInvariant());
    }

    [Theory]
    [InlineData(EntitlementAttestationDecisionV1.Disabled)]
    [InlineData(EntitlementAttestationDecisionV1.Expired)]
    [InlineData(EntitlementAttestationDecisionV1.Missing)]
    [InlineData(EntitlementAttestationDecisionV1.NotApplicable)]
    public async Task Four_authoritative_denials_are_typed_403(EntitlementAttestationDecisionV1 decision)
    {
        var local = new Local(Fu16LocalAuthorizationResultKind.Accepted);
        var result = await Consumer(Token(decision), local: local).EnforceAsync(Request(), Snapshot(), default);
        Assert.Equal(EntitlementAttestationOutcomeKind.Forbidden, result.Kind); Assert.Equal(0, local.Calls);
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
        var result = await Consumer(ReplaceAndSign(Token(), claim, replacement, 1)).EnforceAsync(Request(), Snapshot(), default);
        Assert.Equal(EntitlementAttestationOutcomeKind.Unauthorized, result.Kind);
    }

    [Theory]
    [InlineData("typ", "diten-delegated-actor-proof+jwt")]
    [InlineData("alg", "HS256")]
    [InlineData("kid", "wrong")]
    public async Task Token_family_header_and_key_are_isolated(string claim, string replacement)
    {
        var result = await Consumer(ReplaceAndSign(Token(), claim, replacement, 0)).EnforceAsync(Request(), Snapshot(), default);
        Assert.Equal(EntitlementAttestationOutcomeKind.Unauthorized, result.Kind);
    }

    [Fact]
    public async Task Bad_signature_rsa2048_and_test_identity_are_401()
    {
        var bad = Token()[..^1] + (Token()[^1] == 'A' ? "B" : "A");
        Assert.Equal(EntitlementAttestationOutcomeKind.Unauthorized, (await Consumer(bad).EnforceAsync(Request(), Snapshot(), default)).Kind);
        using var small = RSA.Create(2048);
        Assert.Equal(EntitlementAttestationOutcomeKind.Unauthorized, (await Consumer(Token(signer: small), rsa: small).EnforceAsync(Request(), Snapshot(), default)).Kind);
        Assert.Equal(EntitlementAttestationOutcomeKind.Unauthorized, (await Consumer(Token(), testOnly: true).EnforceAsync(Request(), Snapshot(), default)).Kind);
    }

    [Fact]
    public async Task Hard_boundary_future_and_expired_are_503_without_skew_extension()
    {
        Assert.Equal(EntitlementAttestationOutcomeKind.Continue, (await Consumer(Token(), now: Issued.AddSeconds(14.999)).EnforceAsync(Request(), Snapshot(), default)).Kind);
        Assert.Equal(EntitlementAttestationOutcomeKind.ServiceUnavailable, (await Consumer(Token(), now: Issued.AddSeconds(15)).EnforceAsync(Request(), Snapshot(), default)).Kind);
        Assert.Equal(EntitlementAttestationOutcomeKind.ServiceUnavailable, (await Consumer(Token(), now: Issued.AddMilliseconds(-1)).EnforceAsync(Request(), Snapshot(), default)).Kind);
    }

    [Theory]
    [InlineData(EntitlementAttestationProviderFailureV1.Disabled)]
    [InlineData(EntitlementAttestationProviderFailureV1.Unavailable)]
    [InlineData(EntitlementAttestationProviderFailureV1.Timeout)]
    [InlineData(EntitlementAttestationProviderFailureV1.Malformed)]
    [InlineData(EntitlementAttestationProviderFailureV1.Indeterminate)]
    public async Task Provider_failures_are_503_and_never_run_local_authorization(EntitlementAttestationProviderFailureV1 failure)
    {
        var local = new Local(Fu16LocalAuthorizationResultKind.Accepted);
        var result = await Consumer(null, providerFailure: failure, local: local).EnforceAsync(Request(), Snapshot(), default);
        Assert.Equal(EntitlementAttestationOutcomeKind.ServiceUnavailable, result.Kind); Assert.Equal(0, local.Calls);
    }

    [Theory]
    [InlineData(EntitlementVersionFenceResult.Older)]
    [InlineData(EntitlementVersionFenceResult.Incomparable)]
    [InlineData(EntitlementVersionFenceResult.AuthorityUnavailable)]
    public async Task Version_rollback_incomparability_or_uncertainty_is_503(EntitlementVersionFenceResult fence)
    {
        Assert.Equal(EntitlementAttestationOutcomeKind.ServiceUnavailable, (await Consumer(Token(), fence: fence).EnforceAsync(Request(), Snapshot(), default)).Kind);
    }

    [Theory]
    [InlineData(Fu16LocalAuthorizationResultKind.StaleOrConcurrent, EntitlementAttestationOutcomeKind.Conflict)]
    [InlineData(Fu16LocalAuthorizationResultKind.Unauthorized, EntitlementAttestationOutcomeKind.Unauthorized)]
    [InlineData(Fu16LocalAuthorizationResultKind.Forbidden, EntitlementAttestationOutcomeKind.Forbidden)]
    [InlineData(Fu16LocalAuthorizationResultKind.AuthorityUnavailable, EntitlementAttestationOutcomeKind.ServiceUnavailable)]
    public async Task Local_principal_credential_membership_grant_version_freshness_is_typed(Fu16LocalAuthorizationResultKind localKind, EntitlementAttestationOutcomeKind expected)
    {
        Assert.Equal(expected, (await Consumer(Token(), local: new(localKind)).EnforceAsync(Request(), Snapshot(), default)).Kind);
    }

    [Fact]
    public async Task Cancellation_propagates_and_default_is_disabled()
    {
        Assert.False(new EntitlementAttestationConsumerOptions().Enabled);
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Consumer(Token()).EnforceAsync(Request(), Snapshot(), cts.Token));
    }

    [Fact]
    public async Task Exact_case_and_cross_tenant_bindings_do_not_disclose()
    {
        Assert.Equal(EntitlementAttestationOutcomeKind.Unauthorized, (await Consumer(Token()).EnforceAsync(Request() with { ModuleCode = "ppm" }, Snapshot(), default)).Kind);
        Assert.Equal(EntitlementAttestationOutcomeKind.Unauthorized, (await Consumer(Token()).EnforceAsync(Request() with { TenantId = Guid.NewGuid() }, Snapshot(), default)).Kind);
    }

    private EntitlementAttestationConsumer Consumer(string? token, RSA? rsa = null, bool testOnly = false,
        EntitlementAttestationProviderFailureV1? providerFailure = null, Local? local = null,
        EntitlementVersionFenceResult fence = EntitlementVersionFenceResult.Accepted, DateTimeOffset? now = null) => new(
        new Provider(token, providerFailure), new Keys(new(EntitlementAttestationContractV1.Issuer, "key-2026-01", rsa ?? _rsa, true, testOnly)),
        local ?? new(Fu16LocalAuthorizationResultKind.Accepted), new Fence(fence), new() { Enabled = true }, new Clock(now ?? Issued.AddSeconds(1)));

    private static EntitlementAttestationRequestV1 Request() => new(Tenant, "PPM", Hash);
    private static Fu16LocalAuthorizationSnapshot Snapshot() => new(Guid.NewGuid(), 4, 3, Guid.NewGuid(), 8, Guid.NewGuid(), 9, 10, "jti", "nonce");
    private string Token(EntitlementAttestationDecisionV1 decision = EntitlementAttestationDecisionV1.Active, RSA? signer = null)
    { var h=Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"kid\":\"key-2026-01\",\"typ\":\"diten-entitlement-attestation+jwt\"}"); var p=Payload(decision); var input=$"{B64(h)}.{B64(p)}"; return $"{input}.{B64((signer ?? _rsa).SignData(Encoding.ASCII.GetBytes(input),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1))}"; }
    private static byte[] Payload(EntitlementAttestationDecisionV1 decision = EntitlementAttestationDecisionV1.Active) => Encoding.UTF8.GetBytes(
        $"{{\"aud\":\"diten-auth-service\",\"contract_id\":\"platform.entitlement-attestation\",\"contract_version\":\"1.0\",\"decision\":\"{decision}\",\"iat\":\"2026-08-25T10:11:12.345Z\",\"iss\":\"diten-platform-service\",\"jti\":\"fixture-jti\",\"module_applicability_version\":7,\"module_code\":\"PPM\",\"physical_entitlement_version\":3,\"request_hash\":\"{Hash}\",\"resolved_at_utc\":\"2026-08-25T10:11:12.345Z\",\"subscription_version\":5,\"tenant_id\":\"11111111-2222-3333-4444-555555555555\",\"valid_until_utc\":\"2026-08-25T10:11:27.345Z\"}}");
    private string ReplaceAndSign(string token, string claim, string replacement, int part) { var pieces=token.Split('.'); using var doc=JsonDocument.Parse(Decode(pieces[part])); var text=Encoding.UTF8.GetString(Decode(pieces[part])); var old=doc.RootElement.GetProperty(claim).GetString()!; pieces[part]=B64(Encoding.UTF8.GetBytes(text.Replace($"\"{old}\"",$"\"{replacement}\"",StringComparison.Ordinal))); var input=$"{pieces[0]}.{pieces[1]}"; pieces[2]=B64(_rsa.SignData(Encoding.ASCII.GetBytes(input),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1)); return string.Join('.',pieces); }
    private static string B64(ReadOnlySpan<byte> b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+','-').Replace('/','_');
    private static byte[] Decode(string s) { s=s.Replace('-','+').Replace('_','/'); s+=new string('=',(4-s.Length%4)%4); return Convert.FromBase64String(s); }
    public void Dispose() => _rsa.Dispose();
    private sealed class Provider(string? token, EntitlementAttestationProviderFailureV1? failure) : IPlatformEntitlementAttestationProvider { public Task<EntitlementAttestationProviderResultV1> GetAsync(EntitlementAttestationRequestV1 r,CancellationToken c) => Task.FromResult(failure is null ? new EntitlementAttestationProviderResultV1.Attested(token!) as EntitlementAttestationProviderResultV1 : new EntitlementAttestationProviderResultV1.Failed(failure.Value)); }
    private sealed class Keys(EntitlementAttestationTrustedKey key) : IEntitlementAttestationTrustedKeyProvider { public Task<EntitlementAttestationKeyResolution> ResolveAsync(string i,string k,CancellationToken c) => Task.FromResult(new EntitlementAttestationKeyResolution(k==key.Kid?EntitlementAttestationKeyResolutionKind.Resolved:EntitlementAttestationKeyResolutionKind.Unknown,k==key.Kid?key:null)); }
    private sealed class Fence(EntitlementVersionFenceResult result) : IEntitlementStateVersionFence { public Task<EntitlementVersionFenceResult> ObserveAsync(Guid t,string m,EntitlementStateVersionV1 v,CancellationToken c)=>Task.FromResult(result); }
    private sealed class Local(Fu16LocalAuthorizationResultKind result) : IFu16AuthorizationTransactionSession { public int Calls {get;private set;} public Task<Fu16LocalAuthorizationResult> ValidateAndConsumeAsync(Fu16LocalAuthorizationSnapshot s,CancellationToken c){Calls++;return Task.FromResult(new Fu16LocalAuthorizationResult(result));} }
    private sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow()=>now; }
}
