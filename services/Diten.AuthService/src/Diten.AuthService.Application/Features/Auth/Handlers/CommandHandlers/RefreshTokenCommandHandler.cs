using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Auth.Commands;
using Diten.AuthService.Application.Features.Auth.Services;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace Diten.AuthService.Application.Features.Auth.Handlers.CommandHandlers;

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Response<AuthResponse>>
{
    /// <summary>BL-521 FIX5 item 12 — every platform administrator account lives in the platform system tenant: the
    /// convergence names it, never the tenant a (possibly foreign) token row carries.</summary>
    private static readonly Guid PlatformTenantId = PlatformSystemTenant.Id;

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

        // BL-521 FIX6 item 13 (A') — a TENANT session of a platform account is not refreshed (see LoginCommandHandler): the
        // tenant door no longer mints one, and one minted before it refused such accounts ends here with the generic answer of
        // an invalid refresh token. The account's PLATFORM session (platform_admin / partner_admin) is the platform door's
        // own and is untouched. Nothing is revoked: the platform sessions of the same account share this user and tenant.
        if (user.IsPlatformAccount && !IsPlatformActor(actorType))
        {
            // FIX7 item 5 - one warning (event code, door, tenant id - no e-mail, no user id); the answer is the same 401.
            TenantDoorPlatformAccountRefusal.Log(_logger, TenantDoorPlatformAccountRefusal.RefreshDoor, existingToken.TenantId);
            return Response<AuthResponse>.Fail("Invalid refresh token.", 401);
        }

        var isPlatformActor = IsPlatformActor(actorType);
        if (isPlatformActor)
        {
            // BL-521 FIX1 F1(b) — a platform actor's new token carries the actor type its Platform administrator RECORD
            // names, not the one the old token carried: a platform administrator demoted to partner becomes partner_admin
            // at its next refresh instead of keeping platform_admin forever. No active record / unreadable → refused, the
            // same direction as the activity rule this replaces.
            var record = await _platformAdministratorStatusClient.GetActiveActorTypeAsync(user.Email, ct);
            if (record.Verdict == PlatformAdministratorRecordVerdict.Unavailable)
            {
                // BL-521 FIX2 item 3 — Platform did not answer (unreachable, 5xx, unreadable). That is not a "no": this
                // refresh is refused with 503 and NOTHING is revoked — the old refresh token is not even rotated, so the
                // client retries with it.
                _logger.LogWarning(
                    "Platform refresh refused: platform administrator status unavailable. UserId={UserId}",
                    user.Id);
                return Response<AuthResponse>.Fail(PlatformAdministratorRecordAnswer.UnavailableMessage, 503);
            }

            if (record.Verdict != PlatformAdministratorRecordVerdict.Confirmed)
            {
                await _refreshTokenRepository.RevokeAllByUserAsync(user.Id, existingToken.TenantId, ct);
                _logger.LogWarning(
                    "Platform refresh denied by platform administrator status. UserId={UserId} Email={Email}",
                    user.Id,
                    user.Email);
                return Response<AuthResponse>.Fail("Platform administrator account is inactive.", 401);
            }

            var recordActorType = record.ActorType!;

            // BL-521 FIX7 item 1 - Auth's copy of the actor type follows the record at every platform mint (see PlatformLoginCommandHandler).
            await PlatformAccountDiscriminator.EnsureAsync(_userRepository, user, recordActorType, _logger, ct);

            if (!string.Equals(recordActorType, actorType, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "Platform refresh re-issued with the administrator record's actor type. UserId={UserId} From={From} To={To}",
                    user.Id,
                    actorType,
                    recordActorType);
            }

            actorType = recordActorType;

            // BL-521 FIX4 item 2 — the refreshed token's roles follow the RECORD: Auth's copy is converged to the roles
            // Platform's answer names before the token is minted from it. An older Platform that sends none converges nothing.
            // FIX5 item 3 — no provisioning default at mint; item 12 — the platform tenant, by constant.
            if (record.Roles is not null)
            {
                await PlatformAdministratorRoleConvergence.ConvergeAtMintAsync(_userRoleRepository, _roleRepository, user.Id, PlatformTenantId, record.Roles, ct);
            }

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
