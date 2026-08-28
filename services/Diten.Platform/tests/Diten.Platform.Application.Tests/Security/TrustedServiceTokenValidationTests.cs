using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Diten.Platform.API.Configuration;
using Diten.Platform.API.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using JsonWebToken = Microsoft.IdentityModel.JsonWebTokens.JsonWebToken;
using Xunit;

namespace Diten.Platform.Application.Tests.Security;

public sealed class TrustedServiceTokenValidationTests : IDisposable
{
    private const string Issuer = "https://auth.service.local";
    private readonly RSA _current = RSA.Create(2048);
    private readonly RSA _previous = RSA.Create(2048);
    private readonly TestTimeProvider _clock = new(DateTimeOffset.UtcNow);

    [Fact]
    public void Current_rs256_key_and_exact_claims_are_accepted()
    {
        var options = Options();
        var token = Token(_current, "current-key", SecurityAlgorithms.RsaSha256);

        var validation = Validate(token, options);

        Assert.True(TrustedServiceTokenValidationExtensions.HasExactServiceClaims(validation.Token, Issuer));
        Assert.True(TrustedServiceTokenValidationExtensions.HasExactServiceClaims(new JsonWebToken(token), Issuer));
        Assert.Equal("service", validation.Principal.Claims.Single(claim => claim.Type == "actor_type").Value);
        Assert.Equal("Diten.MDM", validation.Principal.Claims.Single(claim => claim.Type == "service_name").Value);
    }

    [Fact]
    public async Task Named_jwt_bearer_scheme_executes_signature_and_exact_claim_event_validation()
    {
        var options = Options();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{TrustedServiceTokenValidationOptions.SectionName}:Issuer"] = options.Issuer,
                [$"{TrustedServiceTokenValidationOptions.SectionName}:CurrentKeyId"] = options.CurrentKeyId,
                [$"{TrustedServiceTokenValidationOptions.SectionName}:CurrentPublicKeyPem"] = options.CurrentPublicKeyPem
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication("HumanBearer");
        services.AddTrustedServiceTokenValidation(configuration, _clock);
        await using var provider = services.BuildServiceProvider();

        var accepted = await AuthenticateAsync(provider, Token(_current, "current-key", SecurityAlgorithms.RsaSha256));
        var rejected = await AuthenticateAsync(provider, Token(
            new SigningCredentials(
                new RsaSecurityKey(_current.ExportParameters(true)) { KeyId = "current-key" },
                SecurityAlgorithms.RsaSha256),
            lifetimeSeconds: 299));

        Assert.True(accepted.Succeeded);
        var acceptedClaims = Assert.IsAssignableFrom<ClaimsPrincipal>(accepted.Principal).Claims.ToArray();
        Assert.Equal(10, acceptedClaims.Length);
        Assert.Equal(
            new[] { "actor_type", "aud", "exp", "iat", "iss", "jti", "nbf", "service_name", "sub", "tenant_id" },
            acceptedClaims.Select(claim => claim.Type).Order(StringComparer.Ordinal));
        Assert.All(acceptedClaims.GroupBy(claim => claim.Type, StringComparer.Ordinal), group => Assert.Single(group));
        Assert.False(rejected.Succeeded);
    }

    [Fact]
    public void Previous_key_is_accepted_only_strictly_before_overlap_expiry()
    {
        var options = Options(previousValidUntilUtc: _clock.GetUtcNow().AddMinutes(1));
        var token = Token(_previous, "previous-key", SecurityAlgorithms.RsaSha256);

        Assert.True(TrustedServiceTokenValidationExtensions.HasExactServiceClaims(Validate(token, options).Token, Issuer));

        _clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Throws<SecurityTokenSignatureKeyNotFoundException>(() => Validate(token, options));
    }

    [Fact]
    public void Unknown_kid_is_rejected()
    {
        var token = Token(_current, "unknown-key", SecurityAlgorithms.RsaSha256);

        Assert.Throws<SecurityTokenSignatureKeyNotFoundException>(() => Validate(token, Options()));
    }

    [Fact]
    public void Missing_kid_is_rejected()
    {
        var token = Token(new SigningCredentials(
            new RsaSecurityKey(_current.ExportParameters(true)),
            SecurityAlgorithms.RsaSha256));

        Assert.Throws<SecurityTokenSignatureKeyNotFoundException>(() => Validate(token, Options()));
    }

