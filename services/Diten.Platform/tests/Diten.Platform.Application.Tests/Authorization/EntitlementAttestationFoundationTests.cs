using System.Security.Cryptography;
using System.Text;
using Diten.Platform.Application.Features.EntitlementAttestations;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Authorization;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class EntitlementAttestationFoundationTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly string RequestHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("fixture"))).TrimEnd('=').Replace('+','-').Replace('/','_');

    [Fact]
    public async Task Disabled_provider_is_typed_503_without_touching_authority()
    {
        var source = new FakeSource(new(1, 1, 1), EntitlementDecisionV1.Active);
        var provider = CreateProvider(source, false);
        var result = Assert.IsType<EntitlementDecisionResultV1.ServiceUnavailable>(await provider.DecideAsync(new(Tenant, "PPM", RequestHash)));
        Assert.Equal(EntitlementDecisionFailureV1.ProviderDisabled, result.Failure);
        Assert.Equal(0, source.Reads);
    }

    [Fact]
    public void Provider_options_are_default_disabled() => Assert.False(new PlatformEntitlementAttestationOptions().Enabled);

    [Theory]
    [InlineData(EntitlementDecisionV1.Active)]
    [InlineData(EntitlementDecisionV1.Disabled)]
    [InlineData(EntitlementDecisionV1.Expired)]
    [InlineData(EntitlementDecisionV1.Missing)]
    [InlineData(EntitlementDecisionV1.NotApplicable)]
    public async Task Returns_closed_authoritative_result_matrix(EntitlementDecisionV1 expected)
    {
        var result = Assert.IsType<EntitlementDecisionResultV1.Authoritative>(await CreateProvider(new FakeSource(new(3, 5, 7), expected), true).DecideAsync(new(Tenant, "ppm", RequestHash)));
        Assert.Equal(expected, result.Snapshot.Decision);
        Assert.Equal("PPM", result.Snapshot.ModuleCode);
        Assert.Equal(new EntitlementStateVersionV1(3, 5, 7), result.Snapshot.Version);
    }

    [Fact]
    public async Task Mutation_between_evaluation_and_revalidation_is_typed_indeterminate_and_never_cached()
    {
        var source = new MutatingSource(); var provider = CreateProvider(source, true);
        var first = Assert.IsType<EntitlementDecisionResultV1.ServiceUnavailable>(await provider.DecideAsync(new(Tenant, "PPM", RequestHash)));
        Assert.Equal(EntitlementDecisionFailureV1.Indeterminate, first.Failure);
        Assert.Equal(1, source.DecisionReads);
    }

    [Fact]
    public async Task Changed_fence_after_decision_is_rejected_even_when_decision_reports_new_vector()
    {
        var result = Assert.IsType<EntitlementDecisionResultV1.ServiceUnavailable>(await CreateProvider(new FenceChangedSource(), true).DecideAsync(new(Tenant,"PPM",RequestHash)));
        Assert.Equal(EntitlementDecisionFailureV1.Indeterminate, result.Failure);
    }

    [Fact]
    public async Task Stale_decision_vector_is_rejected_against_stable_current_fence()
    {
        var result = Assert.IsType<EntitlementDecisionResultV1.ServiceUnavailable>(await CreateProvider(new DecisionMismatchSource(), true).DecideAsync(new(Tenant,"PPM",RequestHash)));
        Assert.Equal(EntitlementDecisionFailureV1.Indeterminate, result.Failure);
    }

    [Theory]
    [InlineData(TenantModuleEffectiveAccess.Active, EntitlementDecisionV1.Active)]
    [InlineData(TenantModuleEffectiveAccess.EnabledByOverride, EntitlementDecisionV1.Active)]
    [InlineData(TenantModuleEffectiveAccess.SystemLocked, EntitlementDecisionV1.Active)]
    [InlineData(TenantModuleEffectiveAccess.BlockedByOverride, EntitlementDecisionV1.Disabled)]
    [InlineData(TenantModuleEffectiveAccess.Expired, EntitlementDecisionV1.Expired)]
    [InlineData(TenantModuleEffectiveAccess.NoAccess, EntitlementDecisionV1.Missing)]
    public void Effective_access_mapping_is_closed(TenantModuleEffectiveAccess source, EntitlementDecisionV1 expected) =>
        Assert.Equal(expected, MongoAuthoritativeEntitlementDecisionSource.MapEffectiveAccess(source));

    [Theory]
    [InlineData(SourceFailure.Unavailable, EntitlementDecisionFailureV1.ProviderUnavailable)]
    [InlineData(SourceFailure.Malformed, EntitlementDecisionFailureV1.MalformedAuthority)]
    [InlineData(SourceFailure.Incomplete, EntitlementDecisionFailureV1.Indeterminate)]
    public async Task Dependency_failures_remain_distinct_typed_503(SourceFailure sourceFailure, EntitlementDecisionFailureV1 expected)
    {
        var result = Assert.IsType<EntitlementDecisionResultV1.ServiceUnavailable>(await CreateProvider(new FailingSource(sourceFailure), true).DecideAsync(new(Tenant,"PPM",RequestHash)));
        Assert.Equal(expected, result.Failure);
    }

    [Fact]
    public async Task Dependency_timeout_is_distinct_typed_503()
    {
        var provider = new PlatformEntitlementDecisionProvider(new FailingSource(SourceFailure.Timeout), new(), Options.Create(new PlatformEntitlementAttestationOptions { Enabled = true, AuthorityTimeout = TimeSpan.FromMilliseconds(20) }));
        var result = Assert.IsType<EntitlementDecisionResultV1.ServiceUnavailable>(await provider.DecideAsync(new(Tenant,"PPM",RequestHash)));
        Assert.Equal(EntitlementDecisionFailureV1.Timeout, result.Failure);
    }

    [Fact]
    public void Cache_requires_exact_complete_vector_and_rejects_stale_or_incomparable_invalidation()
    {
        var cache = new VersionAwareEntitlementDecisionCache(); var now = DateTimeOffset.UtcNow;
        var v = new EntitlementStateVersionV1(2, 2, 2); var snapshot = new EntitlementDecisionSnapshotV1(Tenant, "PPM", RequestHash, EntitlementDecisionV1.Active, v, now);
        Assert.True(cache.TryWrite(snapshot, v));
        Assert.False(cache.TryWrite(snapshot, new(3,2,2)));
        Assert.True(cache.TryGet(Tenant, "PPM", RequestHash, v, out _));
        Assert.False(cache.TryGet(Tenant, "PPM", RequestHash, new(3,2,2), out _));
        Assert.False(cache.Invalidate(Tenant, "PPM", new(1,3,2)));
        Assert.True(cache.Invalidate(Tenant, "PPM", new(3,3,3)));
        Assert.False(cache.TryGet(Tenant, "PPM", RequestHash, v, out _));
    }

    [Fact]
    public void Canonical_payload_has_normative_bytes_and_hash()
    {
        var decision = new EntitlementDecisionSnapshotV1(Tenant, "PPM", RequestHash, EntitlementDecisionV1.Active, new(3,5,7), new DateTimeOffset(2026,8,25,10,11,12,345,TimeSpan.Zero));
        var bytes = EntitlementAttestationSigner.CanonicalPayload(decision, decision.ResolvedAtUtc, decision.ResolvedAtUtc.AddSeconds(15), "fixture-jti");
        var expected = $"{{\"aud\":\"diten-auth-service\",\"contract_id\":\"platform.entitlement-attestation\",\"contract_version\":\"1.0\",\"decision\":\"Active\",\"iat\":\"2026-08-25T10:11:12.345Z\",\"iss\":\"diten-platform-service\",\"jti\":\"fixture-jti\",\"module_applicability_version\":7,\"module_code\":\"PPM\",\"physical_entitlement_version\":3,\"request_hash\":\"{RequestHash}\",\"resolved_at_utc\":\"2026-08-25T10:11:12.345Z\",\"subscription_version\":5,\"tenant_id\":\"11111111-2222-3333-4444-555555555555\",\"valid_until_utc\":\"2026-08-25T10:11:27.345Z\"}}";
        Assert.Equal(expected, Encoding.UTF8.GetString(bytes));
        Assert.Equal("607a718d4cc46e5512632a1452d433f4f06799c433082fc39a62fd5e266ea4ed", Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    [Fact]
    public void Rs256_binding_and_hard_15_second_boundary_fail_closed()
    {
        using var rsa = RSA.Create(3072); var identity = new Identity("key-2026-01", rsa, false);
        var signer = new EntitlementAttestationSigner(identity); var issued = new DateTimeOffset(2026,8,25,10,0,0,TimeSpan.Zero);
        var decision = new EntitlementDecisionSnapshotV1(Tenant, "PPM", RequestHash, EntitlementDecisionV1.Active, new(1,1,1), issued);
        var token = signer.Sign(decision, issued, "jti-1");
        var valid = new EntitlementAttestationValidationContext(Tenant, "PPM", RequestHash, issued.AddSeconds(14.999), identity.KeyId, rsa);
        Assert.True(EntitlementAttestationValidatorV1.TryValidate(token.Token, valid, out _));
        Assert.False(EntitlementAttestationValidatorV1.TryValidate(token.Token, valid with { NowUtc = issued.AddSeconds(15) }, out _));
        Assert.False(EntitlementAttestationValidatorV1.TryValidate(token.Token, valid with { TenantId = Guid.NewGuid() }, out _));
        Assert.False(EntitlementAttestationValidatorV1.TryValidate(token.Token, valid with { ModuleCode = "MDM" }, out _));
        Assert.False(EntitlementAttestationValidatorV1.TryValidate(token.Token, valid with { ModuleCode = "ppm" }, out _));
        Assert.False(EntitlementAttestationValidatorV1.TryValidate(token.Token, valid with { RequestHash = RequestHash[..^1] + "A" }, out _));
        Assert.False(EntitlementAttestationValidatorV1.TryValidate(token.Token, valid with { KeyId = "wrong" }, out _));
    }

    [Fact]
    public void Test_only_or_small_signing_identity_is_rejected()
    {
        using var rsa = RSA.Create(3072); var decision = new EntitlementDecisionSnapshotV1(Tenant,"PPM",RequestHash,EntitlementDecisionV1.Active,new(1,1,1),DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() => new EntitlementAttestationSigner(new Identity("test", rsa, true)));
        using var small = RSA.Create(2048);
        Assert.Throws<InvalidOperationException>(() => new EntitlementAttestationSigner(new Identity("small", small, false)));
    }

    [Theory]
    [InlineData("iss", "wrong-issuer")]
    [InlineData("aud", "wrong-audience")]
    public void Wrong_issuer_or_audience_is_rejected(string claim, string replacement)
    {
        using var rsa = RSA.Create(3072); var issued = new DateTimeOffset(2026,8,25,10,0,0,TimeSpan.Zero);
        var identity = new Identity("kid-1", rsa, false);
        var decision = new EntitlementDecisionSnapshotV1(Tenant,"PPM",RequestHash,EntitlementDecisionV1.Active,new(1,1,1),issued);
        var token = new EntitlementAttestationSigner(identity).Sign(decision, issued, "jti").Token;
        var original = claim == "iss" ? EntitlementAttestationContractV1.Issuer : EntitlementAttestationContractV1.Audience;
        var tampered = ReplaceJwtText(token, original, replacement, 1);
        Assert.False(EntitlementAttestationValidatorV1.TryValidate(tampered, new(Tenant,"PPM",RequestHash,issued.AddSeconds(1),identity.KeyId,rsa), out _));
    }

    [Theory]
    [InlineData("diten-entitlement-attestation+jwt", "wrong+jwt")]
    [InlineData("RS256", "HS256")]
    public void Wrong_typ_or_alg_is_rejected(string original, string replacement)
    {
        using var rsa = RSA.Create(3072); var issued = new DateTimeOffset(2026,8,25,10,0,0,TimeSpan.Zero);
        var identity = new Identity("kid-1", rsa, false);
        var decision = new EntitlementDecisionSnapshotV1(Tenant,"PPM",RequestHash,EntitlementDecisionV1.Active,new(1,1,1),issued);
        var token = new EntitlementAttestationSigner(identity).Sign(decision, issued, "jti").Token;
        Assert.False(EntitlementAttestationValidatorV1.TryValidate(ReplaceJwtText(token, original, replacement, 0), new(Tenant,"PPM",RequestHash,issued.AddSeconds(1),identity.KeyId,rsa), out _));
    }

    [Fact]
    public void Exact_algorithm_guard_rejects_valid_rsa_signature_with_wrong_alg()
    {
        using var rsa = RSA.Create(3072); var issued = new DateTimeOffset(2026,8,25,10,0,0,TimeSpan.Zero);
        var identity = new Identity("kid-1", rsa, false); var decision = new EntitlementDecisionSnapshotV1(Tenant,"PPM",RequestHash,EntitlementDecisionV1.Active,new(1,1,1),issued);
        var token = new EntitlementAttestationSigner(identity).Sign(decision, issued, "jti").Token;
        var resigned = ReplaceJwtTextAndResign(token, "RS256", "HS256", 0, rsa);
        Assert.False(EntitlementAttestationValidatorV1.TryValidate(resigned, new(Tenant,"PPM",RequestHash,issued.AddSeconds(1),identity.KeyId,rsa), out _));
    }

    [Fact]
    public void Exact_issuer_guard_rejects_valid_signature_with_wrong_issuer()
    {
        using var rsa = RSA.Create(3072); var issued = new DateTimeOffset(2026,8,25,10,0,0,TimeSpan.Zero);
        var identity = new Identity("kid-1", rsa, false); var decision = new EntitlementDecisionSnapshotV1(Tenant,"PPM",RequestHash,EntitlementDecisionV1.Active,new(1,1,1),issued);
        var token = new EntitlementAttestationSigner(identity).Sign(decision, issued, "jti").Token;
        var resigned = ReplaceJwtTextAndResign(token, EntitlementAttestationContractV1.Issuer, "wrong-issuer", 1, rsa);
        Assert.False(EntitlementAttestationValidatorV1.TryValidate(resigned, new(Tenant,"PPM",RequestHash,issued.AddSeconds(1),identity.KeyId,rsa), out _));
    }

    private static string ReplaceJwtText(string token, string original, string replacement, int part)
    {
        var pieces = token.Split('.');
        var padded = pieces[part].Replace('-', '+').Replace('_', '/'); padded += new string('=', (4 - padded.Length % 4) % 4);
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded)).Replace(original, replacement, StringComparison.Ordinal);
        pieces[part] = Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+','-').Replace('/','_');
        return string.Join('.', pieces);
    }

    private static string ReplaceJwtTextAndResign(string token, string original, string replacement, int part, RSA rsa)
    {
        var pieces = ReplaceJwtText(token, original, replacement, part).Split('.');
        pieces[2] = Convert.ToBase64String(rsa.SignData(Encoding.ASCII.GetBytes($"{pieces[0]}.{pieces[1]}"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)).TrimEnd('=').Replace('+','-').Replace('/','_');
        return string.Join('.', pieces);
    }

    private static PlatformEntitlementDecisionProvider CreateProvider(IAuthoritativeEntitlementDecisionSource source, bool enabled) =>
        new(source, new(), Options.Create(new PlatformEntitlementAttestationOptions { Enabled = enabled, AuthorityTimeout = TimeSpan.FromSeconds(1) }));
    private sealed record Identity(string KeyId, RSA Rsa, bool IsTestOnly) : IEntitlementAttestationSigningIdentity;
    private sealed class FakeSource(EntitlementStateVersionV1 version, EntitlementDecisionV1 decision) : IAuthoritativeEntitlementDecisionSource
    {
        public int Reads { get; private set; }
        public Task<EntitlementStateVersionV1> ReadCurrentVersionAsync(Guid t,string m,CancellationToken c) => Task.FromResult(version);
        public Task<EntitlementDecisionSnapshotV1> ReadAsync(Guid t,string m,string h,CancellationToken c) { Reads++; return Task.FromResult(new EntitlementDecisionSnapshotV1(t,m,h,decision,version,DateTimeOffset.UtcNow)); }
    }
    private sealed class MutatingSource : IAuthoritativeEntitlementDecisionSource
    {
        private int _versions; public int DecisionReads { get; private set; }
        public Task<EntitlementStateVersionV1> ReadCurrentVersionAsync(Guid t,string m,CancellationToken c) => Task.FromResult(++_versions == 1 ? new EntitlementStateVersionV1(1,1,1) : new(2,1,1));
        public Task<EntitlementDecisionSnapshotV1> ReadAsync(Guid t,string m,string h,CancellationToken c) { DecisionReads++; return Task.FromResult(new EntitlementDecisionSnapshotV1(t,m,h,EntitlementDecisionV1.Active,new(1,1,1),DateTimeOffset.UtcNow)); }
    }
    private sealed class FenceChangedSource : IAuthoritativeEntitlementDecisionSource
    {
        private int reads;
        public Task<EntitlementStateVersionV1> ReadCurrentVersionAsync(Guid t,string m,CancellationToken c) => Task.FromResult(++reads == 1 ? new EntitlementStateVersionV1(1,1,1) : new(2,1,1));
        public Task<EntitlementDecisionSnapshotV1> ReadAsync(Guid t,string m,string h,CancellationToken c) => Task.FromResult(new EntitlementDecisionSnapshotV1(t,m,h,EntitlementDecisionV1.Active,new(2,1,1),DateTimeOffset.UtcNow));
    }
    private sealed class DecisionMismatchSource : IAuthoritativeEntitlementDecisionSource
    {
        public Task<EntitlementStateVersionV1> ReadCurrentVersionAsync(Guid t,string m,CancellationToken c) => Task.FromResult(new EntitlementStateVersionV1(2,1,1));
        public Task<EntitlementDecisionSnapshotV1> ReadAsync(Guid t,string m,string h,CancellationToken c) => Task.FromResult(new EntitlementDecisionSnapshotV1(t,m,h,EntitlementDecisionV1.Active,new(1,1,1),DateTimeOffset.UtcNow));
    }
    public enum SourceFailure { Unavailable, Malformed, Incomplete, Timeout }
    private sealed class FailingSource(SourceFailure failure) : IAuthoritativeEntitlementDecisionSource
    {
        public async Task<EntitlementStateVersionV1> ReadCurrentVersionAsync(Guid t,string m,CancellationToken c)
        {
            if (failure == SourceFailure.Timeout) { await Task.Delay(TimeSpan.FromSeconds(5), c); }
            if (failure == SourceFailure.Unavailable) throw new IOException("authority unavailable");
            if (failure == SourceFailure.Malformed) throw new FormatException("malformed authority");
            return failure == SourceFailure.Incomplete ? new(0,1,1) : new(1,1,1);
        }
        public Task<EntitlementDecisionSnapshotV1> ReadAsync(Guid t,string m,string h,CancellationToken c) => throw new InvalidOperationException();
    }
}
