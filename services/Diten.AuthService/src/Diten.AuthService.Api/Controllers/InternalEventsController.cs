using Diten.AuthService.Application.Common.Events;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Services;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Diten.AuthService.Api.Controllers;

[ApiController]
[Route("internal/events")]
public sealed class InternalEventsController : ControllerBase
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private const string TenantActivatedEventName = "tenant.activated";

    private const string EntitlementSyncActor = "tenant-provisioning";

    private readonly IInternalEventAuthService _internalEventAuthService;
    private readonly IRoleProvisioningService _roleProvisioningService;
    private readonly ITenantEntitlementClient _tenantEntitlementClient;
    private readonly IEntitlementPermissionSyncService _entitlementPermissionSyncService;
    private readonly IIntegrationEventInboxRepository _inboxRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly ITenantUserMembershipRepository _tenantUserMembershipRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenHasher _refreshTokenHasher;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUserAuditRecorder _audit;
    private readonly ILogger<InternalEventsController> _logger;

    public InternalEventsController(
        IInternalEventAuthService internalEventAuthService,
        IRoleProvisioningService roleProvisioningService,
        ITenantEntitlementClient tenantEntitlementClient,
        IEntitlementPermissionSyncService entitlementPermissionSyncService,
        IIntegrationEventInboxRepository inboxRepository,
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IUserRoleRepository userRoleRepository,
        ITenantUserMembershipRepository tenantUserMembershipRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IRefreshTokenHasher refreshTokenHasher,
        ILogger<InternalEventsController> logger,
        IRefreshTokenRepository refreshTokenRepository,
        IUserAuditRecorder audit)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _audit = audit;
        _internalEventAuthService = internalEventAuthService;
        _roleProvisioningService = roleProvisioningService;
        _tenantEntitlementClient = tenantEntitlementClient;
        _entitlementPermissionSyncService = entitlementPermissionSyncService;
        _inboxRepository = inboxRepository;
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _tenantUserMembershipRepository = tenantUserMembershipRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _refreshTokenHasher = refreshTokenHasher;
        _logger = logger;
    }

    [HttpPost("tenant-activated")]
    public async Task<IActionResult> TenantActivated([FromBody] TenantActivatedIntegrationEvent integrationEvent, CancellationToken ct)
    {
        if (!_internalEventAuthService.IsAuthorized(Request.Headers[InternalApiKeyHeader].FirstOrDefault()))
        {
            return Unauthorized(new { message = "internal authentication failed" });
        }

        if (!string.Equals(integrationEvent.EventName, TenantActivatedEventName, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "event_name must be tenant.activated" });
        }

        var inserted = await _inboxRepository.TryInsertAsync(
            integrationEvent.EventId,
            integrationEvent.EventName,
            integrationEvent.TenantId,
            ct);

        if (!inserted)
        {
            _logger.LogInformation(
                "Duplicate internal event ignored. EventId={EventId} TenantId={TenantId} EventName={EventName}",
                integrationEvent.EventId,
                integrationEvent.TenantId,
                integrationEvent.EventName);
            return Ok(new { status = "noop_duplicate" });
        }

        await _roleProvisioningService.EnsureDefaultRolesAsync(integrationEvent.TenantId, ct);
        await SyncEntitledModulesBestEffortAsync(integrationEvent.TenantId, ct);

        return Ok(new { status = "processed" });
    }

    [HttpPost("tenant-admin-invited")]
    public async Task<IActionResult> TenantAdminInvited([FromBody] TenantAdminInvitationProvisioningRequest request, CancellationToken ct)
    {
        if (!_internalEventAuthService.IsAuthorized(Request.Headers[InternalApiKeyHeader].FirstOrDefault()))
        {
            return Unauthorized(new { message = "internal authentication failed" });
        }

        if (request.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "tenantId and email are required" });
        }

        await _roleProvisioningService.EnsureDefaultRolesAsync(request.TenantId, ct);
        await SyncEntitledModulesBestEffortAsync(request.TenantId, ct);

        // BL-454 slice 2 stage D — the tenant administrator gets the same ONE-TIME set-password link every other invited
        // account gets (BL-529's redemption door, api/users/set-password), never a password: the temporary password this
        // door used to generate travelled in the response and was meant for the e-mail. Platform builds the address and
        // sends it; only the token's HASH is stored here.
        var setupToken = _tokenService.GenerateRefreshToken();
        var setupTokenHash = _refreshTokenHasher.Hash(setupToken);
        var setupExpiresAtUtc = DateTime.UtcNow.Add(InvitationLinkLifetime);
        var existingUser = await _userRepository.GetByEmailAndTenantAsync(request.Email.Trim().ToLowerInvariant(), request.TenantId, ct);

        var userProvisioned = existingUser is null;
        var user = existingUser ?? CreateUser(request, AdminPasswordReset.UnusableHash(_passwordHasher, _tokenService));
        if (existingUser is null)
        {
            IssueNewAccountInvitation(user, setupTokenHash, setupExpiresAtUtc);
            await _userRepository.CreateAsync(user, ct);
        }
        else
        {
            // BL-529 — an EXISTING account re-invited as tenant administrator is reset: the old password stops working
            // (a password nobody knows replaces it), every session of the account in this tenant ends, and the new link
            // is the only way back in (AdminPasswordReset; audited).
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var outcome = await AdminPasswordReset.ResetAsync(
                user,
                request.TenantId,
                AdminResetVia.TenantAdministratorReinvite,
                _ => AdminPasswordReset.UnusableHash(_passwordHasher, _tokenService),
                u =>
                {
                    u.ActivateByAdministrator(); // Platform's re-invitation is an administrator's activation
                    u.ConfirmEmail();
                    u.SetPasswordResetToken(setupTokenHash, setupExpiresAtUtc);
                    u.RequirePasswordChange(null);
                },
                c => _userRepository.GetByEmailAndTenantAsync(normalizedEmail, request.TenantId, c),
                afterWrite: null,
                _userRepository,
                _refreshTokenRepository,
                _audit,
                ct);
            if (!outcome.Succeeded)
            {
                return Conflict(new { message = "the account changed while it was being re-invited; try again" });
            }

            user = outcome.User!;
        }

        var memberships = await _tenantUserMembershipRepository.GetByUserIdAsync(user.Id, ct);
        if (!memberships.Any(x => x.TenantId == request.TenantId))
        {
            await _tenantUserMembershipRepository.CreateAsync(new TenantUserMembership(user.Id, request.TenantId, user.Email), ct);
        }

        var adminRole = await _roleRepository.GetByNameAndTenantAsync("Admin", request.TenantId, ct);
        if (adminRole is null)
        {
            return UnprocessableEntity(new { message = "tenant admin role is not available" });
        }

        if (!await _userRoleRepository.ExistsAsync(user.Id, adminRole.Id, request.TenantId, ct))
        {
            await _userRoleRepository.AssignAsync(new UserRole(user.Id, adminRole.Id, request.TenantId, "system"), ct);
        }

        return Ok(new TenantAdminInvitationProvisioningResponse(userProvisioned, setupToken, setupExpiresAtUtc, "processed"));
    }

    /// <summary>How long the tenant administrator's set-password link lives: the same 7 days as every tenant invitation
    /// (CreateUserCommandHandler, ResendUserInvitationCommandHandler).</summary>
    public static readonly TimeSpan InvitationLinkLifetime = TimeSpan.FromDays(7);

    /// <summary>
    /// A NEW account's invitation — not a reset (nothing existed to reset): the account waits, inactive and with no usable
    /// password, until the link is redeemed (which activates it and confirms the address), exactly like an invitation from
    /// the Users screen (CreateUserCommandHandler.CreateByInvitationAsync).
    /// </summary>
    private static void IssueNewAccountInvitation(User user, string setupTokenHash, DateTime setupExpiresAtUtc)
    {
        user.SetPasswordResetToken(setupTokenHash, setupExpiresAtUtc);
        user.Deactivate();
        user.RequirePasswordChange(null);
    }

    // Best-effort entitled-module → role-permission sync at provisioning. Pulls the tenant's effective entitled
    // modules from Platform and grants them. NEVER throws (provisioning must not fail on a Platform blip), and
    // SKIPS when the pull is empty so a transient/unreachable Platform can't strip existing Module-grants.
    private async Task SyncEntitledModulesBestEffortAsync(Guid tenantId, CancellationToken ct)
    {
        try
        {
            // FIX-3 — pull each entitled module WITH its declared catalog permission keys and reconcile by them
            // (namespace-agnostic). Per-module convention fallback is applied inside the sync service when a module
            // declares no keys, so workflow / goldencompact still get granted.
            var modules = await _tenantEntitlementClient.GetEntitledModulesWithPermissionKeysAsync(tenantId, ct);
            if (modules.Count == 0)
            {
                return; // nothing to grant, and never revoke on an empty/failed pull
            }

            await _entitlementPermissionSyncService.SyncTenantModulesWithKeysAsync(tenantId, modules, EntitlementSyncActor, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Entitled-module sync skipped for TenantId={TenantId}.", tenantId);
        }
    }

    private static User CreateUser(TenantAdminInvitationProvisioningRequest request, string passwordHash)
    {
        var (firstName, lastName) = SplitName(request.Name, request.Email);
        var user = new User(request.Email.Trim().ToLowerInvariant(), passwordHash, firstName, lastName, request.TenantId);
        // WP-INFRA-AUTH-ACCOUNT-KIND-01 — an invited tenant admin is a person in every real case, and is STILL
        // Unknown here: no automatic path classifies (owner decision 2026-09-11). Classification is an explicit act.
        user.SetAccountKind(Diten.AuthService.Domain.Enums.AccountKind.Unknown);
        return user;
    }

    private static (string FirstName, string LastName) SplitName(string? name, string email)
    {
        var normalized = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized) || string.Equals(normalized, email, StringComparison.OrdinalIgnoreCase))
        {
            return ("Admin", "User");
        }

        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return (parts[0], "Admin");
        }

        return (parts[0], string.Join(' ', parts.Skip(1)));
    }

    public sealed record TenantAdminInvitationProvisioningRequest(
        Guid TenantId,
        Guid AdminUserId,
        string TenantCode,
        string TenantName,
        string Email,
        string Name);

    /// <summary>The set-password token in clear travels ONLY here, to Platform over the internal key, which builds the
    /// link and sends it; it is never logged or stored in clear.</summary>
    public sealed record TenantAdminInvitationProvisioningResponse(
        bool UserProvisioned,
        string SetupToken,
        DateTime SetupExpiresAtUtc,
        string Message);
}
