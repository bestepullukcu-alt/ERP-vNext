using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Auth.Commands;
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

        return Response<AuthResponse>.Success(await GenerateNewTokens(user, existingToken, actorType, request, ct));
    }

    private async Task<AuthResponse> GenerateNewTokens(User user, RefreshToken oldToken, string? actorType, RefreshTokenCommand request, CancellationToken ct)
    {
        var tokenTenantId = oldToken.TenantId;
        var roles = await _userRoleRepository.GetRolesByUserAsync(user.Id, tokenTenantId, ct);
        var roleIds = await ResolveRoleIdsAsync(roles, tokenTenantId, ct);
        var permissions = await _rolePermissionRepository.GetPermissionsByRolesAsync(roleIds, tokenTenantId, ct);

        var isPlatformActor = IsPlatformActor(actorType);

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
            : _tokenService.GenerateAccessToken(user, roles, permissions, tenantSettings!.SessionTimeoutMinutes);
        var newRefreshTokenStr = _tokenService.GenerateRefreshToken();
        var newRefreshTokenHash = _refreshTokenHasher.Hash(newRefreshTokenStr);

        oldToken.Revoke(newRefreshTokenHash, request.RequestIp, "rotated");
        await _refreshTokenRepository.UpdateAsync(oldToken, ct);

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
        await _refreshTokenRepository.CreateAsync(newRefreshToken, ct);

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
            RequiresPasswordChange: isPlatformActor && user.MustChangePassword
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
