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

    /// <summary>
    /// BL-454 stage D FIX2 (3) — the tenant-created EVENT's door: create only, by construction. It has no trigger to get
    /// wrong, and an AuthService older than this round answers 404 here — the caller fails closed and retries instead of
    /// reaching the operator's door, which resets an existing account.
    /// </summary>
    [HttpPost("tenant-admin-created")]
    public Task<IActionResult> TenantAdminCreated([FromBody] TenantAdminInvitationProvisioningRequest request, CancellationToken ct) =>
        ProvisionTenantAdministratorAsync(request, createOnly: true, auditTrigger: TriggerTenantCreatedEvent, ct);

    /// <summary>
    /// The operator's "Invite" door. <c>trigger=operator-invite</c> resets an existing account (BL-529, audited); anything
    /// else — or nothing (a caller that does not say who it is) — is create only.
    /// </summary>
    [HttpPost("tenant-admin-invited")]
    public Task<IActionResult> TenantAdminInvited([FromBody] TenantAdminInvitationProvisioningRequest request, CancellationToken ct)
    {
        var operatorInvite = string.Equals(request.Trigger, TriggerOperatorInvite, StringComparison.Ordinal);
        return ProvisionTenantAdministratorAsync(
            request, createOnly: !operatorInvite, auditTrigger: operatorInvite ? TriggerOperatorInvite : request.Trigger ?? "unspecified", ct);
    }

    private async Task<IActionResult> ProvisionTenantAdministratorAsync(
        TenantAdminInvitationProvisioningRequest request, bool createOnly, string auditTrigger, CancellationToken ct)
    {
        if (!_internalEventAuthService.IsAuthorized(Request.Headers[InternalApiKeyHeader].FirstOrDefault()))
        {
            return Unauthorized(new { message = "internal authentication failed" });
        }

        if (request.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "tenantId and email are required" });
        }

        // BL-454 stage D FIX1 (K3) — the platform's own tenant has platform administrators, never a tenant administrator.
        if (request.TenantId == PlatformTenantId)
        {
            return BadRequest(new { message = "the platform tenant has no tenant administrators", code = ReasonPlatformTenantRefused });
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var existingUser = await _userRepository.GetByEmailAndTenantAsync(normalizedEmail, request.TenantId, ct);

        // BL-454 stage D FIX1/FIX2 — the create-only path leaves an account that already exists EXACTLY as it is: no reset,
        // no reactivation, no session ended, no link, no role, no membership (a non-administrator account is never raised
        // to Admin by an event — K13). Asked first (K10): nothing of the tenant is provisioned for a call that does nothing.
        if (existingUser is not null && createOnly)
        {
            _logger.LogInformation(
                "tenant.admin_invitation.account_exists TenantId={TenantId} UserId={UserId} Trigger={Trigger}. Nothing changed.",
                request.TenantId, existingUser.Id, auditTrigger);
            return Ok(new TenantAdminInvitationProvisioningResponse(false, null, null, StatusAccountExists));
        }

        await _roleProvisioningService.EnsureDefaultRolesAsync(request.TenantId, ct);
        await SyncEntitledModulesBestEffortAsync(request.TenantId, ct);
        var adminRole = await _roleRepository.GetByNameAndTenantAsync("Admin", request.TenantId, ct);
        if (adminRole is null)
        {
            return UnprocessableEntity(new { message = "tenant admin role is not available" });
        }

        // BL-454 slice 2 stage D — the tenant administrator gets the same ONE-TIME set-password link every other invited
        // account gets (BL-529's redemption door, api/users/set-password), never a password. Only the token's HASH is stored.
        var setupToken = _tokenService.GenerateRefreshToken();
        var setupTokenHash = _refreshTokenHasher.Hash(setupToken);
        var setupExpiresAtUtc = DateTime.UtcNow.Add(InvitationLinkLifetime);

        if (existingUser is null)
        {
            var user = CreateUser(request, AdminPasswordReset.UnusableHash(_passwordHasher, _tokenService));
            IssueNewAccountInvitation(user, setupTokenHash, setupExpiresAtUtc);

            // BL-454 stage D FIX2 (1) — the ACCOUNT is written LAST: its membership and Admin role first, under its id. A
            // failure before the account exists leaves no account (a retry creates it whole; the grants it left name an id
            // nobody can sign in as); once the account exists, everything it needs exists. There is no half-made
            // administrator for a retry to stumble on.
            await GrantTenantAdministratorAsync(user.Id, user.Email, request.TenantId, adminRole, ct);
            var created = await _userRepository.CreateAsync(user, ct);
            // BL-456 — the account exists from here on: its row is written now (ids and facts only; no address, no token),
            // naming the REAL trigger (E2).
            await _audit.RecordAsync(UserAuditEvents.Invited, request.TenantId, created.Id,
                new Dictionary<string, object?>
                {
                    ["accountKind"] = created.AccountKind.ToString(),
                    ["via"] = "tenant-administrator-invitation",
                    ["trigger"] = auditTrigger
                }, ct);
            return Ok(new TenantAdminInvitationProvisioningResponse(true, setupToken, setupExpiresAtUtc, "processed"));
        }

        // The operator's "Invite" over an existing account — BL-529: the old password stops working (a password nobody knows
        // replaces it), every session of the account in this tenant ends, and the new link is the only way back in.
        var outcome = await AdminPasswordReset.ResetAsync(
            existingUser,
            request.TenantId,
            AdminResetVia.TenantAdministratorReinvite,
            _ => AdminPasswordReset.UnusableHash(_passwordHasher, _tokenService),
            u =>
            {
                u.ActivateByAdministrator(); // the operator's explicit re-invitation is an administrator's activation
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

        await GrantTenantAdministratorAsync(outcome.User!.Id, outcome.User.Email, request.TenantId, adminRole, ct);
        return Ok(new TenantAdminInvitationProvisioningResponse(false, setupToken, setupExpiresAtUtc, "processed"));
    }

    // Membership in the tenant and the Admin role — idempotent (each written only when missing).
    private async Task GrantTenantAdministratorAsync(Guid userId, string email, Guid tenantId, Role adminRole, CancellationToken ct)
    {
        var memberships = await _tenantUserMembershipRepository.GetByUserIdAsync(userId, ct);
        if (!memberships.Any(x => x.TenantId == tenantId))
        {
            await _tenantUserMembershipRepository.CreateAsync(new TenantUserMembership(userId, tenantId, email), ct);
        }

        if (!await _userRoleRepository.ExistsAsync(userId, adminRole.Id, tenantId, ct))
        {
            await _userRoleRepository.AssignAsync(new UserRole(userId, adminRole.Id, tenantId, "system"), ct);
        }
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

    /// <param name="Trigger"><see cref="TriggerOperatorInvite"/> (the operator's "Invite": an existing account is reset) or
    /// <see cref="TriggerTenantCreatedEvent"/> (create only). Anything else — or nothing — is create only.</param>
    public sealed record TenantAdminInvitationProvisioningRequest(
        Guid TenantId,
        Guid AdminUserId,
        string TenantCode,
        string TenantName,
        string Email,
        string Name,
        string? Trigger = null);

    public const string TriggerOperatorInvite = "operator-invite";
    public const string TriggerTenantCreatedEvent = "tenant-created-event";
    public const string StatusAccountExists = "account_exists";
    public const string ReasonPlatformTenantRefused = "PLATFORM_TENANT_REFUSED";
    private static readonly Guid PlatformTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>The set-password token in clear travels ONLY here, to Platform over the internal key, which builds the
    /// link and sends it; it is never logged or stored in clear.</summary>
    public sealed record TenantAdminInvitationProvisioningResponse(
        bool UserProvisioned,
        string? SetupToken,
        DateTime? SetupExpiresAtUtc,
        string Message);
}