    [Fact]
    public void Hs256_algorithm_confusion_is_rejected()
    {
        var signingKey = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(64)) { KeyId = "current-key" };
        var token = Token(new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        Assert.ThrowsAny<SecurityTokenException>(() => Validate(token, Options()));
    }

    [Fact]
    public void Unsigned_none_token_is_rejected()
    {
        var token = Token(signingCredentials: null);

        Assert.Throws<SecurityTokenInvalidSignatureException>(() => Validate(token, Options()));
    }

    [Theory]
    [InlineData("actor_type", "human")]
    [InlineData("service_name", "Other.Service")]
    [InlineData("tenant_id", "00000000-0000-0000-0000-000000000000")]
    [InlineData("sub", "not-a-guid")]
    public void Wrong_exact_service_claim_is_rejected(string claimType, string claimValue)
    {
        var token = Token(_current, "current-key", SecurityAlgorithms.RsaSha256, claimType, claimValue);
        var validation = Validate(token, Options());

        Assert.False(TrustedServiceTokenValidationExtensions.HasExactServiceClaims(validation.Token, Issuer));
    }

    [Fact]
    public void Duplicate_tenant_or_user_authorization_claim_is_rejected()
    {
        var duplicateTenant = Token(
            _current,
            "current-key",
            SecurityAlgorithms.RsaSha256,
            additionalClaims: [new Claim("tenant_id", Guid.NewGuid().ToString("D"))]);
        var role = Token(
            _current,
            "current-key",
            SecurityAlgorithms.RsaSha256,
            additionalClaims: [new Claim("role", "Admin")]);

        Assert.False(TrustedServiceTokenValidationExtensions.HasExactServiceClaims(
            Validate(duplicateTenant, Options()).Token, Issuer));
        Assert.False(TrustedServiceTokenValidationExtensions.HasExactServiceClaims(
            Validate(role, Options()).Token, Issuer));
    }

    [Theory]
    [InlineData(299, 0)]
    [InlineData(301, 0)]
    [InlineData(300, -1)]
    public void Signed_token_with_non_exact_ttl_or_iat_nbf_pair_is_rejected(int lifetimeSeconds, int issuedAtOffsetSeconds)
    {
        var token = Token(
            new SigningCredentials(
                new RsaSecurityKey(_current.ExportParameters(true)) { KeyId = "current-key" },
                SecurityAlgorithms.RsaSha256),
            lifetimeSeconds: lifetimeSeconds,
            issuedAtOffsetSeconds: issuedAtOffsetSeconds);

        var validation = Validate(token, Options());

        Assert.False(TrustedServiceTokenValidationExtensions.HasExactServiceClaims(validation.Token, Issuer));
    }

    [Fact]
    public void Wrong_issuer_or_audience_is_rejected_by_cryptographic_validation_contract()
    {
        var wrongIssuer = Token(_current, "current-key", SecurityAlgorithms.RsaSha256, issuer: "https://other.local");
        var wrongAudience = Token(_current, "current-key", SecurityAlgorithms.RsaSha256, audience: "OTHER_AUDIENCE");

        Assert.Throws<SecurityTokenInvalidIssuerException>(() => Validate(wrongIssuer, Options()));
        Assert.Throws<SecurityTokenInvalidAudienceException>(() => Validate(wrongAudience, Options()));
    }

    [Fact]
    public void Malformed_or_private_key_configuration_fails_closed()
    {
        var duplicateKids = Options();
        duplicateKids.PreviousKeyId = duplicateKids.CurrentKeyId;
        duplicateKids.PreviousPublicKeyPem = _previous.ExportSubjectPublicKeyInfoPem();
        duplicateKids.PreviousValidUntilUtc = _clock.GetUtcNow().AddMinutes(1);

        var privateKey = Options();
        privateKey.CurrentPublicKeyPem = _current.ExportPkcs8PrivateKeyPem();

        var incompletePrevious = Options();
        incompletePrevious.PreviousKeyId = "previous-key";

        Assert.Throws<InvalidOperationException>(() => Parameters(duplicateKids));
        Assert.Throws<InvalidOperationException>(() => Parameters(privateKey));
        Assert.Throws<InvalidOperationException>(() => Parameters(incompletePrevious));
    }

    [Fact]
    public void Missing_validator_configuration_fails_closed()
    {
        Assert.Throws<InvalidOperationException>(() => Parameters(new TrustedServiceTokenValidationOptions()));
    }

