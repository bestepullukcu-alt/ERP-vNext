using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Diten.AuthService.Api.Configuration;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Settings;
using Diten.BuildingBlocks.Security.Secrets;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Diten.AuthService.Api.Services.ServiceIdentityTokens;

public sealed class ServiceClientOperationalActorAuthorizer : IServiceClientOperationalActorAuthorizer
{
    public const string RequiredPermission = DefaultRolePermissionTemplate.ServiceClientProvisionPermission;
    private const string RequiredActorType = "platform_admin";
    private static readonly Guid PlatformSystemTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly JwtSettings _jwtSettings;
    private readonly ServiceClientOperationalProvisioningOptions _options;
    private readonly ISecretRotationResolver _secretRotationResolver;
    private readonly IUserRepository _userRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly TimeProvider _timeProvider;

    public ServiceClientOperationalActorAuthorizer(
        IOptions<JwtSettings> jwtSettings,
        IOptions<ServiceClientOperationalProvisioningOptions> options,
        ISecretRotationResolver secretRotationResolver,
        IUserRepository userRepository,
        IUserRoleRepository userRoleRepository,
        IRoleRepository roleRepository,
        IRolePermissionRepository rolePermissionRepository,
        IPermissionRepository permissionRepository,
        TimeProvider timeProvider)
    {
        _jwtSettings = jwtSettings.Value;
        _options = options.Value;
        _secretRotationResolver = secretRotationResolver;
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _roleRepository = roleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _permissionRepository = permissionRepository;
        _timeProvider = timeProvider;
    }

