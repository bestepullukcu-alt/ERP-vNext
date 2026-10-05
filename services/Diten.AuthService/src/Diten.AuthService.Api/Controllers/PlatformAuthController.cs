using Diten.AuthService.Api.Models;
using Diten.AuthService.Api.Controllers.Common;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Auth.Services;
using Diten.AuthService.Application.Features.Users.Services;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Auth.Commands;
using Diten.AuthService.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace Diten.AuthService.Api.Controllers;

[Route("api/platform-auth")]
public sealed class PlatformAuthController : CustomBaseController
{
    private static readonly Guid PlatformTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";

    private readonly IMediator _mediator;
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordPolicyService _passwordPolicyService;
    private readonly IRefreshTokenHasher _refreshTokenHasher;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenService _tokenService;
    private readonly IInternalEventAuthService _internalEventAuthService;
    private readonly IPlatformAuthEmailService _emailService;
    private readonly IPlatformAdministratorStatusClient _platformAdministratorStatusClient;
    private readonly IUserAuditRecorder _audit;
    private readonly Diten.AuthService.Infrastructure.Security.PasswordDoorRateLimiter _passwordDoors;
    private readonly Diten.AuthService.Infrastructure.Security.ClientAddressResolver _clientAddress;
    private readonly IWebHostEnvironment _environment;

