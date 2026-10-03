using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.BuildingBlocks.Security.Secrets;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Diten.MdmService.Api.Security;

public interface IPlatformServiceTokenValidator
{
    bool IsAuthorized(string token, Guid tenantId, Guid actorId, Guid legalEntityId);
}

public sealed class PlatformServiceTokenValidator : IPlatformServiceTokenValidator
{
    private readonly PlatformServiceIdentityOptions _options;

    public PlatformServiceTokenValidator(IOptions<PlatformServiceIdentityOptions> options)
    {
        _options = options.Value;
    }

    public bool IsAuthorized(string token, Guid tenantId, Guid actorId, Guid legalEntityId)
    {
        if (!_options.Enabled
            || tenantId == Guid.Empty
            || actorId == Guid.Empty
            || legalEntityId == Guid.Empty
            || string.IsNullOrWhiteSpace(token)
            || string.IsNullOrWhiteSpace(_options.Issuer)
            || string.IsNullOrWhiteSpace(_options.Audience)
            || string.IsNullOrWhiteSpace(_options.CallerId)
            || string.IsNullOrWhiteSpace(_options.KeyId)
            || string.IsNullOrWhiteSpace(_options.Secret)
            || Encoding.UTF8.GetByteCount(_options.Secret) < 32
            || _options.MaximumTokenLifetimeSeconds is < 5 or > 60
            || _options.ClockSkewSeconds is < 0 or > 10)
        {
            return false;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            _ = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _options.Issuer,
                ValidateAudience = true,
                ValidAudience = _options.Audience,
                ValidateLifetime = true,
                ClockSkew = JwtValidationDefaults.ClockSkew,
                RequireExpirationTime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret))
            }, out var validatedToken);

            if (validatedToken is not JwtSecurityToken jwt
                || !string.Equals(jwt.Header.Alg, SecurityAlgorithms.HmacSha256, StringComparison.Ordinal)
                || !string.Equals(jwt.Header.Kid, _options.KeyId, StringComparison.Ordinal)
                || !HasExactlyOneAudience(jwt, _options.Audience)
                || !HasExactlyOneClaim(jwt, JwtRegisteredClaimNames.Sub, _options.CallerId)
                || !HasExactlyOneClaim(jwt, "scope", PlatformServiceIdentityOptions.RequiredScope)
                || !HasExactlyOneClaim(jwt, "tenant_id", tenantId.ToString("D"))
                || !HasExactlyOneClaim(jwt, "actor_id", actorId.ToString("D"))
                || !HasExactlyOneClaim(jwt, "legal_entity_id", legalEntityId.ToString("D"))
                || !HasExactlyOneNonEmptyClaim(jwt, JwtRegisteredClaimNames.Jti)
                || !TryGetExactlyOneNumericDate(jwt, JwtRegisteredClaimNames.Iat, out var issuedAt)
                || !TryGetExactlyOneNumericDate(jwt, JwtRegisteredClaimNames.Nbf, out var notBefore)
                || !TryGetExactlyOneNumericDate(jwt, JwtRegisteredClaimNames.Exp, out var expiresAt)
                || issuedAt != notBefore
                || issuedAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)JwtValidationDefaults.ClockSkew.TotalSeconds
                || expiresAt <= issuedAt
                || expiresAt - issuedAt > _options.MaximumTokenLifetimeSeconds)
            {
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            return false;
        }
    }

    private static bool HasExactlyOneAudience(JwtSecurityToken token, string expected)
    {
        var audiences = token.Audiences.ToArray();
        return audiences.Length == 1
               && string.Equals(audiences[0], expected, StringComparison.Ordinal);
    }

    private static bool HasExactlyOneClaim(JwtSecurityToken token, string type, string expected)
    {
        var values = token.Claims
            .Where(claim => string.Equals(claim.Type, type, StringComparison.Ordinal))
            .Select(claim => claim.Value)
            .ToArray();
        return values.Length == 1
               && string.Equals(values[0], expected, StringComparison.Ordinal);
    }

    private static bool HasExactlyOneNonEmptyClaim(JwtSecurityToken token, string type)
    {
        var values = token.Claims
            .Where(claim => string.Equals(claim.Type, type, StringComparison.Ordinal))
            .Select(claim => claim.Value)
            .ToArray();
        return values.Length == 1 && !string.IsNullOrWhiteSpace(values[0]);
    }

    private static bool TryGetExactlyOneNumericDate(
        JwtSecurityToken token,
        string type,
        out long value)
    {
        value = default;
        if (token.Claims.Count(claim => string.Equals(claim.Type, type, StringComparison.Ordinal)) != 1)
            return false;

        try
        {
            using var payload = JsonDocument.Parse(Base64UrlEncoder.Decode(token.RawPayload));
            var matchingProperties = payload.RootElement
                .EnumerateObject()
                .Where(property => string.Equals(property.Name, type, StringComparison.Ordinal))
                .ToArray();

            return matchingProperties.Length == 1
                   && matchingProperties[0].Value.ValueKind == JsonValueKind.Number
                   && matchingProperties[0].Value.TryGetInt64(out value);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
