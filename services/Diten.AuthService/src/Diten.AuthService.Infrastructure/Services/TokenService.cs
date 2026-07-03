using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
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
            ValidateLifetime = false,
            ClockSkew = TimeSpan.Zero
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
}
