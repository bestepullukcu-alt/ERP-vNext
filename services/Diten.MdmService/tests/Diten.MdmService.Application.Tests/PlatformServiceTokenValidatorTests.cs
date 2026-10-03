using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.MdmService.Api.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class PlatformServiceTokenValidatorTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid LegalEntityId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private const string Secret = "01234567890123456789012345678901";

    [Fact]
    public void Exact_platform_identity_and_scope_are_accepted() =>
        Assert.True(CreateValidator().IsAuthorized(CreateToken(), TenantId, ActorId, LegalEntityId));

    [Fact]
    public void Wrong_signing_key_is_rejected() =>
        Assert.False(CreateValidator().IsAuthorized(
            CreateToken(secret: "abcdefghijklmnopqrstuvwxyz123456"), TenantId, ActorId, LegalEntityId));

    [Theory]
    [InlineData("wrong-caller", "mdm.legal-entities.reference.validate")]
    [InlineData("Diten.Platform", "mdm.everything.read")]
    public void Wrong_caller_or_scope_is_rejected(string caller, string scope) =>
        Assert.False(CreateValidator().IsAuthorized(
            CreateToken(caller: caller, scope: scope), TenantId, ActorId, LegalEntityId));

    [Fact]
    public void Tenant_actor_and_legal_entity_must_all_match()
    {
        var validator = CreateValidator();
        var token = CreateToken();
        Assert.False(validator.IsAuthorized(token, Guid.NewGuid(), ActorId, LegalEntityId));
        Assert.False(validator.IsAuthorized(token, TenantId, Guid.NewGuid(), LegalEntityId));
        Assert.False(validator.IsAuthorized(token, TenantId, ActorId, Guid.NewGuid()));
    }

    [Fact]
    public void Disabled_identity_rejects_otherwise_valid_token()
    {
        var options = ValidOptions();
        options.Enabled = false;
        Assert.False(new PlatformServiceTokenValidator(Options.Create(options))
            .IsAuthorized(CreateToken(), TenantId, ActorId, LegalEntityId));
    }

    [Fact]
    public void Excessive_token_lifetime_is_rejected()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Assert.False(CreateValidator().IsAuthorized(
            CreateToken(iat: now, nbf: now, exp: now + 120), TenantId, ActorId, LegalEntityId));
    }

    [Theory]
    [InlineData(JwtRegisteredClaimNames.Sub, "other-caller")]
    [InlineData("scope", "mdm.everything.read")]
    [InlineData("tenant_id", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")]
    [InlineData("actor_id", "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")]
    [InlineData("legal_entity_id", "cccccccc-cccc-cccc-cccc-cccccccccccc")]
    [InlineData(JwtRegisteredClaimNames.Jti, "duplicate-jti")]
    [InlineData(JwtRegisteredClaimNames.Iat, "1")]
    public void Duplicate_security_claim_is_rejected(string type, string conflictingValue)
    {
        var duplicate = new Claim(type, conflictingValue, type == JwtRegisteredClaimNames.Iat
            ? ClaimValueTypes.Integer64
            : ClaimValueTypes.String);
        Assert.False(CreateValidator().IsAuthorized(
            CreateToken(extraClaims: new[] { duplicate }), TenantId, ActorId, LegalEntityId));
    }

    [Theory]
    [InlineData(JwtRegisteredClaimNames.Sub)]
    [InlineData("scope")]
    [InlineData("tenant_id")]
    [InlineData("actor_id")]
    [InlineData("legal_entity_id")]
    [InlineData(JwtRegisteredClaimNames.Jti)]
    [InlineData(JwtRegisteredClaimNames.Iat)]
    public void Missing_security_claim_is_rejected(string type) =>
        Assert.False(CreateValidator().IsAuthorized(
            CreateToken(omitClaim: type), TenantId, ActorId, LegalEntityId));

    [Fact]
    public void Expected_plus_extra_audience_is_rejected() =>
        Assert.False(CreateValidator().IsAuthorized(
            CreateToken(extraClaims: new[] { new Claim(JwtRegisteredClaimNames.Aud, "other-audience") }),
            TenantId, ActorId, LegalEntityId));

    [Fact]
    public void Missing_audience_is_rejected() =>
        Assert.False(CreateValidator().IsAuthorized(
            CreateToken(audience: null), TenantId, ActorId, LegalEntityId));

    [Fact]
    public void Future_iat_is_rejected_even_when_nbf_is_current()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Assert.False(CreateValidator(clockSkewSeconds: 5).IsAuthorized(
            CreateToken(iat: now + 20, nbf: now, exp: now + 40), TenantId, ActorId, LegalEntityId));
    }

    [Fact]
    public void Iat_and_nbf_must_have_the_same_numeric_date()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Assert.False(CreateValidator(clockSkewSeconds: 5).IsAuthorized(
            CreateToken(iat: now - 1, nbf: now, exp: now + 20), TenantId, ActorId, LegalEntityId));
    }

    [Fact]
    public void Future_nbf_outside_skew_is_rejected()
    {
        // Offsets lie beyond JwtValidationDefaults.ClockSkew (30 s), the one clock the validator now uses (Q117, BL-296).
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Assert.False(CreateValidator(clockSkewSeconds: 5).IsAuthorized(
            CreateToken(iat: now + 40, nbf: now + 40, exp: now + 60), TenantId, ActorId, LegalEntityId));
    }

    [Fact]
    public void Expired_token_inside_declared_skew_is_accepted()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Assert.True(CreateValidator(clockSkewSeconds: 5).IsAuthorized(
            CreateToken(iat: now - 20, nbf: now - 20, exp: now - 2), TenantId, ActorId, LegalEntityId));
    }

    [Fact]
    public void Expired_token_outside_declared_skew_is_rejected()
    {
        // Expired 40 s ago: outside JwtValidationDefaults.ClockSkew (30 s) (Q117, BL-296).
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Assert.False(CreateValidator(clockSkewSeconds: 5).IsAuthorized(
            CreateToken(iat: now - 50, nbf: now - 50, exp: now - 40), TenantId, ActorId, LegalEntityId));
    }

    [Theory]
    [InlineData(JwtRegisteredClaimNames.Iat)]
    [InlineData(JwtRegisteredClaimNames.Nbf)]
    [InlineData(JwtRegisteredClaimNames.Exp)]
    public void Numeric_dates_encoded_as_json_strings_are_rejected(string stringClaim)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Assert.False(CreateValidator().IsAuthorized(
            CreateTokenWithRawNumericDates(now, stringClaim), TenantId, ActorId, LegalEntityId));
    }

    [Fact]
    public void Fractional_numeric_date_is_rejected()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Assert.False(CreateValidator().IsAuthorized(
            CreateTokenWithRawNumericDates(now, JwtRegisteredClaimNames.Iat, now + 0.5m),
            TenantId, ActorId, LegalEntityId));
    }

    [Fact]
    public void Numeric_date_outside_int64_range_is_rejected()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Assert.False(CreateValidator().IsAuthorized(
            CreateTokenWithRawNumericDates(now, JwtRegisteredClaimNames.Iat, ulong.MaxValue),
            TenantId, ActorId, LegalEntityId));
    }

    private static PlatformServiceTokenValidator CreateValidator(int clockSkewSeconds = 0)
    {
        var options = ValidOptions();
        options.ClockSkewSeconds = clockSkewSeconds;
        return new PlatformServiceTokenValidator(Options.Create(options));
    }

    private static PlatformServiceIdentityOptions ValidOptions() => new()
    {
        Enabled = true,
        Issuer = "diten-platform-service",
        Audience = "diten-mdm-reference-validation",
        CallerId = "Diten.Platform",
        KeyId = "platform-mdm-2026-09",
        Secret = Secret,
        MaximumTokenLifetimeSeconds = 60,
        ClockSkewSeconds = 0
    };

    private static string CreateTokenWithRawNumericDates(
        long issuedAt,
        string overriddenClaim,
        object? overriddenValue = null)
    {
        var numericDates = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Iat] = issuedAt,
            [JwtRegisteredClaimNames.Nbf] = issuedAt,
            [JwtRegisteredClaimNames.Exp] = issuedAt + 30
        };
        numericDates[overriddenClaim] = overriddenValue ?? numericDates[overriddenClaim].ToString()!;

        var headerBytes = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["alg"] = SecurityAlgorithms.HmacSha256,
            ["typ"] = "JWT",
            ["kid"] = "platform-mdm-2026-09"
        });
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Iss] = "diten-platform-service",
            [JwtRegisteredClaimNames.Aud] = "diten-mdm-reference-validation",
            [JwtRegisteredClaimNames.Sub] = "Diten.Platform",
            ["scope"] = "mdm.legal-entities.reference.validate",
            ["tenant_id"] = TenantId.ToString("D"),
            ["actor_id"] = ActorId.ToString("D"),
            ["legal_entity_id"] = LegalEntityId.ToString("D"),
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("D"),
            [JwtRegisteredClaimNames.Iat] = numericDates[JwtRegisteredClaimNames.Iat],
            [JwtRegisteredClaimNames.Nbf] = numericDates[JwtRegisteredClaimNames.Nbf],
            [JwtRegisteredClaimNames.Exp] = numericDates[JwtRegisteredClaimNames.Exp]
        });
        var unsignedToken = $"{Base64UrlEncoder.Encode(headerBytes)}.{Base64UrlEncoder.Encode(payloadBytes)}";
        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.ASCII.GetBytes(unsignedToken));
        return $"{unsignedToken}.{Base64UrlEncoder.Encode(signature)}";
    }

    private static string CreateToken(
        string caller = "Diten.Platform",
        string scope = "mdm.legal-entities.reference.validate",
        string secret = Secret,
        string? audience = "diten-mdm-reference-validation",
        string? omitClaim = null,
        IEnumerable<Claim>? extraClaims = null,
        long? iat = null,
        long? nbf = null,
        long? exp = null)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var issuedAt = iat ?? now;
        var notBefore = nbf ?? issuedAt;
        var expiresAt = exp ?? issuedAt + 30;
        var claims = new List<Claim>();
        Add(JwtRegisteredClaimNames.Sub, caller);
        Add("scope", scope);
        Add("tenant_id", TenantId.ToString("D"));
        Add("actor_id", ActorId.ToString("D"));
        Add("legal_entity_id", LegalEntityId.ToString("D"));
        Add(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D"));
        Add(JwtRegisteredClaimNames.Iat, issuedAt.ToString(), ClaimValueTypes.Integer64);
        if (extraClaims is not null) claims.AddRange(extraClaims);

        var token = new JwtSecurityToken(
            issuer: "diten-platform-service",
            audience: audience,
            claims: claims,
            notBefore: DateTimeOffset.FromUnixTimeSeconds(notBefore).UtcDateTime,
            expires: DateTimeOffset.FromUnixTimeSeconds(expiresAt).UtcDateTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                SecurityAlgorithms.HmacSha256));
        token.Header["kid"] = "platform-mdm-2026-09";
        return new JwtSecurityTokenHandler().WriteToken(token);

        void Add(string type, string value, string valueType = ClaimValueTypes.String)
        {
            if (!string.Equals(omitClaim, type, StringComparison.Ordinal))
                claims.Add(new Claim(type, value, valueType));
        }
    }
}