    public PlatformAuthController(
        IMediator mediator,
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IUserRoleRepository userRoleRepository,
        IRolePermissionRepository rolePermissionRepository,
        IPasswordHasher passwordHasher,
        IPasswordPolicyService passwordPolicyService,
        IRefreshTokenHasher refreshTokenHasher,
        IRefreshTokenRepository refreshTokenRepository,
        ITokenService tokenService,
        IInternalEventAuthService internalEventAuthService,
        IPlatformAuthEmailService emailService,
        IPlatformAdministratorStatusClient platformAdministratorStatusClient,
        IWebHostEnvironment environment,
        IUserAuditRecorder audit,
        Diten.AuthService.Infrastructure.Security.PasswordDoorRateLimiter passwordDoors,
        Diten.AuthService.Infrastructure.Security.ClientAddressResolver clientAddress)
    {
        _clientAddress = clientAddress;
        _audit = audit;
        _passwordDoors = passwordDoors;
        _mediator = mediator;
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _passwordHasher = passwordHasher;
        _passwordPolicyService = passwordPolicyService;
        _refreshTokenHasher = refreshTokenHasher;
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
        _internalEventAuthService = internalEventAuthService;
        _emailService = emailService;
        _platformAdministratorStatusClient = platformAdministratorStatusClient;
        _environment = environment;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] PlatformLoginRequest request, CancellationToken ct)
    {
        var command = new PlatformLoginCommand(request.Email, request.Password, ResolveRequestIp(HttpContext), ResolveUserAgent(HttpContext), request.RememberMe);
        var result = await _mediator.Send(command, ct);
        return CreateActionResultInstance(result);
    }

    [HttpPost("platform-admins/provision")]
    [AllowAnonymous]
    public async Task<IActionResult> ProvisionPlatformAdmin([FromBody] PlatformAdminProvisioningRequest request, CancellationToken ct)
    {
        if (!_internalEventAuthService.IsAuthorized(Request.Headers[InternalApiKeyHeader].FirstOrDefault()))
        {
            return Unauthorized(new { message = "internal authentication failed" });
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var normalizedUserName = NormalizeUserName(request.UserName);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return BadRequest(new { message = "email is required" });
        }

        if (string.IsNullOrWhiteSpace(normalizedUserName))
        {
            return BadRequest(new { message = "userName is required" });
        }

        var existingUser = await _userRepository.GetByEmailAndTenantAsync(normalizedEmail, PlatformTenantId, ct);
        var existingUserNameOwner = await _userRepository.GetByUserNameAndTenantAsync(normalizedUserName, PlatformTenantId, ct);
        if (existingUserNameOwner is not null && (existingUser is null || existingUserNameOwner.Id != existingUser.Id))
        {
            return Conflict(new { message = "username already exists" });
        }

        var (firstName, lastName) = SplitName(request.DisplayName, normalizedEmail);
        var userProvisioned = existingUser is null;
        var setupToken = _tokenService.GenerateRefreshToken();

        if (existingUser is not null)
        {
            // BL-529 — an EXISTING platform account sent a new set-password link (Platform's "Resend invite") is reset: the
            // old password stops working and every session ends, the same rule (and audit row) as every administrator reset.
            // FIX4 — the account reaches ResetAsync UNCHANGED: every change of this re-invitation (name, user name, switch,
            // actor type, link) is made inside `apply`, so the targeted write sees — and stores — each of them, and the
            // switch the reset compares against is the stored one (a passive account is re-invited, not refused).
            (string? SetupUrl, bool EmailSent) delivery = (null, false);
            var outcome = await AdminPasswordReset.ResetAsync(
                existingUser,
                PlatformTenantId,
                AdminResetVia.PlatformAdministratorReinvite,
                _ => AdminPasswordReset.UnusableHash(_passwordHasher, _tokenService),
                u =>
                {
                    u.SetUserName(request.UserName);
                    u.UpdateProfile(firstName, lastName);
                    u.ActivateByAdministrator();
                    u.ConfirmEmail();
                    u.SetPlatformActorType(NormalizeActorType(request.ActorType));
                    u.SetPasswordResetToken(_refreshTokenHasher.Hash(setupToken), DateTime.UtcNow.AddHours(24));
                },
                c => _userRepository.GetByEmailAndTenantAsync(normalizedEmail, PlatformTenantId, c),
                async (u, c) =>
                {
                    await SyncPlatformRolesAsync(u.Id, request.Roles, c);
                    delivery = await SendSetupLinkAsync(u.Email, setupToken, c);
                    return delivery.EmailSent;
                },
                _userRepository,
                _refreshTokenRepository,
                _audit,
                ct);
            if (!outcome.Succeeded)
            {
                return Conflict(new { message = "the account changed while it was being re-invited; try again" });
            }

            return Ok(new
            {
                userProvisioned,
                message = "processed",
                setupUrl = delivery.SetupUrl,
                emailSent = delivery.EmailSent
            });
        }

        var user = new User(
            normalizedEmail,
            _passwordHasher.Hash(_tokenService.GenerateRefreshToken()),
            firstName,
            lastName,
            PlatformTenantId);
        user.SetUserName(request.UserName);
        user.ConfirmEmail();
        // WP-INFRA-AUTH-ACCOUNT-KIND-01 — a provisioned platform admin is created Unknown; an existing account's
        // kind is left exactly as it is (this path never rewrites a classification).
        user.SetAccountKind(Diten.AuthService.Domain.Enums.AccountKind.Unknown);
        user.SetPlatformActorType(NormalizeActorType(request.ActorType));
        user.SetPasswordResetToken(_refreshTokenHasher.Hash(setupToken), DateTime.UtcNow.AddHours(24));
        await _userRepository.CreateAsync(user, ct);
        await SyncPlatformRolesAsync(user.Id, request.Roles, ct);
        var setupDelivery = await SendSetupLinkAsync(user.Email, setupToken, ct);

        return Ok(new
        {
            userProvisioned,
            message = "processed",
            setupUrl = setupDelivery.SetupUrl,
            emailSent = setupDelivery.EmailSent
        });
    }

    /// <summary>
    /// BL-529 — A PLATFORM ADMINISTRATOR RESETS ANOTHER PLATFORM ADMINISTRATOR'S PASSWORD. The administrator's reset, not
    /// the self-service "forgot password" (which stays as it is: it proves nothing about the caller, so it must not end
    /// anyone's password): the old password stops working, every session ends, a 24-hour set-password link is e-mailed,
    /// the audit row is written (<see cref="AdminPasswordReset"/>). A platform actor with
    /// <c>platform.administrators.update</c> only; one's own account is refused (409 <c>USER_RESET_SELF</c>).
    /// </summary>
    [HttpPost("platform-admins/reset-password")]
    [Authorize]
    public async Task<IActionResult> ResetPlatformAdministratorPassword([FromBody] PlatformAdministratorResetRequest request, CancellationToken ct)
    {
        if (!IsPlatformActorWith(PlatformAdministratorsUpdatePermission))
        {
            return CreateActionResultInstance(Response<NoContent>.Fail("Platform administrator privileges are required.", 403));
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var target = string.IsNullOrWhiteSpace(normalizedEmail)
            ? null
            : await _userRepository.GetByEmailAndTenantAsync(normalizedEmail, PlatformTenantId, ct);
        if (target is null)
        {
            return CreateActionResultInstance(UserErrorCodes.NotFoundRefusal<NoContent>());
        }

        var callerId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (Guid.TryParse(callerId, out var caller) && caller == target.Id)
        {
            return CreateActionResultInstance(Response<NoContent>.Fail(
                "You cannot reset the password of the account you are signed in with — use Change password.",
                [new ResponseError(UserErrorCodes.ResetSelf)],
                409));
        }

        var setupToken = _tokenService.GenerateRefreshToken();
        (string? SetupUrl, bool EmailSent) delivery = (null, false);
        var outcome = await AdminPasswordReset.ResetAsync(
            target,
            PlatformTenantId,
            AdminResetVia.PlatformAdministratorReset,
            _ => AdminPasswordReset.UnusableHash(_passwordHasher, _tokenService),
            u => u.SetPasswordResetToken(_refreshTokenHasher.Hash(setupToken), DateTime.UtcNow.AddHours(24)),
            c => _userRepository.GetByEmailAndTenantAsync(normalizedEmail, PlatformTenantId, c),
            async (u, c) =>
            {
                delivery = await SendSetupLinkAsync(u.Email, setupToken, c);
                return delivery.EmailSent;
            },
            _userRepository,
            _refreshTokenRepository,
            _audit,
            ct);

        if (outcome.Conflict)
        {
            return CreateActionResultInstance(Response<NoContent>.Fail(
                "The account changed while it was being reset; nothing stale was written. Reset it again.",
                [new ResponseError(UserErrorCodes.ResetConflict)],
                409));
        }

        if (!outcome.Succeeded)
        {
            return CreateActionResultInstance(UserErrorCodes.NotFoundRefusal<NoContent>());
        }

        return Ok(new { message = "processed", setupUrl = delivery.SetupUrl, emailSent = delivery.EmailSent });
    }

    private const string PlatformAdministratorsUpdatePermission = "platform.administrators.update";

    private IActionResult TooManyRequests()
    {
        Response.Headers.RetryAfter = ((int)_passwordDoors.Window.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return CreateActionResultInstance(Response<NoContent>.Fail(
            "Too many requests. Try again later.",
            [new ResponseError(Diten.AuthService.Infrastructure.Security.PasswordDoorRateLimiter.TooManyRequestsCode)],
            429));
    }

    // The caller is a platform actor of the platform tenant holding the permission. Case-insensitive like Platform's own
    // check: the legacy alias (Platform.Administrators.Update) differs from the canonical key only by case.
    private bool IsPlatformActorWith(string permission)
    {
        var actorType = User.FindFirst("actor_type")?.Value;
        // BL-529 FIX2 — a platform administrator only: a partner administrator never resets a platform administrator
        // (BL-521's scope and last-SuperAdmin protection live in the Platform record; this door does not reach them).
        var isPlatformActor = string.Equals(actorType, "platform_admin", StringComparison.OrdinalIgnoreCase);
        var tenantClaim = User.FindFirst("tenant_id")?.Value;
        return isPlatformActor
               && Guid.TryParse(tenantClaim, out var tenantId) && tenantId == PlatformTenantId
               && User.FindAll("permission").Any(c => string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase));
    }

    [HttpPost("platform-admins/sync")]
    [AllowAnonymous]
    public async Task<IActionResult> SyncPlatformAdmin([FromBody] PlatformAdminSyncRequest request, CancellationToken ct)
    {
        if (!_internalEventAuthService.IsAuthorized(Request.Headers[InternalApiKeyHeader].FirstOrDefault()))
        {
            return Unauthorized(new { message = "internal authentication failed" });
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var normalizedUserName = NormalizeUserName(request.UserName);
        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(normalizedUserName))
        {
            return BadRequest(new { message = "email and userName are required" });
        }

        var user = await _userRepository.GetByEmailAndTenantAsync(normalizedEmail, PlatformTenantId, ct);
        if (user is null)
        {
            return NotFound(new { message = "platform user not found" });
        }

        var existingUserNameOwner = await _userRepository.GetByUserNameAndTenantAsync(normalizedUserName, PlatformTenantId, ct);
        if (existingUserNameOwner is not null && existingUserNameOwner.Id != user.Id)
        {
            return Conflict(new { message = "username already exists" });
        }

        var (firstName, lastName) = SplitName(request.DisplayName, normalizedEmail);
        var state = _userRepository.CaptureState(user);
        user.SetUserName(request.UserName);
        user.UpdateProfile(firstName, lastName);
        user.Activate();
        user.ConfirmEmail();
        user.SetPlatformActorType(NormalizeActorType(request.ActorType));

        // BL-529 FIX3 — only the fields this sync changes (name, profile, switch, actor type): a whole write from the copy
        // read above could put back a password hash an administrator's reset replaced meanwhile.
        await _userRepository.TryWriteChangesAsync(user, state, PlatformTenantId, UserWriteCondition.None, ct);

        await SyncPlatformRolesAsync(user.Id, request.Roles, ct);

        return NoContent();
    }

    [HttpPost("change-password/forced")]
    [Authorize]
    public async Task<IActionResult> ForcedChangePassword([FromBody] PlatformForcedChangePasswordRequest request, CancellationToken ct)
    {
        var user = await ResolveCurrentPlatformUserAsync(ct);
        if (user is null)
        {
            return CreateActionResultInstance(Response<AuthResponse>.Fail("Unauthorized.", 401));
        }

        if (!await _platformAdministratorStatusClient.IsActiveAsync(user.Email, ct))
        {
            await _refreshTokenRepository.RevokeAllByUserAsync(user.Id, PlatformTenantId, ct);
            return CreateActionResultInstance(Response<AuthResponse>.Fail("Platform administrator account is inactive.", 401));
        }

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return CreateActionResultInstance(Response<AuthResponse>.Fail("Current password is incorrect.", 401));
        }

        await _passwordPolicyService.ValidateTenantPasswordAsync(PlatformTenantId, user.Id, request.NewPassword, "platform_forced_change", ct);
        var verifiedHash = user.PasswordHash;
        var state = _userRepository.CaptureState(user);
        user.UpdatePassword(_passwordHasher.Hash(request.NewPassword));
        user.ClearPasswordChangeRequirement();
        // BL-529 FIX2 — only over the password just verified: never over an administrator's reset that landed meanwhile.
        if (!await _userRepository.TryWriteChangesAsync(user, state, PlatformTenantId, new UserWriteCondition(PasswordHash: verifiedHash), ct))
        {
            return CreateActionResultInstance(Response<AuthResponse>.Fail(
                "The password changed while this request ran (for example an administrator reset it). Sign in again.",
                [new ResponseError(AuthRefusalCodes.PasswordChangedMeanwhile)],
                409));
        }
        await _refreshTokenRepository.RevokeLiveSessionsAsync(user.Id, PlatformTenantId, SessionRevocationReasons.PasswordChanged, ct);

        var authResponse = await BuildPlatformAuthResponseAsync(user, request.RememberMe, ResolveRequestIp(HttpContext), ResolveUserAgent(HttpContext), false, ct);
        return CreateActionResultInstance(Response<AuthResponse>.Success(authResponse));
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] PlatformForgotPasswordRequest request, CancellationToken ct)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        if (!_passwordDoors.TryAcquireForgotPassword(_clientAddress.Resolve(HttpContext), normalizedEmail, _clientAddress.IdentifiesClients))
        {
            return TooManyRequests();
        }

        var user = string.IsNullOrWhiteSpace(normalizedEmail)
            ? null
            : await _userRepository.GetByEmailAndTenantAsync(normalizedEmail, PlatformTenantId, ct);

        if (user is not null && user.IsActive && await _platformAdministratorStatusClient.IsActiveAsync(user.Email, ct))
        {
            var resetToken = _tokenService.GenerateRefreshToken();
            // BL-529 FIX2 — the link fields ONLY. This request read the account before the Platform status call; a whole
            // write here could put back the password hash an administrator's reset replaced meanwhile.
            await _userRepository.SetPasswordResetTokenAsync(user.Id, PlatformTenantId, _refreshTokenHasher.Hash(resetToken), DateTime.UtcNow.AddHours(1), ct);
            try
            {
                await _emailService.SendPlatformPasswordResetAsync(user.Email, resetToken, ct);
            }
            catch
            {
                // Keep forgot-password enumeration-safe; operations can inspect service logs/configuration.
            }
        }

        return Ok(new { message = "If the email exists, reset instructions have been sent." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] PlatformResetPasswordRequest request, CancellationToken ct)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        if (!_passwordDoors.TryAcquireLinkRedemption(_clientAddress.Resolve(HttpContext), normalizedEmail, _clientAddress.IdentifiesClients))
        {
            return TooManyRequests();
        }

        var user = await _userRepository.GetByEmailAndTenantAsync(normalizedEmail, PlatformTenantId, ct);
        if (user is null ||
            !await _platformAdministratorStatusClient.IsActiveAsync(user.Email, ct) ||
            string.IsNullOrWhiteSpace(user.PasswordResetTokenHash) ||
            user.PasswordResetTokenExpiresAt <= DateTime.UtcNow ||
            !string.Equals(user.PasswordResetTokenHash, _refreshTokenHasher.Hash(request.Token), StringComparison.Ordinal))
        {
            return CreateActionResultInstance(Response<NoContent>.Fail("Password reset token is invalid or expired.", 400));
        }

        await _passwordPolicyService.ValidateTenantPasswordAsync(PlatformTenantId, user.Id, request.NewPassword, "platform_reset_password", ct);
        var redeemedTokenHash = user.PasswordResetTokenHash!;
        var redeemState = _userRepository.CaptureState(user);
        user.UpdatePassword(_passwordHasher.Hash(request.NewPassword));
        user.ClearPasswordChangeRequirement();
        user.Activate();
        user.ConfirmEmail();
        // BL-529 FIX2 — written only while this link is still the account's (not replaced by a newer reset, not used by a
        // parallel redemption).
        if (!await _userRepository.TryWriteChangesAsync(user, redeemState, PlatformTenantId, new UserWriteCondition(PasswordResetTokenHash: redeemedTokenHash), ct))
        {
            return CreateActionResultInstance(Response<NoContent>.Fail("Password reset token is invalid or expired.", 400));
        }
        await _refreshTokenRepository.RevokeLiveSessionsAsync(user.Id, PlatformTenantId, SessionRevocationReasons.PasswordChanged, ct);
        return CreateActionResultInstance(Response<NoContent>.Success(204));
    }

    private async Task<User?> ResolveCurrentPlatformUserAsync(CancellationToken ct)
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(userIdString, out var userId)
            ? await _userRepository.GetByIdAndTenantAsync(userId, PlatformTenantId, ct)
            : null;
    }

    private async Task<AuthResponse> BuildPlatformAuthResponseAsync(User user, bool rememberMe, string requestIp, string? userAgent, bool requiresPasswordChange, CancellationToken ct)
    {
        var roles = (await _userRoleRepository.GetRolesByUserAsync(user.Id, PlatformTenantId, ct))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var roleIds = await ResolveRoleIdsAsync(roles, ct);
        var permissions = (await _rolePermissionRepository.GetPermissionsByRolesAsync(roleIds, PlatformTenantId, ct))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var actorType = NormalizeActorType(user.PlatformActorType ?? "platform_admin");
        var accessToken = _tokenService.GeneratePlatformAccessToken(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            PlatformTenantId,
            actorType,
            roles,
            permissions,
            15,
            requiresPasswordChange);
        var refreshTokenStr = _tokenService.GenerateRefreshToken();
        var refreshTokenHash = _refreshTokenHasher.Hash(refreshTokenStr);
        var refreshToken = new RefreshToken(
            user.Id,
            refreshTokenHash,
            DateTime.UtcNow.AddDays(rememberMe ? 30 : 7),
            requestIp,
            PlatformTenantId,
            actorType,
            userAgent);
        // BL-529 FIX3 — the session is bound to the password it was opened with (SessionPasswordBinding).
        SessionPasswordBinding.Bind(refreshToken, _refreshTokenHasher, user);
        await _refreshTokenRepository.CreateAsync(refreshToken, ct);

        return new AuthResponse(
            accessToken,
            refreshTokenStr,
            refreshToken.ExpiresAt,
            new Diten.AuthService.Application.DTOs.UserDto(user.Id, user.Email, user.FirstName, user.LastName, user.IsActive, roles, null),
            RequiresPasswordChange: requiresPasswordChange);
    }

    private async Task<List<Guid>> ResolveRoleIdsAsync(IEnumerable<string> roleNames, CancellationToken ct)
    {
        var roleIds = new List<Guid>();
        foreach (var roleName in roleNames)
        {
            var role = await _roleRepository.GetByNameAndTenantAsync(roleName, PlatformTenantId, ct);
            if (role is not null) roleIds.Add(role.Id);
        }
        return roleIds;
    }

    private static string ResolveRequestIp(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static string? ResolveUserAgent(HttpContext context)
    {
        return context.Request.Headers.UserAgent.FirstOrDefault();
    }

    private static string NormalizeEmail(string email) => (email ?? string.Empty).Trim().ToLowerInvariant();
    private static string NormalizeUserName(string userName) => (userName ?? string.Empty).Trim().ToLowerInvariant();

    private static string NormalizeActorType(string actorType) =>
        string.Equals(actorType, "PartnerAdmin", StringComparison.OrdinalIgnoreCase) ? "partner_admin" : "platform_admin";

    private static IEnumerable<string> NormalizeRoles(IEnumerable<string>? roles)
    {
        var normalized = (roles ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return normalized.Length == 0 ? ["ReadOnly"] : normalized;
    }

    private async Task SyncPlatformRolesAsync(Guid userId, IEnumerable<string>? requestedRoles, CancellationToken ct)
    {
        var desired = NormalizeRoles(requestedRoles).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var platformRoleNames = new HashSet<string>(["SuperAdmin", "BillingAdmin", "SupportAdmin", "ReadOnly"], StringComparer.OrdinalIgnoreCase);
        var current = (await _userRoleRepository.GetRolesByUserAsync(userId, PlatformTenantId, ct))
            .Where(role => platformRoleNames.Contains(role))
            .ToArray();

        foreach (var roleName in desired)
        {
            var role = await _roleRepository.UpsertSystemRoleAsync(roleName, roleName, "Platform administrator role", PlatformTenantId, ct);
            if (!await _userRoleRepository.ExistsAsync(userId, role.Id, PlatformTenantId, ct))
            {
                await _userRoleRepository.AssignAsync(new UserRole(userId, role.Id, PlatformTenantId, "system"), ct);
            }
        }

        foreach (var roleName in current.Where(role => !desired.Contains(role)))
        {
            var role = await _roleRepository.GetByNameAndTenantAsync(roleName, PlatformTenantId, ct);
            if (role is not null)
            {
                await _userRoleRepository.RevokeAsync(userId, role.Id, PlatformTenantId, ct);
            }
        }
    }

    private async Task<(string? SetupUrl, bool EmailSent)> SendSetupLinkAsync(string email, string setupToken, CancellationToken ct)
    {
        var setupUrl = _emailService.BuildPlatformPasswordResetUrl(email, setupToken);
        try
        {
            await _emailService.SendPlatformPasswordResetAsync(email, setupToken, ct);
            return (_environment.IsDevelopment() ? setupUrl : null, true);
        }
        catch when (_environment.IsDevelopment())
        {
            return (setupUrl, false);
        }
    }

    private static (string FirstName, string LastName) SplitName(string? name, string email)
    {
        var normalized = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized) || string.Equals(normalized, email, StringComparison.OrdinalIgnoreCase))
        {
            return ("Platform", "Admin");
        }

        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 1 ? (parts[0], "Admin") : (parts[0], string.Join(' ', parts.Skip(1)));
    }
}
