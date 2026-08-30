using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Auth.Commands;
using Diten.AuthService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Auth.Handlers.CommandHandlers;

/// <summary>
/// FIX-TENANT-MUSTCHANGEPW — tenant counterpart of PlatformAuthController's forced change. Verifies the current
/// password, enforces the tenant password policy, clears <see cref="User.MustChangePassword"/>, revokes existing
/// refresh tokens, and issues fresh tokens (so the re-issued tenant token no longer carries pwd_change_required).
/// </summary>
public sealed class ForcedChangeTenantPasswordCommandHandler
    : IRequestHandler<ForcedChangeTenantPasswordCommand, Response<AuthResponse>>
{
    private const string TenantActorType = "tenant_user";

    private readonly IUserRepository _userRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly ITenantEffectivePermissionResolver _effectivePermissionResolver;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenHasher _refreshTokenHasher;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordPolicyService _passwordPolicyService;
    private readonly ITenantLoginSettingsClient _tenantLoginSettingsClient;
    private readonly ITenantAdminActivationClient _tenantAdminActivationClient;
    private readonly IAuthAuditService _authAuditService;
    private readonly ILogger<ForcedChangeTenantPasswordCommandHandler> _logger;

    public ForcedChangeTenantPasswordCommandHandler(
        IUserRepository userRepository,
        IUserRoleRepository userRoleRepository,
        IRoleRepository roleRepository,
        IRolePermissionRepository rolePermissionRepository,
        ITenantEffectivePermissionResolver effectivePermissionResolver,
        ITokenService tokenService,
        IRefreshTokenHasher refreshTokenHasher,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        IPasswordPolicyService passwordPolicyService,
        ITenantLoginSettingsClient tenantLoginSettingsClient,
        ITenantAdminActivationClient tenantAdminActivationClient,
        IAuthAuditService authAuditService,
        ILogger<ForcedChangeTenantPasswordCommandHandler> logger)
    {
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _roleRepository = roleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _effectivePermissionResolver = effectivePermissionResolver;
        _tokenService = tokenService;
        _refreshTokenHasher = refreshTokenHasher;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _passwordPolicyService = passwordPolicyService;
        _tenantLoginSettingsClient = tenantLoginSettingsClient;
        _tenantAdminActivationClient = tenantAdminActivationClient;
        _authAuditService = authAuditService;
        _logger = logger;
    }

    public async Task<Response<AuthResponse>> Handle(ForcedChangeTenantPasswordCommand request, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAndTenantAsync(request.UserId, request.TenantId, ct);
        if (user is null || !user.IsActive)
        {
            return Response<AuthResponse>.Fail("Unauthorized.", 401);
        }

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return Response<AuthResponse>.Fail("Current password is incorrect.", 401);
        }

        // Enforce the tenant password policy (throws ValidationException → 400 via the pipeline, like the platform flow).
        await _passwordPolicyService.ValidateTenantPasswordAsync(request.TenantId, user.Id, request.NewPassword, "tenant_forced_change", ct);

        user.UpdatePassword(_passwordHasher.Hash(request.NewPassword));
        user.ClearPasswordChangeRequirement(); // MustChangePassword → false
        await _userRepository.UpdateForTenantAsync(user, request.TenantId, ct);
        await _refreshTokenRepository.RevokeAllByUserAsync(user.Id, request.TenantId, ct);
        await _authAuditService.WriteAsync("tenant_forced_password_changed", user.Id, request.TenantId, "{}", ct);

        // FIX-TENANT-ADMIN-INVITE-ACTIVATION (Part B) — the invited admin has now completed its forced first-login
        // change; tell Platform to flip the matching TenantAdminUser Invited → Active. Best-effort: an unreachable
        // Platform must NOT fail the password change (idempotent + no-op for non-admin tenant_users on the Platform side).
        try
        {
            await _tenantAdminActivationClient.NotifyActivatedAsync(user.Email, request.TenantId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tenant admin activation callback failed (non-blocking). UserId={UserId} TenantId={TenantId}", user.Id, request.TenantId);
        }

        var authResponse = await BuildAuthResponseAsync(user, request, ct);
        return Response<AuthResponse>.Success(authResponse);
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(User user, ForcedChangeTenantPasswordCommand request, CancellationToken ct)
    {
        var settings = await _tenantLoginSettingsClient.GetAsync(request.TenantId, ct);

        var roles = (await _userRoleRepository.GetRolesByUserAsync(user.Id, request.TenantId, ct))
            ?.Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];

        var roleIds = await ResolveRoleIdsAsync(roles, request.TenantId, ct);
        var rolePermissions = (await _rolePermissionRepository.GetPermissionsByRolesAsync(roleIds, request.TenantId, ct))
            ?.Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];
        var permissions = await _effectivePermissionResolver.ResolveAsync(
            request.TenantId,
            rolePermissions,
            ct);

        // MustChangePassword is now cleared, so the re-issued token carries pwd_change_required=false.
        var accessToken = _tokenService.GenerateAccessToken(user, roles, permissions, settings.SessionTimeoutMinutes);
        var refreshTokenStr = _tokenService.GenerateRefreshToken();
        var refreshTokenHash = _refreshTokenHasher.Hash(refreshTokenStr);
        var refreshExpiresAt = DateTime.UtcNow.AddDays(request.RememberMe ? 30 : settings.RefreshTokenLifetimeDays);

        var refreshToken = new RefreshToken(
            user.Id,
            refreshTokenHash,
            refreshExpiresAt,
            request.RequestIp,
            request.TenantId,
            TenantActorType,
            request.UserAgent);
        await _refreshTokenRepository.CreateAsync(refreshToken, ct);

        return new AuthResponse(
            accessToken,
            refreshTokenStr,
            refreshExpiresAt,
            new UserDto(user.Id, user.Email, user.FirstName, user.LastName, user.IsActive, roles, user.TenantId),
            RequiresPasswordChange: false);
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
