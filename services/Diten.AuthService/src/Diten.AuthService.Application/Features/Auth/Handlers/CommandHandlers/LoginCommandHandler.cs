using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Auth.Commands;
using Diten.AuthService.Application.Features.Auth.Services;
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
    private readonly ITenantEffectivePermissionResolver _effectivePermissionResolver;
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
        ITenantEffectivePermissionResolver effectivePermissionResolver,
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
        _effectivePermissionResolver = effectivePermissionResolver;
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
            // FIX2 item 3 — anonymous: the row is written within a global budget, never once per request.
            if (AnonymousAuditBudget.SettingsReadFailed.TryTake())
                await _authAuditService.WriteAsync("tenant_login_settings_read_failed", null, _tenantContext.TenantId, "{}", ct);
            return Response<AuthResponse>.Fail("Login is temporarily unavailable.", [new ResponseError(AuthRefusalCodes.LoginUnavailable)], 401);
        }

        if (!settings.EmailLoginEnabled)
        {
            await _authAuditService.WriteAsync("tenant_login_denied_by_settings", null, _tenantContext.TenantId, "{\"reason\":\"email_login_disabled\"}", ct);
            return Response<AuthResponse>.Fail("Email login is disabled for this tenant.", [new ResponseError(AuthRefusalCodes.EmailLoginDisabled)], 401);
        }

        var user = await _userRepository.GetByEmailAndTenantAsync(request.Email, _tenantContext.TenantId, ct);

        // BL-521 FIX6 item 13 (A') — A PLATFORM ACCOUNT NEVER SIGNS IN AT THE TENANT DOOR. A platform administrator's account
        // sits in the platform system tenant, which is also a business tenant; asked at this door it used to verify the
        // password and mint a tenant_user token carrying the account's platform roles' whole permission set (measured on
        // FIX5: it opened the user list and let the token disable, reset the password of and assign roles to ANOTHER
        // administrator) — without ever asking Platform's administrator record, which the platform door asks every time. The
        // answer is this door's own generic 401, given BEFORE the password is looked at: an account of that kind and a
        // stranger's address are the same answer, no failed-attempt count, no audit row, nothing to tell them apart. The
        // ordinary users of the same tenant are untouched.
        // WP-USERS-ROLES-CLOSE-FIX1 FIX2 items 1–2 — ONE ANSWER UNTIL THE PASSWORD IS RIGHT. An unknown address, a platform
        // account at this door, a deactivated account, an invitation not yet accepted, a locked account — with a wrong
        // password every one of them is the same 401, the same code, the same body, and takes as long (the branches that
        // find no account to check verify against SignInTiming's fixed hash). Only the right password learns more: "locked"
        // or "deactivated". The failure is counted for every existing account (a locked one included), so the lockout keeps
        // its threshold.
        if (user == null)
        {
            SignInTiming.VerifyAgainstNoAccount(_passwordHasher, request.Password);
            return InvalidCredentials();
        }
        if (user.IsPlatformAccount)
        {
            // FIX7 item 5 - one warning (event code, door, tenant id - no e-mail, no user id); the answer is the same 401.
            TenantDoorPlatformAccountRefusal.Log(_logger, TenantDoorPlatformAccountRefusal.SignInDoor, _tenantContext.TenantId);
            SignInTiming.VerifyAgainstNoAccount(_passwordHasher, request.Password);
            return InvalidCredentials();
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            await HandleLoginFailure(user, settings, ct);
            return InvalidCredentials();
        }

        // The password is right: now — and only now — the account's state may be said.
        var lockoutFailure = await CheckLockout(user, ct);
        if (lockoutFailure is not null)
        {
            return lockoutFailure;
        }

        // Disabled accounts cannot log in. Checked AFTER password verify so it stays enumeration-safe.
        // Invited (not-yet-redeemed) users are inactive too, but they can't pass verify (random hash),
        // so the invitation flow is unaffected.
        if (!user.IsActive)
        {
            await _authAuditService.WriteAsync("tenant_login_denied_inactive", user.Id, _tenantContext.TenantId, "{}", ct);
            return Response<AuthResponse>.Fail("Account is disabled.", [new ResponseError(AuthRefusalCodes.AccountDeactivated)], 401);
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

        var success = await HandleLoginSuccess(user, request, settings, ct);
        return success is null
            ? InvalidCredentials()
            : Response<AuthResponse>.Success(success);
    }

    /// <summary>WP-USERS-ROLES-CLOSE-FIX1 item 2 (BL-496) — the ONE answer of every sign-in that did not match (no such address, a
    /// platform account at this door, a wrong password, a session undone meanwhile): same sentence, same code.</summary>
    internal static Response<AuthResponse> InvalidCredentials()
        => Response<AuthResponse>.Fail("Invalid email or password.", [new ResponseError(AuthRefusalCodes.InvalidCredentials)], 401);

    private async Task<Response<AuthResponse>?> CheckLockout(User user, CancellationToken ct)
    {
        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
        {
            await _authAuditService.WriteAsync("tenant_login_blocked_by_lockout", user.Id, _tenantContext.TenantId, "{}", ct);
            return Response<AuthResponse>.Fail("Account is locked. Please try again later.", [new ResponseError(AuthRefusalCodes.AccountLocked)], 401);
        }

        return null;
    }

    private async Task HandleLoginFailure(User user, TenantLoginSettingsSnapshot settings, CancellationToken ct)
    {
        // BL-529 — only the failure count and the lockout, never the whole account (a whole write could undo a reset
        // that landed between this attempt's read and now). FIX2 — and the count is INCREMENTED in the store: two wrong
        // passwords in parallel are two failures, so the lockout threshold cannot be stepped around.
        var failure = await _userRepository.RecordLoginFailureAsync(
            user.Id, _tenantContext.TenantId, settings.MaxFailedLoginAttempts, settings.LockoutDurationMinutes, ct);
        await _authAuditService.WriteAsync("tenant_login_password_failed", user.Id, _tenantContext.TenantId, "{}", ct);
        if (failure.LockoutStarted)
        {
            await _authAuditService.WriteAsync("tenant_login_lockout_started", user.Id, _tenantContext.TenantId, "{}", ct);
        }
    }

    // BL-529 — null when the account's password changed while this sign-in ran (an administrator's reset): the token it
    // wrote is revoked again and the caller answers exactly as for a wrong password.
    private async Task<AuthResponse?> HandleLoginSuccess(User user, LoginCommand request, TenantLoginSettingsSnapshot settings, CancellationToken ct)
    {
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
        var rolePermissions = (await _rolePermissionRepository
                .GetPermissionsByRolesAsync(roleIds, _tenantContext.TenantId, ct))
            ?.Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];
        var permissions = await _effectivePermissionResolver.ResolveAsync(
            _tenantContext.TenantId,
            rolePermissions,
            ct);

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
        // BL-529 FIX3 — the session is bound to the password it was opened with (SessionPasswordBinding).
        SessionPasswordBinding.Bind(refreshToken, _refreshTokenHasher, user);
        await _refreshTokenRepository.CreateAsync(refreshToken, ct);

        if (!await IssuedSessionGuard.StillValidAsync(_userRepository, _refreshTokenRepository, user.Id, _tenantContext.TenantId, user.PasswordHash, refreshTokenStr, ct))
        {
            await _authAuditService.WriteAsync("tenant_login_session_revoked_by_reset", user.Id, _tenantContext.TenantId, "{}", ct);
            return null;
        }

        // BL-529 — only the sign-in's own fields (last sign-in, failure count, lockout), never the whole account.
        user.RecordLoginSuccess();
        await _userRepository.RecordLoginOutcomeAsync(user, _tenantContext.TenantId, ct);
        await _authAuditService.WriteAsync("tenant_login_success", user.Id, _tenantContext.TenantId, "{\"mfa\":false}", ct);

        return new AuthResponse(
            accessToken,
            refreshTokenStr,
            refreshExpiresAt,
            new UserDto(user.Id, user.Email, user.FirstName, user.LastName, user.IsActive, roles, user.TenantId),
            // FIX-TENANT-MUSTCHANGEPW — surface the forced-change flag so the Web bridge can route a temp-password
            // tenant user to the change-password page (mirrors the platform login).
            RequiresPasswordChange: user.MustChangePassword
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
