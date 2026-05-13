using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Auth.Commands;
using Diten.AuthService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Auth.Handlers.CommandHandlers;

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, Response<AuthResponse>>
{
    private const string TenantActorType = "tenant_user";

    private readonly IUserRepository _userRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenHasher _refreshTokenHasher;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuthAuditService _authAuditService;
    private readonly ITenantLoginSettingsClient _tenantLoginSettingsClient;
    private readonly IMfaChallengeService _mfaChallengeService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IUserRoleRepository userRoleRepository,
        IRoleRepository roleRepository,
        IRolePermissionRepository rolePermissionRepository,
        ITokenService tokenService,
        IRefreshTokenHasher refreshTokenHasher,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        IAuthAuditService authAuditService,
        ITenantLoginSettingsClient tenantLoginSettingsClient,
        IMfaChallengeService mfaChallengeService,
        ITenantContext tenantContext,
        ILogger<LoginCommandHandler> logger)
    {
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _roleRepository = roleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _tokenService = tokenService;
        _refreshTokenHasher = refreshTokenHasher;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _authAuditService = authAuditService;
        _tenantLoginSettingsClient = tenantLoginSettingsClient;
        _mfaChallengeService = mfaChallengeService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<Response<AuthResponse>> Handle(LoginCommand request, CancellationToken ct)
    {
        TenantLoginSettingsSnapshot settings;
        try
        {
            settings = await _tenantLoginSettingsClient.GetAsync(_tenantContext.TenantId, ct);
        }
        catch
        {
            await _authAuditService.WriteAsync("tenant_login_settings_read_failed", null, _tenantContext.TenantId, "{}", ct);
            return Response<AuthResponse>.Fail("Login is temporarily unavailable.", 401);
        }

        if (!settings.EmailLoginEnabled)
        {
            await _authAuditService.WriteAsync("tenant_login_denied_by_settings", null, _tenantContext.TenantId, "{\"reason\":\"email_login_disabled\"}", ct);
            return Response<AuthResponse>.Fail("Email login is disabled for this tenant.", 401);
        }

        var user = await _userRepository.GetByEmailAndTenantAsync(request.Email, _tenantContext.TenantId, ct);
        if (user == null) return Response<AuthResponse>.Fail("Invalid email or password.", 401);

        var lockoutFailure = await CheckLockout(user, ct);
        if (lockoutFailure is not null)
        {
            return lockoutFailure;
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            await HandleLoginFailure(user, settings, ct);
            return Response<AuthResponse>.Fail("Invalid email or password.", 401);
        }

        if (settings.MfaRequired)
        {
            var challenge = await _mfaChallengeService.CreateEmailChallengeAsync(user, request.RequestIp, request.UserAgent, ct);
            await _authAuditService.WriteAsync("tenant_login_mfa_challenge_created", user.Id, _tenantContext.TenantId, "{\"channel\":\"email\"}", ct);
            return Response<AuthResponse>.Success(new AuthResponse(
                null,
                null,
                null,
                null,
                true,
                challenge.ChallengeId,
                challenge.MaskedDestination,
                challenge.Channel,
                challenge.ExpiresAtUtc));
        }

        return Response<AuthResponse>.Success(await HandleLoginSuccess(user, request, settings, ct));
    }

    private async Task<Response<AuthResponse>?> CheckLockout(User user, CancellationToken ct)
    {
        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
        {
            await _authAuditService.WriteAsync("tenant_login_blocked_by_lockout", user.Id, _tenantContext.TenantId, "{}", ct);
            return Response<AuthResponse>.Fail("Account is locked. Please try again later.", 401);
        }

        return null;
    }

    private async Task HandleLoginFailure(User user, TenantLoginSettingsSnapshot settings, CancellationToken ct)
    {
        user.RecordLoginFailure(settings.MaxFailedLoginAttempts, settings.LockoutDurationMinutes);
        await _userRepository.UpdateAsync(user, ct);
        await _authAuditService.WriteAsync("tenant_login_password_failed", user.Id, _tenantContext.TenantId, "{}", ct);
        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
        {
            await _authAuditService.WriteAsync("tenant_login_lockout_started", user.Id, _tenantContext.TenantId, "{}", ct);
        }
    }

    private async Task<AuthResponse> HandleLoginSuccess(User user, LoginCommand request, TenantLoginSettingsSnapshot settings, CancellationToken ct)
    {
        user.RecordLoginSuccess();
        await _userRepository.UpdateAsync(user, ct);

        var roleValues = await _userRoleRepository.GetRolesByUserAsync(user.Id, _tenantContext.TenantId, ct);
        var roles = roleValues?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];

        if (roles.Length == 0)
        {
            _logger.LogWarning(
                "Login produced empty role set. UserId={UserId} TenantId={TenantId} Email={Email}",
                user.Id,
                _tenantContext.TenantId,
                user.Email);

            await _authAuditService.WriteEmptyRoleLoginAsync(user.Id, _tenantContext.TenantId, user.Email, ct);
        }

        var roleIds = await ResolveRoleIdsAsync(roles, _tenantContext.TenantId, ct);
        var permissions = (await _rolePermissionRepository
                .GetPermissionsByRolesAsync(roleIds, _tenantContext.TenantId, ct))
            ?.Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];

        var accessToken = _tokenService.GenerateAccessToken(user, roles, permissions, settings.SessionTimeoutMinutes);
        var refreshTokenStr = _tokenService.GenerateRefreshToken();
        var refreshTokenHash = _refreshTokenHasher.Hash(refreshTokenStr);
        var refreshExpiresAt = DateTime.UtcNow.AddDays(request.RememberMe ? 30 : settings.RefreshTokenLifetimeDays);

        var refreshToken = new RefreshToken(
            user.Id,
            refreshTokenHash,
            refreshExpiresAt,
            request.RequestIp,
            _tenantContext.TenantId,
            TenantActorType,
            request.UserAgent);
        await _refreshTokenRepository.CreateAsync(refreshToken, ct);
        await _authAuditService.WriteAsync("tenant_login_success", user.Id, _tenantContext.TenantId, "{\"mfa\":false}", ct);

        return new AuthResponse(
            accessToken,
            refreshTokenStr,
            refreshExpiresAt,
            new UserDto(user.Id, user.Email, user.FirstName, user.LastName, user.IsActive, roles, user.TenantId)
        );
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