    public async Task<ServiceClientOperationalActor> AuthorizeAsync(
        string accessToken,
        string operationalMarker,
        CancellationToken cancellationToken)
    {
        ValidateMarker(operationalMarker);

        if (string.IsNullOrWhiteSpace(accessToken)
            || accessToken.Length > ServiceClientOperationalProvisioningRunner.MaximumAccessTokenCharacters)
        {
            throw new UnauthorizedAccessException("Operational actor token is invalid.");
        }

        ClaimsPrincipal principal;
        SecurityToken validatedToken;
        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            principal = handler.ValidateToken(
                accessToken,
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = _jwtSettings.Issuer,
                    ValidAudience = _jwtSettings.Audience,
                    IssuerSigningKeys = _secretRotationResolver.GetValidationKeys(),
                    ClockSkew = JwtValidationDefaults.ClockSkew
                },
                out validatedToken);
        }
        catch (Exception exception) when (exception is SecurityTokenException or ArgumentException)
        {
            throw new UnauthorizedAccessException("Operational actor token is invalid.");
        }

        if (validatedToken is not JwtSecurityToken jwt
            || !string.Equals(jwt.Header.Alg, SecurityAlgorithms.HmacSha256, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Operational actor token is invalid.");
        }

        var now = _timeProvider.GetUtcNow();
        var issuedAt = ReadIssuedAt(principal, jwt, _jwtSettings.AccessTokenExpirationMinutes);
        if (issuedAt > now || now - issuedAt > TimeSpan.FromSeconds(_options.MaximumOperatorTokenAgeSeconds))
        {
            throw new UnauthorizedAccessException("Operational actor token is not fresh.");
        }

        var userId = ReadRequiredGuid(principal, JwtRegisteredClaimNames.Sub);
        var tenantId = ReadRequiredGuid(principal, "tenant_id");
        if (tenantId != PlatformSystemTenantId
            || !HasExactClaim(principal, "actor_type", RequiredActorType)
            || !HasExactClaim(principal, "pwd_change_required", "false")
            || !principal.Claims.Any(c => c.Type == "permission"
                                          && string.Equals(c.Value, RequiredPermission, StringComparison.Ordinal)))
        {
            throw new UnauthorizedAccessException("Operational actor is not authorized.");
        }

        var user = await _userRepository.GetByIdAndTenantAsync(userId, tenantId, cancellationToken);
        if (user is null
            || user.IsDeleted
            || !user.IsActive
            || user.MustChangePassword
            || !string.Equals(user.PlatformActorType, RequiredActorType, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Operational actor is not active.");
        }

        var roleNames = (await _userRoleRepository.GetRolesByUserAsync(userId, tenantId, cancellationToken))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var roleIds = new List<Guid>(roleNames.Length);
        foreach (var roleName in roleNames)
        {
            var role = await _roleRepository.GetByNameAndTenantAsync(roleName, tenantId, cancellationToken);
            if (role is not null && !role.IsDeleted)
            {
                roleIds.Add(role.Id);
            }
        }

        var persistedPermissions = await _rolePermissionRepository.GetPermissionsByRolesAsync(
            roleIds,
            tenantId,
            cancellationToken);
        if (!persistedPermissions.Contains(RequiredPermission, StringComparer.Ordinal))
        {
            throw new UnauthorizedAccessException("Operational actor permission is not active.");
        }

        var permission = await _permissionRepository.GetByKeyAsync(RequiredPermission, cancellationToken);
        if (permission is null
            || permission.IsDeleted
            || permission.Scope != PermissionScope.PlatformAdmin)
        {
            throw new UnauthorizedAccessException("Operational actor permission is inconsistent.");
        }

        return new ServiceClientOperationalActor(userId, tenantId, RequiredActorType);
    }

    private void ValidateMarker(string marker)
    {
        if (string.IsNullOrEmpty(marker) || marker.Length > 1024)
        {
            throw new UnauthorizedAccessException("Operational marker is invalid.");
        }

        byte[] expected;
        try
        {
            expected = Convert.FromHexString(_options.OperationalMarkerSha256);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Operational marker hash configuration is invalid.");
        }

        if (expected.Length != SHA256.HashSizeInBytes)
        {
            throw new InvalidOperationException("Operational marker hash configuration is invalid.");
        }

        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(marker));
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
        {
            throw new UnauthorizedAccessException("Operational marker is invalid.");
        }
    }

    private static DateTimeOffset ReadIssuedAt(
        ClaimsPrincipal principal,
        JwtSecurityToken token,
        int configuredLifetimeMinutes)
    {
        var value = principal.FindFirst(JwtRegisteredClaimNames.Iat)?.Value;
        if (long.TryParse(value, out var seconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }

        // Current human-token issuance predates an explicit iat claim. Its signed nbf is the issuance boundary;
        // accept that exact temporal claim rather than weakening freshness or changing the protected TokenService.
        if (token.Payload.NotBefore is { } notBefore)
        {
            return DateTimeOffset.FromUnixTimeSeconds(notBefore);
        }

        // The current platform token has a signed exp but no iat/nbf. Its configured fixed lifetime lets us derive
        // the issuance boundary without trusting an unsigned process value. A later token format may supply iat.
        return token.Payload.Expiration is { } expires && configuredLifetimeMinutes is > 0 and <= 1440
            ? DateTimeOffset.FromUnixTimeSeconds(expires).AddMinutes(-configuredLifetimeMinutes)
            : throw new UnauthorizedAccessException("Operational actor token is invalid.");
    }

    private static Guid ReadRequiredGuid(ClaimsPrincipal principal, string claimType)
    {
        var values = principal.Claims.Where(c => c.Type == claimType).Select(c => c.Value).ToArray();
        return values.Length == 1 && Guid.TryParse(values[0], out var value) && value != Guid.Empty
            ? value
            : throw new UnauthorizedAccessException("Operational actor token is invalid.");
    }

    private static bool HasExactClaim(ClaimsPrincipal principal, string claimType, string expected)
    {
        var values = principal.Claims.Where(c => c.Type == claimType).Select(c => c.Value).ToArray();
        return values.Length == 1 && string.Equals(values[0], expected, StringComparison.Ordinal);
    }
}