    [Fact]
    public void Rsa_key_smaller_than_2048_bits_fails_closed()
    {
        using var weakKey = RSA.Create(1024);
        var options = Options();
        options.CurrentPublicKeyPem = weakKey.ExportSubjectPublicKeyInfoPem();

        Assert.Throws<InvalidOperationException>(() => Parameters(options));
    }

    [Fact]
    public void Previous_key_overlap_boundary_must_be_utc()
    {
        var options = Options();
        options.PreviousKeyId = "previous-key";
        options.PreviousPublicKeyPem = _previous.ExportSubjectPublicKeyInfoPem();
        options.PreviousValidUntilUtc = new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.FromHours(3));

        Assert.Throws<InvalidOperationException>(() => Parameters(options));
    }

    public void Dispose()
    {
        _current.Dispose();
        _previous.Dispose();
    }

    private TrustedServiceTokenValidationOptions Options(DateTimeOffset? previousValidUntilUtc = null) => new()
    {
        Issuer = Issuer,
        CurrentKeyId = "current-key",
        CurrentPublicKeyPem = _current.ExportSubjectPublicKeyInfoPem(),
        PreviousKeyId = previousValidUntilUtc is null ? null : "previous-key",
        PreviousPublicKeyPem = previousValidUntilUtc is null ? null : _previous.ExportSubjectPublicKeyInfoPem(),
        PreviousValidUntilUtc = previousValidUntilUtc
    };

    private TokenValidationParameters Parameters(TrustedServiceTokenValidationOptions options) =>
        TrustedServiceTokenValidationExtensions.CreateTokenValidationParameters(options, _clock);

    private (ClaimsPrincipal Principal, SecurityToken Token) Validate(
        string token,
        TrustedServiceTokenValidationOptions options)
    {
        var principal = new JwtSecurityTokenHandler().ValidateToken(token, Parameters(options), out var validatedToken);
        return (principal, validatedToken);
    }

    private static async Task<AuthenticateResult> AuthenticateAsync(IServiceProvider provider, string token)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Headers.Authorization = $"Bearer {token}";
        return await context.AuthenticateAsync(TrustedServiceTokenValidationExtensions.AuthenticationScheme);
    }

    private string Token(
        RSA key,
        string keyId,
        string algorithm,
        string? replacedClaimType = null,
        string? replacedClaimValue = null,
        IEnumerable<Claim>? additionalClaims = null,
        string issuer = Issuer,
        string audience = TrustedServiceTokenValidationExtensions.RequiredAudience,
        int lifetimeSeconds = 300,
        int issuedAtOffsetSeconds = 0) =>
        Token(
            new SigningCredentials(
                new RsaSecurityKey(key.ExportParameters(true)) { KeyId = keyId },
                algorithm),
            replacedClaimType,
            replacedClaimValue,
            additionalClaims,
            issuer,
            audience,
            lifetimeSeconds,
            issuedAtOffsetSeconds);

    private string Token(
        SigningCredentials? signingCredentials,
        string? replacedClaimType = null,
        string? replacedClaimValue = null,
        IEnumerable<Claim>? additionalClaims = null,
        string issuer = Issuer,
        string audience = TrustedServiceTokenValidationExtensions.RequiredAudience,
        int lifetimeSeconds = 300,
        int issuedAtOffsetSeconds = 0)
    {
        var now = DateTimeOffset.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString("D")),
            new("actor_type", "service"),
            new("service_name", TrustedServiceTokenValidationExtensions.RequiredServiceName),
            new("tenant_id", Guid.NewGuid().ToString("D")),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D")),
            new(JwtRegisteredClaimNames.Iat, now.AddSeconds(issuedAtOffsetSeconds).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        if (replacedClaimType is not null)
        {
            var index = claims.FindIndex(claim => string.Equals(claim.Type, replacedClaimType, StringComparison.Ordinal));
            Assert.True(index >= 0);
            claims[index] = new Claim(replacedClaimType, replacedClaimValue!);
        }

        if (additionalClaims is not null)
        {
            claims.AddRange(additionalClaims);
        }

        var jwt = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            now.UtcDateTime,
            now.AddSeconds(lifetimeSeconds).UtcDateTime,
            signingCredentials);
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan amount) => _now = _now.Add(amount);
    }
}
