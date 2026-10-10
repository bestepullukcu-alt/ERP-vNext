using Diten.AuthService.Api.Models;
using Diten.AuthService.Api.Controllers.Common;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Auth.Services;
using Diten.AuthService.Application.Features.Users.Services;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Auth.Commands;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Api.Controllers;

[Route("api/platform-auth")]
public sealed class PlatformAuthController : CustomBaseController
{
    private static readonly Guid PlatformTenantId = PlatformSystemTenant.Id;
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";

    /// <summary>BL-521 FIX3 item 4 — 403: the forced change door takes only a platform actor's token (platform or partner
    /// administrator, platform tenant).</summary>
    public const string ForcedChangeRequiresPlatformActorCode = "PLATFORM_FORCED_CHANGE_PLATFORM_ACTOR_REQUIRED";

    /// <summary>BL-521 FIX3 item 4 — 409: no forced change is pending on the account; an ordinary change has its own door.</summary>
    public const string ForcedChangeNotPendingCode = "PLATFORM_FORCED_CHANGE_NOT_PENDING";

    /// <summary>BL-521 FIX3 item 4 — 401: the temporary password has expired; a password reset replaces it.</summary>
    public const string TemporaryPasswordExpiredCode = "PLATFORM_TEMPORARY_PASSWORD_EXPIRED";

