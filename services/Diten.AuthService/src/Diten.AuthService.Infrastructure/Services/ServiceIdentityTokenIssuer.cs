using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Diten.AuthService.Infrastructure.Services;

public sealed class ServiceIdentityTokenIssuer : IServiceIdentityTokenIssuer
{
    public const int LifetimeSeconds = 300;
    private const string RequiredServiceName = "Diten.MDM";
    private const string RequiredAudience = "TRUSTED_AUDIT_SOURCE_INGEST";
    private readonly ServiceIdentityTokenIssuerOptions _options;
    private readonly TimeProvider _timeProvider;

    public ServiceIdentityTokenIssuer(IOptions<ServiceIdentityTokenIssuerOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public ServiceIdentityTokenIssue Issue(Guid clientId, string serviceName, Guid tenantId, string audience)
    {
        if (clientId == Guid.Empty || tenantId == Guid.Empty
            || !string.Equals(serviceName, RequiredServiceName, StringComparison.Ordinal)
            || !string.Equals(audience, RequiredAudience, StringComparison.Ordinal))
            throw new InvalidOperationException("Service identity token subject is outside the bounded contract.");

        if (_options.TokenLifetimeSeconds != LifetimeSeconds || !IsExactIdentifier(_options.Issuer, 256)
            || !IsExactIdentifier(_options.ActiveKeyId) || string.IsNullOrWhiteSpace(_options.ActivePrivateKeyPem))
            throw new InvalidOperationException("Service identity token issuer configuration is invalid.");

        using var rsa = RSA.Create();
        try { rsa.ImportFromPem(_options.ActivePrivateKeyPem); }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            throw new InvalidOperationException("Service identity signing key is invalid.", ex);
        }
        if (rsa.KeySize < 2048) throw new InvalidOperationException("Service identity signing key must be at least 2048 bits.");

        var now = _timeProvider.GetUtcNow();
        var expires = now.AddSeconds(LifetimeSeconds);
        var key = new RsaSecurityKey(rsa.ExportParameters(true)) { KeyId = _options.ActiveKeyId };
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, clientId.ToString("D")),
            new Claim("actor_type", "service"),
            new Claim("service_name", serviceName),
            new Claim("tenant_id", tenantId.ToString("D")),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D")),
            new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };
        var token = new JwtSecurityToken(
            _options.Issuer, audience, claims, now.UtcDateTime, expires.UtcDateTime,
            new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
        var encoded = new JwtSecurityTokenHandler().WriteToken(token);
        return new ServiceIdentityTokenIssue(encoded, expires, LifetimeSeconds);
    }

    private static bool IsExactIdentifier(string value, int maxLength = 128) =>
        value.Length is > 0 && value.Length <= maxLength
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl);
}
