using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Diten.AuthService.Application.Features.ServiceIdentityTokens;
using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Diten.AuthService.Application.Tests.ServiceIdentityTokens;

public sealed class ServiceIdentityTokenIssueTests
{
    [Fact]
    public void Issuer_emits_exact_300_second_rs256_service_token_with_kid()
    {
        using var rsa = RSA.Create(2048);
        var now = DateTimeOffset.UtcNow;
        var issuer = new ServiceIdentityTokenIssuer(Options.Create(new ServiceIdentityTokenIssuerOptions
        {
            Issuer = "https://auth.local",
            ActiveKeyId = "service-key-1",
            ActivePrivateKeyPem = rsa.ExportPkcs8PrivateKeyPem(),
            TokenLifetimeSeconds = 300
        }), new FrozenTimeProvider(now));
        var tenantId = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        var issued = issuer.Issue(clientId, "Diten.MDM", tenantId, "TRUSTED_AUDIT_SOURCE_INGEST");
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(issued.AccessToken);

        Assert.Equal(SecurityAlgorithms.RsaSha256, jwt.Header.Alg);
        Assert.Equal("service-key-1", jwt.Header.Kid);
        Assert.Equal(300, issued.ExpiresInSeconds);
        Assert.Equal(now.AddSeconds(300), issued.ExpiresAtUtc);
        Assert.Equal(clientId.ToString("D"), jwt.Subject);
        Assert.Equal("service", jwt.Claims.Single(x => x.Type == "actor_type").Value);
        Assert.Equal("Diten.MDM", jwt.Claims.Single(x => x.Type == "service_name").Value);
        Assert.Equal(tenantId.ToString("D"), jwt.Claims.Single(x => x.Type == "tenant_id").Value);
        Assert.DoesNotContain(jwt.Claims, x => x.Type is "role" or "permission" or "email");
        Assert.Equal(
            new[] { "actor_type", "aud", "exp", "iat", "iss", "jti", "nbf", "service_name", "sub", "tenant_id" },
            jwt.Claims.Select(x => x.Type).OrderBy(x => x, StringComparer.Ordinal).ToArray());
        Assert.Single(jwt.Claims, x => x.Type == JwtRegisteredClaimNames.Jti);
        Assert.Single(jwt.Claims, x => x.Type == JwtRegisteredClaimNames.Iat);
        Assert.Single(jwt.Claims, x => x.Type == JwtRegisteredClaimNames.Nbf);
        Assert.Single(jwt.Claims, x => x.Type == JwtRegisteredClaimNames.Exp);
        Assert.Equal(
            long.Parse(jwt.Claims.Single(x => x.Type == JwtRegisteredClaimNames.Iat).Value),
            long.Parse(jwt.Claims.Single(x => x.Type == JwtRegisteredClaimNames.Nbf).Value));
        Assert.Equal(300,
            long.Parse(jwt.Claims.Single(x => x.Type == JwtRegisteredClaimNames.Exp).Value)
            - long.Parse(jwt.Claims.Single(x => x.Type == JwtRegisteredClaimNames.Nbf).Value));
        var replayIndependent = new JwtSecurityTokenHandler().ReadJwtToken(
            issuer.Issue(clientId, "Diten.MDM", tenantId, "TRUSTED_AUDIT_SOURCE_INGEST").AccessToken);
        Assert.NotEqual(
            jwt.Claims.Single(x => x.Type == JwtRegisteredClaimNames.Jti).Value,
            replayIndependent.Claims.Single(x => x.Type == JwtRegisteredClaimNames.Jti).Value);

        var validationKey = new RsaSecurityKey(rsa.ExportParameters(false)) { KeyId = "service-key-1" };
        _ = new JwtSecurityTokenHandler().ValidateToken(issued.AccessToken, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://auth.local",
            ValidateAudience = true,
            ValidAudience = "TRUSTED_AUDIT_SOURCE_INGEST",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = validationKey,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
        }, out _);
    }

    [Fact]
    public void Issuer_emits_exact_workflow_audience_without_changing_claim_shape_or_lifetime()
    {
        using var rsa = RSA.Create(2048);
        var now = DateTimeOffset.UtcNow;
        var issuer = new ServiceIdentityTokenIssuer(Options.Create(new ServiceIdentityTokenIssuerOptions
        {
            Issuer = "https://auth.local",
            ActiveKeyId = "service-key-1",
            ActivePrivateKeyPem = rsa.ExportPkcs8PrivateKeyPem(),
            TokenLifetimeSeconds = 300
        }), new FrozenTimeProvider(now));

        var issued = issuer.Issue(
            Guid.NewGuid(), "Diten.MDM", Guid.NewGuid(), ServiceIdentityTokenAudiencePolicy.TrustedWorkflowConsumer);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(issued.AccessToken);

        Assert.Equal(SecurityAlgorithms.RsaSha256, jwt.Header.Alg);
        Assert.Equal(ServiceIdentityTokenAudiencePolicy.TrustedWorkflowConsumer, Assert.Single(jwt.Audiences));
        Assert.Equal(300, issued.ExpiresInSeconds);
        Assert.Equal(now.AddSeconds(300), issued.ExpiresAtUtc);
        Assert.Equal(
            new[] { "actor_type", "aud", "exp", "iat", "iss", "jti", "nbf", "service_name", "sub", "tenant_id" },
            jwt.Claims.Select(x => x.Type).OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    [Theory]
    [InlineData(1024, "key")]
    [InlineData(2048, "")]
    [InlineData(2048, " key")]
    public void Issuer_rejects_weak_key_or_invalid_kid(int keySize, string kid)
    {
        using var rsa = RSA.Create(keySize);
        var issuer = new ServiceIdentityTokenIssuer(Options.Create(new ServiceIdentityTokenIssuerOptions
        {
            Issuer = "issuer",
            ActiveKeyId = kid,
            ActivePrivateKeyPem = rsa.ExportPkcs8PrivateKeyPem(),
            TokenLifetimeSeconds = 300
        }), TimeProvider.System);

        Assert.Throws<InvalidOperationException>(() => issuer.Issue(
            Guid.NewGuid(), "Diten.MDM", Guid.NewGuid(), "TRUSTED_AUDIT_SOURCE_INGEST"));
    }

    [Theory]
    [InlineData("Other.Service", "TRUSTED_AUDIT_SOURCE_INGEST")]
    [InlineData("Other.Service", "TRUSTED_WORKFLOW_CONSUMER")]
    [InlineData("Diten.MDM", "OTHER_AUDIENCE")]
    public void Issuer_defends_the_first_bounded_pair(string serviceName, string audience)
    {
        using var rsa = RSA.Create(2048);
        var issuer = new ServiceIdentityTokenIssuer(Options.Create(new ServiceIdentityTokenIssuerOptions
        {
            Issuer = "issuer",
            ActiveKeyId = "key",
            ActivePrivateKeyPem = rsa.ExportPkcs8PrivateKeyPem(),
            TokenLifetimeSeconds = 300
        }), TimeProvider.System);
        Assert.Throws<InvalidOperationException>(() => issuer.Issue(Guid.NewGuid(), serviceName, Guid.NewGuid(), audience));
    }

    [Fact]
    public void Issuer_wraps_malformed_pem_as_unavailable_configuration()
    {
        var issuer = new ServiceIdentityTokenIssuer(Options.Create(new ServiceIdentityTokenIssuerOptions
        {
            Issuer = "issuer",
            ActiveKeyId = "key",
            ActivePrivateKeyPem = "not-a-pem",
            TokenLifetimeSeconds = 300
        }), TimeProvider.System);
        Assert.Throws<InvalidOperationException>(() => issuer.Issue(
            Guid.NewGuid(), "Diten.MDM", Guid.NewGuid(), "TRUSTED_AUDIT_SOURCE_INGEST"));
    }
}

internal sealed class FrozenTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