    /// <summary>BL-521 FIX4 item 5 — the Platform record status (its enum name) for which the sync switches the account on.</summary>
    private const string PlatformActiveStatus = "Active";

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
    private readonly ILogger<PlatformAuthController> _logger;

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
        Diten.AuthService.Infrastructure.Security.ClientAddressResolver clientAddress,
        ILogger<PlatformAuthController> logger)
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
        _logger = logger;
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
                    // Merge WP-CT-LANE-BL521-MERGE-01 — BL-521 FIX5 item 11 inside BL-529's reset: an EXISTING account follows
                    // the sync's rule, not "provision means switch on and confirm". It is switched on only for an Active record
                    // (none — an older Platform — leaves its state alone), as an administrator switches it on (BL-529 FIX5:
                    // the deactivation mark goes with it); its address is never confirmed here — only accepting the
                    // invitation does (reset-password). The reset itself (old password unusable, sessions ended, audit row,
                    // targeted conditional write) is BL-529's, unchanged.
                    if (string.Equals(request.Status, PlatformActiveStatus, StringComparison.OrdinalIgnoreCase))
                    {
                        u.ActivateByAdministrator();
                    }

                    u.SetPlatformActorType(NormalizeActorType(request.ActorType));
                    u.SetPasswordResetToken(_refreshTokenHasher.Hash(setupToken), DateTime.UtcNow.AddHours(24));
                },
                c => _userRepository.GetByEmailAndTenantAsync(normalizedEmail, PlatformTenantId, c),
                async (u, c) =>
                {
                    // BL-521 FIX4/FIX5 — the roles follow the record (the convergence that replaced SyncPlatformRolesAsync).
                    await PlatformAdministratorRoleConvergence.ConvergeProvisionedAsync(_userRoleRepository, _roleRepository, u.Id, PlatformTenantId, request.Roles, c);
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
        await PlatformAdministratorRoleConvergence.ConvergeProvisionedAsync(_userRoleRepository, _roleRepository, user.Id, PlatformTenantId, request.Roles, ct);
        // Merge WP-CT-LANE-BL521-MERGE-01 — no session sweep here: an account created on this line has none. BL-529 (7935b8143)
        // took the old shared tail's RevokeAllByUserAsync out of this path; an EXISTING account's sessions are ended above, by
        // AdminPasswordReset (RevokeLiveSessionsAsync).
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

        // BL-521 FIX4 item 1 — THE ADDRESS ONLY FINDS THE ACCOUNT. A platform administrator's e-mail never changes on
        // Platform (the identity key both services match the administrator by), so this sync never moves an account to
        // another address. FIX3's move (found by a previousEmail, then renamed) is gone; a body that still carries
        // previousEmail is not read, and an address no account has is 404 — nothing is renamed, nothing adopted.
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
        var wasActive = user.IsActive;
        var wasMarked = user.DeactivatedByAdministrator;
        user.UpdateProfile(firstName, lastName);

        // Merge WP-CT-LANE-BL521-MERGE-01 — BL-521 FIX4 item 5 with BL-529 FIX5: the account's state follows the RECORD's (an
        // Active record switches it on; any other status — or none, from an older Platform — leaves it alone), and when it
        // is switched on it is switched on as an administrator does (the deactivation mark goes with it). The address is
        // confirmed only by accepting the invitation (reset-password), never here.
        if (string.Equals(request.Status, PlatformActiveStatus, StringComparison.OrdinalIgnoreCase))
        {
            user.ActivateByAdministrator();
        }

        user.SetPlatformActorType(NormalizeActorType(request.ActorType));

        // BL-521 FIX4 item 4 — ROLES FIRST, THE ACCOUNT LAST: if the account write then fails, the roles the record took away
        // are already gone and the account was not switched back on — never an account re-enabled with roles it is losing.
        await PlatformAdministratorRoleConvergence.ConvergeProvisionedAsync(_userRoleRepository, _roleRepository, user.Id, PlatformTenantId, request.Roles, ct);

        // BL-529 FIX3/5/6 — and the account write is only the fields this sync changes (name, profile, switch, actor type), only
        // while the switch is still the one read: a whole write from the copy read above could put back a password hash an
        // administrator's reset replaced meanwhile, or switch back on an account deactivated meanwhile.
        if (!await _userRepository.TryWriteChangesAsync(user, state, PlatformTenantId,
                new UserWriteCondition(IsActive: wasActive, DeactivatedByAdministrator: wasMarked), ct))
        {
            return Conflict(new { message = "the account changed while it was being synced; try again" });
        }

        return NoContent();
    }

    [HttpPost("change-password/forced")]
    [Authorize]
    public async Task<IActionResult> ForcedChangePassword([FromBody] PlatformForcedChangePasswordRequest request, CancellationToken ct)
    {
        // BL-521 FIX3 item 4 — this door MINTS a platform session, so it is never wider than login. Before, it accepted any
        // valid JWT (a tenant_user token of a user in the platform tenant included) and looked at neither the account's own
        // state nor whether a change was pending: an account disabled in Auth but with a live Platform record could mint
        // itself a fresh platform session with every new token, indefinitely.
        if (!IsPlatformActorToken(User))
        {
            return CreateActionResultInstance(Response<AuthResponse>.Fail(
                "A platform administrator token is required.", [new ResponseError(ForcedChangeRequiresPlatformActorCode)], 403));
        }

        var user = await ResolveCurrentPlatformUserAsync(ct);
        if (user is null)
        {
            return CreateActionResultInstance(Response<AuthResponse>.Fail("Unauthorized.", 401));
        }

        if (!user.IsActive)
        {
            return CreateActionResultInstance(Response<AuthResponse>.Fail("User is inactive.", 401));
        }

        // Only a pending FORCED change opens this door (the flag login sets on the token); an ordinary change has its own.
        if (!user.MustChangePassword)
        {
            return CreateActionResultInstance(Response<AuthResponse>.Fail(
                "No password change is pending for this account. Use the regular password change.",
                [new ResponseError(ForcedChangeNotPendingCode)], 409));
        }

        // The same rule (and sentence) as login: an expired temporary password is replaced through a reset, not here.
        if (user.TemporaryPasswordExpiresAt is { } temporaryExpiry && temporaryExpiry <= DateTime.UtcNow)
        {
            return CreateActionResultInstance(Response<AuthResponse>.Fail(
                "Temporary password has expired. Please reset your password.",
                [new ResponseError(TemporaryPasswordExpiredCode)], 401));
        }

        // BL-521 FIX2 item 1 — this door MINTS a platform token, so the actor type comes from the administrator's Platform
        // RECORD (the same answer login and refresh use), never from Auth's copy on the user. Before: the copy went
        // through NormalizeActorType, which knew only "PartnerAdmin" — the stored "partner_admin" came out as
        // platform_admin, so a partner calling this with its own password got a platform_admin token and refresh row.
        // Item 3 — Platform's definitive no ends the sessions; no answer (503) ends nothing.
        var record = await _platformAdministratorStatusClient.GetActiveActorTypeAsync(user.Email, ct);
        if (record.Verdict == PlatformAdministratorRecordVerdict.Unavailable)
        {
            return CreateActionResultInstance(Response<AuthResponse>.Fail(PlatformAdministratorRecordAnswer.UnavailableMessage, 503));
        }

        if (record.Verdict != PlatformAdministratorRecordVerdict.Confirmed)
        {
            await _refreshTokenRepository.RevokeAllByUserAsync(user.Id, PlatformTenantId, ct);
            return CreateActionResultInstance(Response<AuthResponse>.Fail("Platform administrator account is inactive.", 401));
        }

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return CreateActionResultInstance(Response<AuthResponse>.Fail("Current password is incorrect.", 401));
        }

        // BL-521 FIX5 item 12 — the new password is checked FIRST: a change the policy refuses must not have rewritten the
        // account's roles on its way out (the convergence is a write; it belongs to the change that is actually made).
        await _passwordPolicyService.ValidateTenantPasswordAsync(PlatformTenantId, user.Id, request.NewPassword, "platform_forced_change", ct);

        // BL-521 FIX4 item 2 — as login and refresh: the roles the token will be minted with follow the record first (an
        // older Platform that sends no roles converges nothing). FIX5 item 3 — no provisioning default at mint.
        if (record.Roles is not null)
        {
            await PlatformAdministratorRoleConvergence.ConvergeAtMintAsync(_userRoleRepository, _roleRepository, user.Id, PlatformTenantId, record.Roles, ct);
        }

        // BL-521 FIX7 item 1 - Auth's copy of the actor type follows the record at every platform mint (see PlatformLoginCommandHandler);
        // the in-memory user is healed too: the replace of the document below would otherwise write the old value back.
        await PlatformAccountDiscriminator.EnsureAsync(_userRepository, user, record.ActorType!, _logger, ct);

        // BL-529 FIX2 — the state is taken AFTER the record convergence above, so the write below carries only the password
        // change, and only over the password just verified.
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

        var authResponse = await BuildPlatformAuthResponseAsync(user, record.ActorType!, request.RememberMe, ResolveRequestIp(HttpContext), ResolveUserAgent(HttpContext), false, ct);
        return CreateActionResultInstance(Response<AuthResponse>.Success(authResponse));
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] PlatformForgotPasswordRequest request, CancellationToken ct)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var asking = _clientAddress.Identify(HttpContext);
        if (!_passwordDoors.TryAcquireForgotPassword(asking.Key, normalizedEmail, asking.Identified))
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

        // BL-529 FIX5 — the link is compared FIRST. A request carrying the valid link is never counted and never refused (the
        // owner always gets through); only a link that does not match is counted, and only it can earn a 429. Platform's
        // status is asked only AFTER the link matched: an anonymous request with a wrong link never reaches Platform.
        var user = await _userRepository.GetByEmailAndTenantAsync(normalizedEmail, PlatformTenantId, ct);
        // FIX8 item 6 — an empty or blank token is a link that does not match (counted), never a hashing exception (an
        // uncounted 500 that would tell a live link apart).
        var linkMatches = user is not null &&
                          !string.IsNullOrWhiteSpace(request.Token) &&
                          !string.IsNullOrWhiteSpace(user.PasswordResetTokenHash) &&
                          user.PasswordResetTokenExpiresAt > DateTime.UtcNow &&
                          LinkHashesEqual(user.PasswordResetTokenHash, _refreshTokenHasher.Hash(request.Token));
        if (!linkMatches)
        {
            var asking = _clientAddress.Identify(HttpContext);
            if (!_passwordDoors.TryCountInvalidLinkAttempt(asking.Key, normalizedEmail, asking.Identified))
            {
                return TooManyRequests();
            }

            return CreateActionResultInstance(Response<NoContent>.Fail("Password reset token is invalid or expired.", 400));
        }

        if (!await _platformAdministratorStatusClient.IsActiveAsync(user!.Email, ct))
        {
            return CreateActionResultInstance(Response<NoContent>.Fail("Password reset token is invalid or expired.", 400));
        }

        // BL-529 FIX7 item 4 — as the tenant door: an account an administrator deactivated (an invitation's included — the
        // deactivation keeps its link) is not switched on by the link; "active AND marked" is never written. The link was
        // valid, so the attempt is not counted.
        // FIX8 item 1 (b) — marked, or switched off before BL-529 (inactive, no mark, not a pending invitation).
        if (user.IsDeactivatedByAdministrator())
        {
            return CreateActionResultInstance(Response<NoContent>.Fail(
                "This account has been deactivated by an administrator.",
                [new ResponseError(AuthRefusalCodes.AccountDeactivated)],
                409));
        }

        await _passwordPolicyService.ValidateTenantPasswordAsync(PlatformTenantId, user.Id, request.NewPassword, "platform_reset_password", ct);
        var redeemedTokenHash = user.PasswordResetTokenHash!;
        var redeemState = _userRepository.CaptureState(user);
        var wasActive = user.IsActive; // FIX9 (M4) — the switch as read: a plain deactivation landing meanwhile is not undone
        user.UpdatePassword(_passwordHasher.Hash(request.NewPassword));
        user.ClearPasswordChangeRequirement();
        user.Activate();
        user.ConfirmEmail();
        // BL-529 FIX2 — written only while this link is still the account's (not replaced by a newer reset, not used by a
        // parallel redemption).
        if (!await _userRepository.TryWriteChangesAsync(user, redeemState, PlatformTenantId, new UserWriteCondition(PasswordResetTokenHash: redeemedTokenHash, IsActive: wasActive, DeactivatedByAdministrator: false), ct))
        {
            return CreateActionResultInstance(Response<NoContent>.Fail("Password reset token is invalid or expired.", 400));
        }
        await _refreshTokenRepository.RevokeLiveSessionsAsync(user.Id, PlatformTenantId, SessionRevocationReasons.PasswordChanged, ct);
        return CreateActionResultInstance(Response<NoContent>.Success(204));
    }

    // BL-529 FIX6 — constant-time comparison (hygiene: the stored value is a keyed hash, so timing reveals nothing usable).
    private static bool LinkHashesEqual(string? stored, string presented)
        => stored is not null && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(stored), System.Text.Encoding.UTF8.GetBytes(presented));

    /// <summary>
    /// BL-521 FIX3 item 4 — the token is a platform actor's: <c>actor_type</c> platform_admin or partner_admin (the only two
    /// a platform token is minted with) and the platform tenant. A tenant_user token — even of a user that lives in the
    /// platform tenant — is not.
    /// </summary>
    private static bool IsPlatformActorToken(System.Security.Claims.ClaimsPrincipal principal)
    {
        var actorType = principal.FindFirst("actor_type")?.Value;
        var isPlatformActor = string.Equals(actorType, PlatformAdministratorRecordAnswer.PlatformAdminActorType, StringComparison.Ordinal)
                              || string.Equals(actorType, PlatformAdministratorRecordAnswer.PartnerAdminActorType, StringComparison.Ordinal);
        return isPlatformActor
               && Guid.TryParse(principal.FindFirst("tenant_id")?.Value, out var tenantId)
               && tenantId == PlatformTenantId;
    }

    private async Task<User?> ResolveCurrentPlatformUserAsync(CancellationToken ct)
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(userIdString, out var userId)
            ? await _userRepository.GetByIdAndTenantAsync(userId, PlatformTenantId, ct)
            : null;
    }

    /// <param name="actorType">The actor type the administrator's Platform record names (BL-521 FIX2 item 1) — never
    /// read from <c>user.PlatformActorType</c>.</param>
    private async Task<AuthResponse> BuildPlatformAuthResponseAsync(User user, string actorType, bool rememberMe, string requestIp, string? userAgent, bool requiresPasswordChange, CancellationToken ct)
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

    /// <summary>
    /// Auth's copy of the record's actor type, written by provision / sync (Platform sends the enum name). BL-521 FIX2
    /// item 1 — informational only: no door takes a token's actor type from this copy any more (each asks the Platform
    /// record). Both spellings are recognised, so the copy never turns a partner into platform_admin.
    /// </summary>
    private static string NormalizeActorType(string actorType) =>
        string.Equals(actorType, "PartnerAdmin", StringComparison.OrdinalIgnoreCase)
        || string.Equals(actorType, PlatformAdministratorRecordAnswer.PartnerAdminActorType, StringComparison.OrdinalIgnoreCase)
            ? PlatformAdministratorRecordAnswer.PartnerAdminActorType
            : PlatformAdministratorRecordAnswer.PlatformAdminActorType;

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
