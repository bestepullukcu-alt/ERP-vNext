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
    private static readonly Guid PlatformTenant = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly string _issuer;
    private readonly string _audience;
    private readonly ServiceClientOperationalProvisioningOptions _options;
    private readonly ISecretRotationResolver _keys;
    private readonly IUserRepository _users;
    private readonly IUserRoleRepository _userRoles;
    private readonly IRoleRepository _roles;
    private readonly IRolePermissionRepository _grants;
    private readonly IPermissionRepository _permissions;
    private readonly TimeProvider _clock;
    private int _attempted;

    public ServiceClientOperationalActorAuthorizer(IOptions<JwtSettings> jwtSettings,
        IOptions<ServiceClientOperationalProvisioningOptions> options, ISecretRotationResolver secretRotationResolver,
        IUserRepository userRepository, IUserRoleRepository userRoleRepository, IRoleRepository roleRepository,
        IRolePermissionRepository rolePermissionRepository, IPermissionRepository permissionRepository,
        TimeProvider timeProvider)
    {
        _issuer = jwtSettings.Value.Issuer;
        _audience = jwtSettings.Value.Audience;
        _options = options.Value.Snapshot();
        _keys = secretRotationResolver;
        _users = userRepository;
        _userRoles = userRoleRepository;
        _roles = roleRepository;
        _grants = rolePermissionRepository;
        _permissions = permissionRepository;
        _clock = timeProvider;
    }

    public async Task<ServiceClientOperationalActor> AuthorizeAsync(string accessToken, string operationalMarker,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.Exchange(ref _attempted, 1) != 0) throw Denied();
        ValidateMarker(operationalMarker);
        if (string.IsNullOrWhiteSpace(accessToken)
            || accessToken.Length > ServiceClientOperationalProvisioningRunner.MaximumAccessTokenCharacters
            || string.IsNullOrWhiteSpace(_issuer) || string.IsNullOrWhiteSpace(_audience)
            || _options.MaximumOperatorTokenAgeSeconds is < 1 or > 300
            || _options.OperationTimeoutSeconds is < 1 or > 120) throw Denied();

        ClaimsPrincipal principal;
        JwtSecurityToken jwt;
        var now = _clock.GetUtcNow();
        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            principal = handler.ValidateToken(accessToken, new TokenValidationParameters
            {
                ValidateIssuer = true, ValidateAudience = true, ValidateIssuerSigningKey = true,
                ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
                ValidIssuer = _issuer, ValidAudience = _audience,
                IssuerSigningKeys = _keys.GetValidationKeys(),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ClockSkew = JwtValidationDefaults.ClockSkew,
                LifetimeValidator = (notBefore, expires, _, parameters) => expires.HasValue
                    && (!notBefore.HasValue || notBefore.Value <= now.UtcDateTime + parameters.ClockSkew)
                    && expires.Value > now.UtcDateTime
                    && (!notBefore.HasValue || notBefore.Value <= expires.Value)
            }, out var token);
            jwt = token as JwtSecurityToken ?? throw Denied();
            if (!string.Equals(jwt.Header.Alg, SecurityAlgorithms.HmacSha256, StringComparison.Ordinal)) throw Denied();
        }
        catch (Exception exception) when (exception is SecurityTokenException or ArgumentException)
        { throw Denied(); }

        var subject = RequiredGuid(principal, JwtRegisteredClaimNames.Sub);
        var tenant = RequiredGuid(principal, "tenant_id");
        ValidateGuidAlias(principal, ClaimTypes.NameIdentifier, subject);
        ValidateGuidAlias(principal, "tenantId", tenant);
        if (tenant != PlatformTenant || !ExactClaim(principal, "actor_type", "platform_admin")
            || !ExactClaim(principal, "pwd_change_required", "false")
            || !principal.Claims.Any(claim => claim.Type == "permission"
                && string.Equals(claim.Value, RequiredPermission, StringComparison.Ordinal))) throw Denied();
        var issuedAt = ValidateFreshness(principal, now);

        var user = await _users.GetByIdAndTenantAsync(subject, tenant, cancellationToken);
        if (user is null || user.Id != subject || user.TenantId != tenant || user.IsDeleted || !user.IsActive
            || !user.EmailConfirmed || user.MustChangePassword
            || !string.Equals(user.PlatformActorType, "platform_admin", StringComparison.Ordinal)
            || user.PasswordChangedAt is { } passwordChanged && passwordChanged.ToUniversalTime() > issuedAt.UtcDateTime)
            throw Denied();
        var permission = await _permissions.GetByKeyAsync(RequiredPermission, cancellationToken);
        if (permission is null || permission.IsDeleted || permission.Scope != PermissionScope.PlatformAdmin
            || !string.Equals(permission.Key, RequiredPermission, StringComparison.Ordinal)) throw Denied();

        var roleNames = (await _userRoles.GetRolesByUserAsync(subject, tenant, cancellationToken))
            .Distinct(StringComparer.Ordinal).ToArray();
        var authorized = false;
        foreach (var roleName in roleNames)
        {
            var role = await _roles.GetByNameAndTenantAsync(roleName, tenant, cancellationToken);
            if (role is null || role.TenantId != tenant || role.IsDeleted
                || !string.Equals(role.Name, roleName, StringComparison.Ordinal)
                || !await _userRoles.ExistsAsync(subject, role.Id, tenant, cancellationToken)) continue;
            var roleGrants = await _grants.GetByRoleAsync(role.Id, tenant, cancellationToken);
            if (roleGrants.Any(grant => !grant.IsDeleted && grant.TenantId == tenant && grant.RoleId == role.Id
                && grant.PermissionId == permission.Id)) authorized = true;
        }
        if (!authorized) throw Denied();
        // Recheck bounded temporal eligibility after persisted reads; no claim of an atomic global revocation fence.
        _ = ValidateFreshness(principal, _clock.GetUtcNow());
        return new ServiceClientOperationalActor(subject, tenant, "platform_admin");
    }

    private DateTimeOffset ValidateFreshness(ClaimsPrincipal principal, DateTimeOffset now)
    {
        var expires = ReadTime(principal, JwtRegisteredClaimNames.Exp, required: true)!.Value;
        var issued = ReadTime(principal, JwtRegisteredClaimNames.Iat, required: false);
        var notBefore = ReadTime(principal, JwtRegisteredClaimNames.Nbf, required: false);
        // Current PlatformLogin, PlatformAuth and platform refresh issuers all use an exact 15-minute lifetime.
        // Older tokens omit iat/nbf: this is a conservative inferred bound under that inspected issuer contract,
        // not an observed issuance timestamp and not the independently configurable tenant-token lifetime.
        var issuance = issued ?? notBefore ?? expires.AddMinutes(-15);
        if (issuance > now || now - issuance > TimeSpan.FromSeconds(_options.MaximumOperatorTokenAgeSeconds)
            || expires <= now.AddSeconds(_options.OperationTimeoutSeconds)
            || expires <= issuance || expires - issuance > TimeSpan.FromMinutes(15)
            || notBefore is { } nbf && (nbf > now || nbf < issuance || nbf >= expires)) throw Denied();
        return issuance;
    }

    private void ValidateMarker(string marker)
    {
        if (string.IsNullOrEmpty(marker) || marker.Length > 1024 || marker.Any(char.IsControl)
            || _options.OperationalMarkerSha256.Length != 64) throw Denied();
        byte[] expected;
        try { expected = Convert.FromHexString(_options.OperationalMarkerSha256); }
        catch (FormatException) { throw Denied(); }
        var encoded = Encoding.UTF8.GetBytes(marker);
        var actual = SHA256.HashData(encoded);
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(actual, expected)) throw Denied();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encoded);
            CryptographicOperations.ZeroMemory(actual);
            CryptographicOperations.ZeroMemory(expected);
        }
    }

    private static DateTimeOffset? ReadTime(ClaimsPrincipal principal, string type, bool required)
    {
        var values = principal.FindAll(type).Select(claim => claim.Value).ToArray();
        if (values.Length == 0 && !required) return null;
        if (values.Length != 1 || !long.TryParse(values[0], System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var seconds)) throw Denied();
        try { return DateTimeOffset.FromUnixTimeSeconds(seconds); }
        catch (ArgumentOutOfRangeException) { throw Denied(); }
    }
    private static Guid RequiredGuid(ClaimsPrincipal principal, string type)
    {
        var values = principal.FindAll(type).Select(claim => claim.Value).ToArray();
        return values.Length == 1 && Guid.TryParseExact(values[0], "D", out var id) && id != Guid.Empty
            && values[0] == id.ToString("D") ? id : throw Denied();
    }
    private static void ValidateGuidAlias(ClaimsPrincipal principal, string type, Guid expected)
    {
        if (principal.HasClaim(claim => claim.Type == type) && RequiredGuid(principal, type) != expected) throw Denied();
    }
    private static bool ExactClaim(ClaimsPrincipal principal, string type, string expected)
    {
        var values = principal.FindAll(type).Select(claim => claim.Value).ToArray();
        return values.Length == 1 && string.Equals(values[0], expected, StringComparison.Ordinal);
    }
    private static UnauthorizedAccessException Denied() => new("Operational actor authorization failed.");
}
