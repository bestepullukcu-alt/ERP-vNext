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
        user.SetUserName(request.UserName);
        user.UpdateProfile(firstName, lastName);
        user.Activate();
        user.ConfirmEmail();
        user.SetPlatformActorType(NormalizeActorType(request.ActorType));

        await _userRepository.UpdateForTenantAsync(user, PlatformTenantId, ct);

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
        user.UpdatePassword(_passwordHasher.Hash(request.NewPassword));
        user.ClearPasswordChangeRequirement();
        await _userRepository.UpdateForTenantAsync(user, PlatformTenantId, ct);
        await _refreshTokenRepository.RevokeAllByUserAsync(user.Id, PlatformTenantId, ct);

        var authResponse = await BuildPlatformAuthResponseAsync(user, request.RememberMe, ResolveRequestIp(HttpContext), ResolveUserAgent(HttpContext), false, ct);
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
