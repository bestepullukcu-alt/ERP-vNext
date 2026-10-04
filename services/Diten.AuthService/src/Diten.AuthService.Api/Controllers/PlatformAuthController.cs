using Diten.AuthService.Api.Models;
using Diten.AuthService.Api.Controllers.Common;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
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

    /// <summary>BL-521 FIX3 item 4 — 403: the forced change door takes only a platform actor's token (platform or partner
    /// administrator, platform tenant).</summary>
    public const string ForcedChangeRequiresPlatformActorCode = "PLATFORM_FORCED_CHANGE_PLATFORM_ACTOR_REQUIRED";

    /// <summary>BL-521 FIX3 item 4 — 409: no forced change is pending on the account; an ordinary change has its own door.</summary>
    public const string ForcedChangeNotPendingCode = "PLATFORM_FORCED_CHANGE_NOT_PENDING";

    /// <summary>BL-521 FIX3 item 4 — 401: the temporary password has expired; a password reset replaces it.</summary>
    public const string TemporaryPasswordExpiredCode = "PLATFORM_TEMPORARY_PASSWORD_EXPIRED";

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
        IWebHostEnvironment environment)
    {
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
        var user = existingUser ?? new User(
            normalizedEmail,
            _passwordHasher.Hash(_tokenService.GenerateRefreshToken()),
            firstName,
            lastName,
            PlatformTenantId);
        user.SetUserName(request.UserName);

        if (existingUser is null)
        {
            user.ConfirmEmail();
            // WP-INFRA-AUTH-ACCOUNT-KIND-01 — a provisioned platform admin is created Unknown; an existing account's
            // kind is left exactly as it is (this path never rewrites a classification).
            user.SetAccountKind(Diten.AuthService.Domain.Enums.AccountKind.Unknown);
        }
        else
        {
            user.UpdateProfile(firstName, lastName);
            user.Activate();
            user.ConfirmEmail();
        }

        user.SetPlatformActorType(NormalizeActorType(request.ActorType));
        var setupToken = _tokenService.GenerateRefreshToken();
        user.SetPasswordResetToken(_refreshTokenHasher.Hash(setupToken), DateTime.UtcNow.AddHours(24));

        if (existingUser is null)
        {
            await _userRepository.CreateAsync(user, ct);
        }
        else
        {
            await _userRepository.UpdateForTenantAsync(user, PlatformTenantId, ct);
        }

        await SyncPlatformRolesAsync(user.Id, request.Roles, ct);
        await _refreshTokenRepository.RevokeAllByUserAsync(user.Id, PlatformTenantId, ct);
        var setupDelivery = await SendSetupLinkAsync(user.Email, setupToken, ct);

        return Ok(new
        {
            userProvisioned,
            message = "processed",
            setupUrl = setupDelivery.SetupUrl,
            emailSent = setupDelivery.EmailSent
        });
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

        // BL-521 FIX3 item 2(a) — the account is found by the address the Platform record had BEFORE this change
        // (PreviousEmail) and moved to the new one. Before, it was looked up by the NEW address only: changing another
        // administrator's e-mail answered 404 here while Platform had already saved the record, so the account stayed on the
        // old address and its next sign-in / refresh found no record — refused, every session revoked. A caller that sends
        // no previous address (an older Platform) is looked up by its address, as before. An unknown previous address is
        // NOT retried under the new one: whatever account already sits there is another account, never adopted.
        var lookupEmail = string.IsNullOrWhiteSpace(request.PreviousEmail) ? normalizedEmail : NormalizeEmail(request.PreviousEmail);
        var user = await _userRepository.GetByEmailAndTenantAsync(lookupEmail, PlatformTenantId, ct);
        if (user is null)
        {
            return NotFound(new { message = "platform user not found" });
        }

        var existingUserNameOwner = await _userRepository.GetByUserNameAndTenantAsync(normalizedUserName, PlatformTenantId, ct);
        if (existingUserNameOwner is not null && existingUserNameOwner.Id != user.Id)
        {
            return Conflict(new { message = "username already exists" });
        }

        if (!string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            var emailOwner = await _userRepository.GetByEmailAndTenantAsync(normalizedEmail, PlatformTenantId, ct);
            if (emailOwner is not null && emailOwner.Id != user.Id)
            {
                return Conflict(new { message = "email already exists" });
            }

            user.ChangeEmail(normalizedEmail);
        }

        var (firstName, lastName) = SplitName(request.DisplayName, normalizedEmail);
        user.SetUserName(request.UserName);
        user.UpdateProfile(firstName, lastName);
        user.Activate();
        user.ConfirmEmail();
        user.SetPlatformActorType(NormalizeActorType(request.ActorType));

        // Roles first, the account (with its new address) LAST: a failure part-way leaves the account where Platform's
        // record still points, and Platform's retry or put-back finds it there.
        await SyncPlatformRolesAsync(user.Id, request.Roles, ct);

        await _userRepository.UpdateForTenantAsync(user, PlatformTenantId, ct);

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

        await _passwordPolicyService.ValidateTenantPasswordAsync(PlatformTenantId, user.Id, request.NewPassword, "platform_forced_change", ct);
        user.UpdatePassword(_passwordHasher.Hash(request.NewPassword));
        user.ClearPasswordChangeRequirement();
        await _userRepository.UpdateForTenantAsync(user, PlatformTenantId, ct);
        await _refreshTokenRepository.RevokeAllByUserAsync(user.Id, PlatformTenantId, ct);

        var authResponse = await BuildPlatformAuthResponseAsync(user, record.ActorType!, request.RememberMe, ResolveRequestIp(HttpContext), ResolveUserAgent(HttpContext), false, ct);
        return CreateActionResultInstance(Response<AuthResponse>.Success(authResponse));
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] PlatformForgotPasswordRequest request, CancellationToken ct)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var user = string.IsNullOrWhiteSpace(normalizedEmail)
            ? null
            : await _userRepository.GetByEmailAndTenantAsync(normalizedEmail, PlatformTenantId, ct);

        if (user is not null && user.IsActive && await _platformAdministratorStatusClient.IsActiveAsync(user.Email, ct))
        {
            var resetToken = _tokenService.GenerateRefreshToken();
            user.SetPasswordResetToken(_refreshTokenHasher.Hash(resetToken), DateTime.UtcNow.AddHours(1));
            await _userRepository.UpdateForTenantAsync(user, PlatformTenantId, ct);
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
        user.UpdatePassword(_passwordHasher.Hash(request.NewPassword));
        user.ClearPasswordChangeRequirement();
        user.Activate();
        user.ConfirmEmail();
        await _userRepository.UpdateForTenantAsync(user, PlatformTenantId, ct);
        await _refreshTokenRepository.RevokeAllByUserAsync(user.Id, PlatformTenantId, ct);
        return CreateActionResultInstance(Response<NoContent>.Success(204));
    }

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
