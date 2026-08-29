using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.S2S;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.S2S;
using Diten.AuthService.Infrastructure.S2S;
using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.Infrastructure.Settings;
using Diten.BuildingBlocks.Security.Secrets;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Diten.AuthService.Application.Tests.S2S;

public sealed class DelegatedActorProofCryptographicTests : IDisposable
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(2_000_000_000);
    private readonly RSA _rsa = RSA.Create(3072);
    private readonly FixedTimeProvider _clock = new(Now);
    private readonly ServicePrincipal _principal;
    private readonly ServiceCredentialDescriptor _credential;
    private readonly MemoryKeyProvider _keys;
    private readonly MemoryPrincipalRepository _principals;
    private readonly MemoryCredentialRepository _credentials;

    public DelegatedActorProofCryptographicTests()
    {
        _principal = CreatePrincipal();
        _credential = CreateCredential(_principal.ServicePrincipalId);
        _keys = new MemoryKeyProvider(_rsa, _principal, _credential);
        _principals = new MemoryPrincipalRepository(_principal);
        _credentials = new MemoryCredentialRepository(_credential);
    }

    [Fact]
    public async Task Valid_rs256_proof_issues_and_validates_to_typed_provenance_once()
    {
        var replay = new MemoryReplayStore();
        var proof = CreateProof();
        var issue = await CreateIssuer().IssueAsync(new(proof, _principal, _credential, _credential.Kid), CancellationToken.None);
        Assert.True(issue.Succeeded);
        Assert.NotNull(issue.Token);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(issue.Token);
        Assert.Equal("RS256", token.Header.Alg);
        Assert.Equal(DelegatedActorProofV1.ExactType, token.Header.Typ);
        Assert.Equal(_credential.Kid, token.Header.Kid);

        var validator = CreateValidator(replay);
        var request = Request(issue.Token!);
        var first = await validator.ValidateAsync(request, CancellationToken.None);
        var second = await validator.ValidateAsync(request, CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.NotNull(first.Provenance);
        Assert.Equal(_principal.ServicePrincipalId, first.Provenance.ServicePrincipalId);
        Assert.Equal(_credential.CredentialId, first.Provenance.CredentialId);
        Assert.Equal(S2SAuthenticationFailureCode.Replay, second.Failure);
        Assert.Equal(1, replay.Count);
        Assert.DoesNotContain(typeof(DelegatedActorProofProvenance).GetProperties(), x => x.PropertyType == typeof(ClaimsPrincipal));
    }

    [Theory]
    [InlineData("alg", "HS256", S2SAuthenticationFailureCode.InvalidAlgorithm)]
    [InlineData("alg", "none", S2SAuthenticationFailureCode.InvalidAlgorithm)]
    [InlineData("alg", "RS384", S2SAuthenticationFailureCode.InvalidAlgorithm)]
    [InlineData("alg", "RS512", S2SAuthenticationFailureCode.InvalidAlgorithm)]
    [InlineData("typ", "JWT", S2SAuthenticationFailureCode.InvalidTokenType)]
    [InlineData("kid", " key-01", S2SAuthenticationFailureCode.InvalidKeyIdentifier)]
    [InlineData("kid", "KEY-01", S2SAuthenticationFailureCode.UnknownKey)]
    [InlineData("kid", "unknown", S2SAuthenticationFailureCode.UnknownKey)]
    public async Task Exact_header_guards_reject_without_replay(string field, string value, S2SAuthenticationFailureCode expected)
    {
        var replay = new MemoryReplayStore();
        var token = (await CreateIssuer().IssueAsync(new(CreateProof(), _principal, _credential, _credential.Kid), CancellationToken.None)).Token!;
        var mutated = MutateHeader(token, field, value);

        var result = await CreateValidator(replay).ValidateAsync(Request(mutated), CancellationToken.None);

        Assert.Equal(expected, result.Failure);
        Assert.Equal(0, replay.Count);
    }

    [Fact]
    public async Task Rsa_2048_and_provider_unavailable_are_typed_failures()
    {
        using var weakRsa = RSA.Create(2048);
        var weakKeys = new MemoryKeyProvider(weakRsa, _principal, _credential);
        var weakIssuer = new DelegatedActorProofIssuer(weakKeys, new(), _clock);
        var weak = await weakIssuer.IssueAsync(new(CreateProof(), _principal, _credential, _credential.Kid), CancellationToken.None);
        Assert.Equal(S2SAuthenticationFailureCode.InvalidCredential, weak.Failure);

        _keys.Unavailable = true;
        var unavailable = await CreateIssuer().IssueAsync(new(CreateProof(), _principal, _credential, _credential.Kid), CancellationToken.None);
        Assert.Equal(503, unavailable.SuggestedHttpStatusCode);
    }

    [Fact]
    public async Task Signature_issuer_audience_lifetime_and_claim_failures_do_not_write_replay()
    {
        var replay = new MemoryReplayStore();
        var validator = CreateValidator(replay);
        var proof = CreateProof();
        var valid = RawToken(proof);
        var invalidSignature = valid[..^1] + (valid[^1] == 'A' ? 'B' : 'A');
        Assert.Equal(S2SAuthenticationFailureCode.InvalidSignature, (await validator.ValidateAsync(Request(invalidSignature), CancellationToken.None)).Failure);

        Assert.Equal(S2SAuthenticationFailureCode.InvalidIssuer,
            (await validator.ValidateAsync(Request(RawToken(proof with { Issuer = "wrong-issuer" })), CancellationToken.None)).Failure);
        Assert.Equal(S2SAuthenticationFailureCode.InvalidAudience,
            (await validator.ValidateAsync(Request(RawToken(proof with { Audience = "diten-erp" })), CancellationToken.None)).Failure);
        Assert.Equal(S2SAuthenticationFailureCode.InvalidLifetime,
            (await validator.ValidateAsync(Request(RawToken(proof with
            {
                IssuedAt = Now.ToUnixTimeSeconds() - 331,
                NotBefore = Now.ToUnixTimeSeconds() - 331,
                ExpiresAt = Now.ToUnixTimeSeconds() - 31
            })), CancellationToken.None)).Failure);
        Assert.Equal(S2SAuthenticationFailureCode.InvalidClaims,
            (await validator.ValidateAsync(Request(RawToken(proof with
            {
                ExpiresAt = Now.ToUnixTimeSeconds() + DelegatedActorProofV1.MaximumLifetimeSeconds + 1
            })), CancellationToken.None)).Failure);
        Assert.Equal(S2SAuthenticationFailureCode.InvalidClaims,
            (await validator.ValidateAsync(Request(RemovePayloadClaim(valid, "nonce")), CancellationToken.None)).Failure);
        Assert.Equal(0, replay.Count);
    }

    [Fact]
    public async Task Request_binding_principal_credential_key_and_cancellation_are_fail_closed()
    {
        var token = (await CreateIssuer().IssueAsync(new(CreateProof(), _principal, _credential, _credential.Kid), CancellationToken.None)).Token!;
        var replay = new MemoryReplayStore();
        Assert.Equal(S2SAuthenticationFailureCode.InvalidRequestBinding,
            (await CreateValidator(replay).ValidateAsync(Request(token) with { Path = "/internal/other" }, CancellationToken.None)).Failure);

        _principal.TransitionTo(ServicePrincipalStatus.Suspended, "test", Now);
        Assert.Equal(S2SAuthenticationFailureCode.InactivePrincipal,
            (await CreateValidator(replay).ValidateAsync(Request(token), CancellationToken.None)).Failure);
        Assert.Equal(0, replay.Count);

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateValidator(replay).ValidateAsync(Request(token), cancelled.Token));
        Assert.Equal(0, replay.Count);
    }

    [Fact]
    public async Task Revoked_expired_unknown_and_unavailable_validation_keys_are_distinct_and_do_not_replay()
    {
        var token = RawToken(CreateProof());
        var replay = new MemoryReplayStore();

        _keys.Revoked = true;
        Assert.Equal(S2SAuthenticationFailureCode.UnknownKey, (await CreateValidator(replay).ValidateAsync(Request(token), CancellationToken.None)).Failure);
        _keys.Revoked = false;
        _keys.Expired = true;
        Assert.Equal(S2SAuthenticationFailureCode.UnknownKey, (await CreateValidator(replay).ValidateAsync(Request(token), CancellationToken.None)).Failure);
        _keys.Expired = false;
        _keys.Unavailable = true;
        var unavailable = await CreateValidator(replay).ValidateAsync(Request(token), CancellationToken.None);
        Assert.Equal(S2SAuthenticationFailureCode.AuthorityUnavailable, unavailable.Failure);
        Assert.Equal(503, unavailable.SuggestedHttpStatusCode);
        Assert.Equal(0, replay.Count);
    }

    [Fact]
    public async Task User_and_s2s_token_families_never_fallback()
    {
        var userService = new TokenService(Options.Create(new JwtSettings
        {
            Secret = new string('u', 48), Issuer = "user-issuer", Audience = "user-audience", AccessTokenExpirationMinutes = 5
        }), new NoOpRotationResolver());
        var userToken = userService.GenerateAccessToken(new User("user@example.test", "hash", "User", "Test", Guid.NewGuid()), [], []);
        var replay = new MemoryReplayStore();
        Assert.Equal(S2SAuthenticationFailureCode.InvalidTokenType,
            (await CreateValidator(replay).ValidateAsync(Request(userToken), CancellationToken.None)).Failure);

        var s2s = RawToken(CreateProof());
        Assert.ThrowsAny<SecurityTokenException>(() => userService.GetPrincipalFromExpiredToken(s2s));
        Assert.Equal(0, replay.Count);
    }

    [Fact]
    public async Task Exact_clock_skew_boundaries_are_deterministic()
    {
        var within = CreateProof() with { IssuedAt = Now.ToUnixTimeSeconds() + 30, NotBefore = Now.ToUnixTimeSeconds() + 30, ExpiresAt = Now.ToUnixTimeSeconds() + 330 };
        Assert.True((await CreateValidator(new MemoryReplayStore()).ValidateAsync(Request(RawToken(within)), CancellationToken.None)).Succeeded);

        var future = within with { IssuedAt = Now.ToUnixTimeSeconds() + 31, NotBefore = Now.ToUnixTimeSeconds() + 31, ExpiresAt = Now.ToUnixTimeSeconds() + 331 };
        Assert.Equal(S2SAuthenticationFailureCode.InvalidLifetime,
            (await CreateValidator(new MemoryReplayStore()).ValidateAsync(Request(RawToken(future)), CancellationToken.None)).Failure);

        var expiredWithin = CreateProof() with { IssuedAt = Now.ToUnixTimeSeconds() - 330, NotBefore = Now.ToUnixTimeSeconds() - 330, ExpiresAt = Now.ToUnixTimeSeconds() - 30 };
        Assert.True((await CreateValidator(new MemoryReplayStore()).ValidateAsync(Request(RawToken(expiredWithin)), CancellationToken.None)).Succeeded);
        var expiredOutside = expiredWithin with { IssuedAt = Now.ToUnixTimeSeconds() - 331, NotBefore = Now.ToUnixTimeSeconds() - 331, ExpiresAt = Now.ToUnixTimeSeconds() - 31 };
        Assert.Equal(S2SAuthenticationFailureCode.InvalidLifetime,
            (await CreateValidator(new MemoryReplayStore()).ValidateAsync(Request(RawToken(expiredOutside)), CancellationToken.None)).Failure);
    }

    public void Dispose() => _rsa.Dispose();

    private DelegatedActorProofIssuer CreateIssuer() => new(_keys, new(), _clock);
    private DelegatedActorProofValidator CreateValidator(IS2SProofAcceptanceCoordinator acceptance) => new(_keys, _principals, _credentials, acceptance, new(), _clock);

    private DelegatedActorProofValidationRequest Request(string token) => new(token, "POST", "/internal/budgets", Encoding.UTF8.GetBytes("{\"value\":1}"),
        Guid.Parse("11111111-1111-1111-1111-111111111111"), "budget.create");

    private DelegatedActorProofV1 CreateProof()
    {
        var tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var hash = CanonicalRequestBinding.Compute("POST", "/internal/budgets", tenant, "budget.create", Encoding.UTF8.GetBytes("{\"value\":1}"));
        var now = Now.ToUnixTimeSeconds();
        return new(DelegatedActorProofV1.ExactType, DelegatedActorProofV1.ExactIssuer, "diten-fpa-service",
            _principal.ServicePrincipalId, _principal.ClientId, _principal.ClientId, Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"),
            tenant, Guid.NewGuid(), Guid.NewGuid(), "budget.create", ["fpa.budget.create"], DelegatedActorProofV1.ExactScope,
            hash, now, now, now + 300, 1, _principal.PrincipalVersion, _credential.Generation);
    }

    private ServicePrincipal CreatePrincipal()
    {
        var principal = new ServicePrincipal(Guid.NewGuid(), "diten-fpa-producer", "FPA", ["MOD-0136"], ["diten-fpa-service"],
            [DelegatedActorProofV1.ExactScope], Now.AddDays(-1), Now.AddDays(1), "test");
        principal.AdvanceCredentialGeneration(1, "test", Now);
        principal.TransitionTo(ServicePrincipalStatus.Active, "test", Now);
        return principal;
    }

    private ServiceCredentialDescriptor CreateCredential(Guid principalId)
    {
        var descriptor = new ServiceCredentialDescriptor(Guid.NewGuid(), principalId, "key-01", "RS256", 3072,
            "memory-public-reference", "memory-thumbprint", Now.AddDays(-1), Now.AddDays(1), 1, Now.AddHours(1), "test");
        descriptor.TransitionTo(ServiceCredentialStatus.Active, "test", Now);
        return descriptor;
    }

    private string RawToken(DelegatedActorProofV1 proof)
    {
        var header = new JwtHeader(new SigningCredentials(new RsaSecurityKey(_rsa) { KeyId = _credential.Kid }, SecurityAlgorithms.RsaSha256))
        {
            ["typ"] = DelegatedActorProofV1.ExactType, ["kid"] = _credential.Kid
        };
        var payload = new JwtPayload();
        foreach (var claim in Claims(proof)) payload.AddClaim(new Claim(claim.Type, claim.Value));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(header, payload));
    }

    private static IEnumerable<S2SClaim> Claims(DelegatedActorProofV1 proof)
    {
        var claims = new List<S2SClaim>
        {
            new("typ", proof.Type), new("iss", proof.Issuer), new("aud", proof.Audience), new("sub", proof.ServicePrincipalId.ToString("D")),
            new("client_id", proof.ClientId), new("azp", proof.AuthorizedParty), new("actor_type", "service"),
            new("tenant_id", proof.TenantId.ToString("D")), new("delegated_actor_id", proof.DelegatedActorId.ToString("D")),
            new("delegated_actor_type", "tenant_user"), new("delegation_id", proof.DelegationId.ToString("D")),
            new("operation_id", proof.OperationId), new("scope", proof.Scope), new("request_hash", proof.RequestHash),
            new("nonce", proof.Nonce), new("jti", proof.Jti), new("iat", proof.IssuedAt.ToString()), new("nbf", proof.NotBefore.ToString()),
            new("exp", proof.ExpiresAt.ToString()), new("tenant_grant_version", proof.TenantGrantVersion.ToString()),
            new("service_principal_version", proof.ServicePrincipalVersion.ToString()), new("credential_generation", proof.CredentialGeneration.ToString())
        };
        claims.AddRange(proof.Permissions.Select(x => new S2SClaim("permission", x)));
        return claims;
    }

    private static string MutateHeader(string token, string field, string value)
    {
        var parts = token.Split('.');
        var header = JsonSerializer.Deserialize<Dictionary<string, object>>(Base64UrlEncoder.Decode(parts[0]))!;
        header[field] = value;
        parts[0] = Base64UrlEncoder.Encode(JsonSerializer.Serialize(header));
        return string.Join('.', parts);
    }

    private string RemovePayloadClaim(string token, string claim)
    {
        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var proof = CreateProof();
        var header = new JwtHeader(new SigningCredentials(new RsaSecurityKey(_rsa) { KeyId = _credential.Kid }, SecurityAlgorithms.RsaSha256))
        { ["typ"] = DelegatedActorProofV1.ExactType, ["kid"] = _credential.Kid };
        var payload = new JwtPayload();
        foreach (var item in parsed.Claims.Where(x => x.Type != claim)) payload.AddClaim(item);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(header, payload));
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class MemoryKeyProvider : IS2SPrivateSigningKeyProvider, IS2STrustedValidationKeyProvider
    {
        private readonly RSA _rsa;
        private readonly ServicePrincipal _principal;
        private readonly ServiceCredentialDescriptor _credential;
        public bool Unavailable { get; set; }
        public bool Revoked { get; set; }
        public bool Expired { get; set; }

        public MemoryKeyProvider(RSA rsa, ServicePrincipal principal, ServiceCredentialDescriptor credential)
        {
            _rsa = rsa; _principal = principal; _credential = credential;
        }

        public Task<S2SKeyResolution<S2SPrivateSigningKeyDescriptor>> ResolveAsync(string issuer, string kid, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Unavailable) return Task.FromResult(S2SKeyResolution<S2SPrivateSigningKeyDescriptor>.AuthorityUnavailable());
            if (!string.Equals(issuer, DelegatedActorProofV1.ExactIssuer, StringComparison.Ordinal) || !string.Equals(kid, _credential.Kid, StringComparison.Ordinal))
                return Task.FromResult(S2SKeyResolution<S2SPrivateSigningKeyDescriptor>.Unknown());
            var key = new RsaSecurityKey(_rsa) { KeyId = kid };
            return Task.FromResult(S2SKeyResolution<S2SPrivateSigningKeyDescriptor>.Resolved(new(issuer, kid, _credential.CredentialId,
                _principal.ServicePrincipalId, _credential.Generation, _credential.NotBeforeUtc, _credential.ExpiresAtUtc, key)));
        }

        Task<S2SKeyResolution<S2STrustedValidationKeyDescriptor>> IS2STrustedValidationKeyProvider.ResolveAsync(string issuer, string kid, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Unavailable) return Task.FromResult(S2SKeyResolution<S2STrustedValidationKeyDescriptor>.AuthorityUnavailable());
            if (!string.Equals(issuer, DelegatedActorProofV1.ExactIssuer, StringComparison.Ordinal) || !string.Equals(kid, _credential.Kid, StringComparison.Ordinal))
                return Task.FromResult(S2SKeyResolution<S2STrustedValidationKeyDescriptor>.Unknown());
            var publicKey = RSA.Create();
            publicKey.ImportParameters(_rsa.ExportParameters(false));
            var key = new RsaSecurityKey(publicKey) { KeyId = kid };
            return Task.FromResult(S2SKeyResolution<S2STrustedValidationKeyDescriptor>.Resolved(new(issuer, kid, _credential.CredentialId,
                _principal.ServicePrincipalId, _credential.Generation, _credential.Status, _credential.NotBeforeUtc,
                Expired ? Now.AddSeconds(-1) : _credential.ExpiresAtUtc, _credential.OverlapValidUntilUtc, Revoked, key)));
        }
    }

    private sealed class MemoryPrincipalRepository(ServicePrincipal principal) : IServicePrincipalRepository
    {
        public Task<ServicePrincipal?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken) =>
            Task.FromResult<ServicePrincipal?>(string.Equals(clientId, principal.ClientId, StringComparison.Ordinal) ? principal : null);
        public Task<bool> TryCreateAsync(ServicePrincipal value, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> TryReplaceAsync(ServicePrincipal value, long expectedVersion, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class MemoryCredentialRepository(ServiceCredentialDescriptor credential) : IServiceCredentialDescriptorRepository
    {
        public Task<IReadOnlyList<ServiceCredentialDescriptor>> GetAcceptedAsync(Guid servicePrincipalId, DateTimeOffset atUtc, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ServiceCredentialDescriptor>>(credential.ServicePrincipalId == servicePrincipalId ? [credential] : []);
        public Task<bool> TryCreateAsync(ServiceCredentialDescriptor descriptor, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class MemoryReplayStore : IS2SProofAcceptanceCoordinator
    {
        private readonly HashSet<string> _jti = new(StringComparer.Ordinal);
        private readonly HashSet<string> _nonce = new(StringComparer.Ordinal);
        private readonly object _gate = new();
        public int Count { get { lock (_gate) return _jti.Count; } }
        public Task<S2SProofAcceptanceResult> TryAcceptAsync(S2SProofAcceptanceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var receipt = request.ReplayReceipt;
            lock (_gate)
            {
                if (!_jti.Add(receipt.Issuer + "\0" + receipt.Jti) || !_nonce.Add(receipt.Issuer + "\0" + receipt.Nonce))
                    return Task.FromResult(S2SProofAcceptanceResult.Replay());
                return Task.FromResult(S2SProofAcceptanceResult.Accepted());
            }
        }
    }

    private sealed class NoOpRotationResolver : ISecretRotationResolver
    {
        public SecurityKey GetCurrentSigningKey() => throw new NotSupportedException();
        public IReadOnlyList<SecurityKey> GetValidationKeys() => [];
    }
}
