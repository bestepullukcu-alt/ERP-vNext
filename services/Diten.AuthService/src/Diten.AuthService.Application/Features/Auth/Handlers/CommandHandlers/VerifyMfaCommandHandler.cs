using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Auth.Commands;
using Diten.AuthService.Domain.Entities;
using MediatR;

namespace Diten.AuthService.Application.Features.Auth.Handlers.CommandHandlers;

public sealed class VerifyMfaCommandHandler : IRequestHandler<VerifyMfaCommand, Response<AuthResponse>>
{
    private const string TenantActorType = "tenant_user";

    private readonly IMfaChallengeService _mfaChallengeService;
    private readonly ITenantLoginSettingsClient _tenantLoginSettingsClient;
    private readonly IUserRepository _userRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenHasher _refreshTokenHasher;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IAuthAuditService _authAuditService;

    public VerifyMfaCommandHandler(
        IMfaChallengeService mfaChallengeService,
        ITenantLoginSettingsClient tenantLoginSettingsClient,
        IUserRepository userRepository,
        IUserRoleRepository userRoleRepository,
        IRoleRepository roleRepository,
        IRolePermissionRepository rolePermissionRepository,
        ITokenService tokenService,
        IRefreshTokenHasher refreshTokenHasher,
        IRefreshTokenRepository refreshTokenRepository,
        IAuthAuditService authAuditService)
    {
        _mfaChallengeService = mfaChallengeService;
        _tenantLoginSettingsClient = tenantLoginSettingsClient;
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _roleRepository = roleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _tokenService = tokenService;
        _refreshTokenHasher = refreshTokenHasher;
        _refreshTokenRepository = refreshTokenRepository;
        _authAuditService = authAuditService;
    }

    public async Task<Response<AuthResponse>> Handle(VerifyMfaCommand request, CancellationToken ct)
    {
        MfaChallenge challenge;
        try
        {
            challenge = await _mfaChallengeService.VerifyAsync(request.ChallengeId, request.Code, ct);
        }
        catch
        {
            return Response<AuthResponse>.Fail("Invalid verification code.", 401);
        }

        var settings = await _tenantLoginSettingsClient.GetAsync(challenge.TenantId, ct);
        var user = await _userRepository.GetByIdAndTenantAsync(challenge.UserId, challenge.TenantId, ct);
        if (user is null || !user.IsActive)
        {
            return Response<AuthResponse>.Fail("Invalid verification code.", 401);
        }

        user.RecordLoginSuccess();
        await _userRepository.UpdateAsync(user, ct);

        var roles = (await _userRoleRepository.GetRolesByUserAsync(user.Id, challenge.TenantId, ct))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var roleIds = await ResolveRoleIdsAsync(roles, challenge.TenantId, ct);
        var permissions = (await _rolePermissionRepository.GetPermissionsByRolesAsync(roleIds, challenge.TenantId, ct))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var accessToken = _tokenService.GenerateAccessToken(user, roles, permissions, settings.SessionTimeoutMinutes);
        var refreshTokenStr = _tokenService.GenerateRefreshToken();
        var refreshTokenHash = _refreshTokenHasher.Hash(refreshTokenStr);
        var refreshExpiresAt = DateTime.UtcNow.AddDays(settings.RefreshTokenLifetimeDays);
        var refreshToken = new RefreshToken(
            user.Id,
            refreshTokenHash,
            refreshExpiresAt,
            request.RequestIp,
            challenge.TenantId,
            TenantActorType,
            request.UserAgent);
        await _refreshTokenRepository.CreateAsync(refreshToken, ct);
        await _authAuditService.WriteAsync("tenant_login_mfa_challenge_consumed", user.Id, challenge.TenantId, "{\"channel\":\"email\"}", ct);
        await _authAuditService.WriteAsync("tenant_login_success", user.Id, challenge.TenantId, "{\"mfa\":true}", ct);

        return Response<AuthResponse>.Success(new AuthResponse(
            accessToken,
            refreshTokenStr,
            refreshExpiresAt,
            new UserDto(user.Id, user.Email, user.FirstName, user.LastName, user.IsActive, roles, user.TenantId)));
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
