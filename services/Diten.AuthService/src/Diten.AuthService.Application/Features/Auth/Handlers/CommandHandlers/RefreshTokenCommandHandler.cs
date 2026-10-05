using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Auth.Commands;
using Diten.AuthService.Application.Features.Auth.Services;
using Diten.AuthService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace Diten.AuthService.Application.Features.Auth.Handlers.CommandHandlers;

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Response<AuthResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly ITenantEffectivePermissionResolver _effectivePermissionResolver;
    private readonly ITokenService _tokenService;
    private readonly ITenantLoginSettingsClient _tenantLoginSettingsClient;
    private readonly IRefreshTokenHasher _refreshTokenHasher;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITenantContext _tenantContext;
    private readonly IPlatformAdministratorStatusClient _platformAdministratorStatusClient;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        IUserRepository userRepository,
        IUserRoleRepository userRoleRepository,
        IRoleRepository roleRepository,
        IRolePermissionRepository rolePermissionRepository,
        ITenantEffectivePermissionResolver effectivePermissionResolver,
        ITokenService tokenService,
        ITenantLoginSettingsClient tenantLoginSettingsClient,
        IRefreshTokenHasher refreshTokenHasher,
        IRefreshTokenRepository refreshTokenRepository,
        ITenantContext tenantContext,
        IPlatformAdministratorStatusClient platformAdministratorStatusClient,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _roleRepository = roleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _effectivePermissionResolver = effectivePermissionResolver;
        _tokenService = tokenService;
        _tenantLoginSettingsClient = tenantLoginSettingsClient;
        _refreshTokenHasher = refreshTokenHasher;
        _refreshTokenRepository = refreshTokenRepository;
        _tenantContext = tenantContext;
        _platformAdministratorStatusClient = platformAdministratorStatusClient;
        _logger = logger;
    }

    public async Task<Response<AuthResponse>> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var existingToken = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken, ct);
        if (existingToken == null) return Response<AuthResponse>.Fail("Invalid refresh token.", 401);

        if (existingToken.RevokedAt != null)
        {
            // BL-529 FIX3 — a token ended by a password change or an administrator's reset is a stale tab, not a stolen
            // token: it is refused and nothing else happens (ending every session here killed the session the owner had
            // just opened with the new password). Reuse of a "rotated" (or otherwise revoked) token is still a theft signal.
            if (SessionRevocationReasons.EndedByPasswordChange(existingToken.RevokedReason))
            {
                return Response<AuthResponse>.Fail("The session was ended. Please sign in again.", 401);
            }

            await _refreshTokenRepository.RevokeAllByUserAsync(existingToken.UserId, existingToken.TenantId, ct);
            return Response<AuthResponse>.Fail("Security violation detected. Please sign in again.", 401);
        }

        if (existingToken.IsExpired)
            return Response<AuthResponse>.Fail("Refresh token has expired.", 401);

        ClaimsPrincipal principal;
        try
        {
            principal = _tokenService.GetPrincipalFromExpiredToken(request.AccessToken);
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Token validation failed during refresh token command handler.");
            return Response<AuthResponse>.Fail("Invalid access token format or signature.", 401);
        }
        var actorType = principal.FindFirst("actor_type")?.Value;
        var subject = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? principal.FindFirst("sub")?.Value;
        var tokenTenantId = principal.FindFirst("tenant_id")?.Value;

        if (!Guid.TryParse(subject, out var accessTokenUserId) || accessTokenUserId != existingToken.UserId)
        {
            return Response<AuthResponse>.Fail("Access token user mismatch.", 401);
        }

        if (!Guid.TryParse(tokenTenantId, out var accessTokenTenantId) || accessTokenTenantId != existingToken.TenantId)
        {
            return Response<AuthResponse>.Fail("Access token tenant mismatch.", 401);
        }

        if (!string.Equals(existingToken.ActorType, actorType, StringComparison.OrdinalIgnoreCase))
        {
            return Response<AuthResponse>.Fail("Access token actor mismatch.", 401);
        }

        if (_tenantContext.IsResolved && _tenantContext.TenantId != existingToken.TenantId)
        {
            return Response<AuthResponse>.Fail("Request tenant mismatch.", 401);
        }

        var user = await _userRepository.GetByIdAndTenantAsync(existingToken.UserId, existingToken.TenantId, ct);
        if (user == null || !user.IsActive)
            return Response<AuthResponse>.Fail("User was not found or is inactive.", 401);

        // BL-529 FIX3 — the token's authority is the password it was opened with: changed (reset) since, or never bound
        // (minted before this rule) → refused, nothing issued.
        if (!SessionPasswordBinding.Matches(existingToken, _refreshTokenHasher, user))
        {
            return Response<AuthResponse>.Fail("The session was ended. Please sign in again.", 401);
        }

        var isPlatformActor = IsPlatformActor(actorType);
        if (isPlatformActor && !await _platformAdministratorStatusClient.IsActiveAsync(user.Email, ct))
        {
            await _refreshTokenRepository.RevokeAllByUserAsync(user.Id, existingToken.TenantId, ct);
            _logger.LogWarning(
                "Platform refresh denied by platform administrator status. UserId={UserId} Email={Email}",
                user.Id,
                user.Email);
            return Response<AuthResponse>.Fail("Platform administrator account is inactive.", 401);
        }

        if (isPlatformActor)
        {
            await _platformAdministratorStatusClient.MarkLoginAcceptedAsync(user.Email, ct);
        }

        var renewed = await GenerateNewTokens(user, existingToken, actorType, request, ct);
        return renewed is null
            ? Response<AuthResponse>.Fail("The session was ended. Please sign in again.", 401)
            : Response<AuthResponse>.Success(renewed);
    }

    // BL-529 — null when the account's password changed while this refresh ran (an administrator's reset ended every
    // session between the read above and the write below): the token just written is revoked again (IssuedSessionGuard).
    private async Task<AuthResponse?> GenerateNewTokens(User user, RefreshToken oldToken, string? actorType, RefreshTokenCommand request, CancellationToken ct)
    {
        var tokenTenantId = oldToken.TenantId;
        var roles = await _userRoleRepository.GetRolesByUserAsync(user.Id, tokenTenantId, ct);
        var roleIds = await ResolveRoleIdsAsync(roles, tokenTenantId, ct);
        var permissions = await _rolePermissionRepository.GetPermissionsByRolesAsync(roleIds, tokenTenantId, ct);

        var isPlatformActor = IsPlatformActor(actorType);
        var effectivePermissions = isPlatformActor
            ? permissions
            : await _effectivePermissionResolver.ResolveAsync(tokenTenantId, permissions, ct);

        var tenantSettings = isPlatformActor ? null : await _tenantLoginSettingsClient.GetAsync(tokenTenantId, ct);
        var accessToken = isPlatformActor
            ? _tokenService.GeneratePlatformAccessToken(
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                tokenTenantId,
                actorType ?? "platform_admin",
                roles,
                permissions,
                15,
                user.MustChangePassword)
            : _tokenService.GenerateAccessToken(user, roles, effectivePermissions, tenantSettings!.SessionTimeoutMinutes);
        var newRefreshTokenStr = _tokenService.GenerateRefreshToken();
        var newRefreshTokenHash = _refreshTokenHasher.Hash(newRefreshTokenStr);

        // BL-529 FIX2 — the rotation is conditional on the presented token still being live. A whole-document write here
        // un-revoked a token an administrator's reset had just ended (and erased its "admin-reset" reason); a refresh that
        // read the token before that sweep then lived on. If it was revoked meanwhile, nothing is issued.
        if (!await _refreshTokenRepository.TryRotateAsync(oldToken.Id, newRefreshTokenHash, request.RequestIp, ct))
        {
            _logger.LogWarning("Refresh refused: the token was revoked while the refresh ran. UserId={UserId}", user.Id);
            return null;
        }

        var newRefreshToken = new RefreshToken(
            user.Id,
            newRefreshTokenHash,
            DateTime.UtcNow.AddDays(isPlatformActor
                ? Math.Max(1, (oldToken.ExpiresAt - DateTime.UtcNow).TotalDays)
                : tenantSettings!.RefreshTokenLifetimeDays),
            request.RequestIp,
            tokenTenantId,
            actorType ?? "tenant_user",
            request.UserAgent,
            oldToken.SessionId,
            oldToken.DeviceId);
        newRefreshToken.BindToPassword(oldToken.PasswordFingerprint!); // carried through rotation
        await _refreshTokenRepository.CreateAsync(newRefreshToken, ct);

        if (!await IssuedSessionGuard.StillValidAsync(_userRepository, _refreshTokenRepository, user.Id, tokenTenantId, user.PasswordHash, newRefreshTokenStr, ct))
        {
            _logger.LogWarning("Refresh refused: the password changed while it ran. UserId={UserId}", user.Id);
            return null;
        }

        return new AuthResponse(
            accessToken,
            newRefreshTokenStr,
            newRefreshToken.ExpiresAt,
            new UserDto(
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.IsActive,
                roles,
                isPlatformActor ? null : user.TenantId),
            // FIX-TENANT-MUSTCHANGEPW — applies to BOTH actor types now: the tenant token (re-issued above) also
            // carries the pwd_change_required claim, so a refresh must keep surfacing the requirement until cleared.
            RequiresPasswordChange: user.MustChangePassword
        );
    }

    private static bool IsPlatformActor(string? actorType)
    {
        return string.Equals(actorType, "platform_admin", StringComparison.OrdinalIgnoreCase)
               || string.Equals(actorType, "partner_admin", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<List<Guid>> ResolveRoleIdsAsync(IEnumerable<string> roleNames, Guid tenantId, CancellationToken ct)
    {
        var roleIds = new List<Guid>();

        foreach (var roleName in roleNames.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var role = await _roleRepository.GetByNameAndTenantAsync(roleName, tenantId, ct);
            if (role is not null)
            {
                roleIds.Add(role.Id);
            }
        }

        return roleIds;
    }
}
