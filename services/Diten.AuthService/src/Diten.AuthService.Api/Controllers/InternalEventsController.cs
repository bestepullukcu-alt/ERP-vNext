using Diten.AuthService.Application.Common.Events;
using Diten.AuthService.Application.Common.Interfaces;
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
    private readonly ITenantLoginSettingsClient _tenantLoginSettingsClient;
    private readonly IPasswordPolicyService _passwordPolicyService;
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
        ITenantLoginSettingsClient tenantLoginSettingsClient,
        IPasswordPolicyService passwordPolicyService,
        ILogger<InternalEventsController> logger)
    {
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
        _tenantLoginSettingsClient = tenantLoginSettingsClient;
        _passwordPolicyService = passwordPolicyService;
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

        var loginSettings = await _tenantLoginSettingsClient.GetAsync(request.TenantId, ct);
        var temporaryPassword = _passwordPolicyService.GenerateTemporaryPassword(loginSettings);
        await _passwordPolicyService.ValidateTenantPasswordAsync(request.TenantId, null, temporaryPassword, "internal_temporary_password", ct);
        var passwordHash = _passwordHasher.Hash(temporaryPassword);
        var existingUser = await _userRepository.GetByEmailAndTenantAsync(request.Email.Trim().ToLowerInvariant(), request.TenantId, ct);

        var userProvisioned = existingUser is null;
        var user = existingUser ?? CreateUser(request, passwordHash);
        if (existingUser is null)
        {
            user.ConfirmEmail();
            await _userRepository.CreateAsync(user, ct);
        }
        else
        {
            user.UpdatePassword(passwordHash);
            user.Activate();
            user.ConfirmEmail();
            await _userRepository.UpdateForTenantAsync(user, request.TenantId, ct);
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

        return Ok(new TenantAdminInvitationProvisioningResponse(userProvisioned, temporaryPassword, "processed"));
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
        return new User(request.Email.Trim().ToLowerInvariant(), passwordHash, firstName, lastName, request.TenantId);
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

    public sealed record TenantAdminInvitationProvisioningResponse(
        bool UserProvisioned,
        string TemporaryPassword,
        string Message);
}
