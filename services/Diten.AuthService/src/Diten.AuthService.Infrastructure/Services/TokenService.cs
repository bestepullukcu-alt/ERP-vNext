using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Settings;
using Diten.BuildingBlocks.Security.Secrets;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Diten.AuthService.Infrastructure.Services;

public sealed class TokenService : ITokenService
{
    private readonly JwtSettings _jwtSettings;
    private readonly ISecretRotationResolver _rotationResolver;

    public TokenService(IOptions<JwtSettings> jwtSettings, ISecretRotationResolver rotationResolver)
    {
        _jwtSettings = jwtSettings.Value;
        _rotationResolver = rotationResolver;
    }

    public string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions)
    {
        return GenerateAccessToken(user, roles, permissions, _jwtSettings.AccessTokenExpirationMinutes);
    }

    public string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.GivenName, user.FirstName),
            new(JwtRegisteredClaimNames.FamilyName, user.LastName),
            new("actor_type", "tenant_user"),
            new("tenant_id", user.TenantId.ToString()),
            // FIX-TENANT-MUSTCHANGEPW — mirror the platform token: tenant token carries the forced-password-change
            // flag derived from the user's real state, so the shell guard can require a first-login change.
            new("pwd_change_required", user.MustChangePassword ? "true" : "false")
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        foreach (var permission in permissions)
        {
            claims.Add(new Claim("permission", permission));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GeneratePlatformAccessToken(
        Guid userId,
        string email,
        string? firstName,
        string? lastName,
        Guid tenantId,
        string actorType,
        IEnumerable<string> roles,
        IEnumerable<string> permissions)
    {
        return GeneratePlatformAccessToken(userId, email, firstName, lastName, tenantId, actorType, roles, permissions, _jwtSettings.AccessTokenExpirationMinutes);
    }

    public string GeneratePlatformAccessToken(
        Guid userId,
        string email,
        string? firstName,
        string? lastName,
        Guid tenantId,
        string actorType,
        IEnumerable<string> roles,
        IEnumerable<string> permissions,
        int expiresInMinutes)
    {
        return GeneratePlatformAccessToken(userId, email, firstName, lastName, tenantId, actorType, roles, permissions, expiresInMinutes, false);
    }

    public string GeneratePlatformAccessToken(
        Guid userId,
        string email,
        string? firstName,
        string? lastName,
        Guid tenantId,
        string actorType,
        IEnumerable<string> roles,
        IEnumerable<string> permissions,
        int expiresInMinutes,
        bool requiresPasswordChange)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new("actor_type", actorType),
            new("tenant_id", tenantId.ToString()),
            new("pwd_change_required", requiresPasswordChange ? "true" : "false")
        };

        if (!string.IsNullOrWhiteSpace(firstName))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.GivenName, firstName));
        }

        if (!string.IsNullOrWhiteSpace(lastName))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.FamilyName, lastName));
        }

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        foreach (var permission in permissions)
        {
            claims.Add(new Claim("permission", permission));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
    {
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidateIssuerSigningKey = true,
            ValidAudience = _jwtSettings.Audience,
            ValidIssuer = _jwtSettings.Issuer,
            IssuerSigningKeys = _rotationResolver.GetValidationKeys(),
            // BL-296: no ClockSkew here, and that is deliberate. ValidateLifetime is false — this decodes a
            // token we already KNOW is expired, during refresh. The library never consults ClockSkew when
            // lifetime validation is off, so any value written here would be decoration. The one tolerance
            // that does decide whether a request is accepted lives in JwtValidationDefaults.ClockSkew.
            ValidateLifetime = false
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);

        if (securityToken is not JwtSecurityToken jwtSecurityToken ||
            !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
        {
            throw new SecurityTokenException("Invalid token");
        }

        return principal;
    }

    public ClaimsPrincipal GetPrincipalFromCurrentToken(string token, DateTimeOffset requiredValidThrough)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var segments = token.Split('.');
        if (segments.Length != 3) throw new SecurityTokenException("TOKEN_FORMAT_INVALID");
        using var rawPayload = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(segments[1]));
        if (rawPayload.RootElement.ValueKind != JsonValueKind.Object
            || rawPayload.RootElement.EnumerateObject().GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() != 1))
            throw new SecurityTokenException("TOKEN_DUPLICATE_JSON_CLAIM");
        var untrusted = handler.ReadJwtToken(token);
        foreach (var key in new[] { "exp", "iat", "nbf" })
        {
            var count = untrusted.Claims.Count(c => c.Type == key);
            if (count > 1 || key == "exp" && count != 1)
                throw new SecurityTokenException("TOKEN_TEMPORAL_CLAIM_AMBIGUOUS");
        }
        var principal = handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true, ValidateIssuerSigningKey = true,
            ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
            ValidIssuer = _jwtSettings.Issuer, ValidAudience = _jwtSettings.Audience,
            IssuerSigningKeys = _rotationResolver.GetValidationKeys(),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            ClockSkew = JwtValidationDefaults.ClockSkew
        }, out var validated);
        if (validated is not JwtSecurityToken jwt || jwt.ValidTo <= requiredValidThrough.UtcDateTime)
            throw new SecurityTokenException("OPERATOR_TOKEN_DEADLINE_NOT_COVERED");
        // Both existing platform login and platform refresh issue a fixed 15-minute token.
        // Legacy tokens omit iat/nbf; in that case only the remaining validity bound is observable.
        if (jwt.ValidTo > DateTime.UtcNow.AddMinutes(15).Add(JwtValidationDefaults.ClockSkew))
            throw new SecurityTokenException("OPERATOR_TOKEN_NOT_SHORT_LIVED");
        foreach (var key in new[] { "iat", "nbf" })
        {
            var value = jwt.Claims.SingleOrDefault(c => c.Type == key)?.Value;
            if (value is not null && (!long.TryParse(value, out var seconds)
                || DateTimeOffset.FromUnixTimeSeconds(seconds) > DateTimeOffset.UtcNow.Add(JwtValidationDefaults.ClockSkew)
                || jwt.ValidTo - DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime > TimeSpan.FromMinutes(15)))
                throw new SecurityTokenException("OPERATOR_TOKEN_TEMPORAL_RANGE_INVALID");
        }
        return principal;
    }
}
