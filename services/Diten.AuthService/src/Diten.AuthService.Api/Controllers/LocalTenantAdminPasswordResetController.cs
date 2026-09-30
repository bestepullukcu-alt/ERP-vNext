using Diten.AuthService.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using System.Security.Claims;

namespace Diten.AuthService.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/platform-auth/local-tenant-admin-reset")]
public sealed class LocalTenantAdminPasswordResetController : ControllerBase
{
    private readonly IHostEnvironment _environment;
    private readonly IUserRepository _users;
    private readonly IUserRoleRepository _userRoles;
    private readonly ITenantUserMembershipRepository _memberships;
    private readonly IPlatformAdministratorStatusClient _platformAdmins;
    private readonly ITokenService _tokens;
    private readonly IRefreshTokenHasher _tokenHasher;
    private readonly ITenantUserInvitationEmailService _links;

    public LocalTenantAdminPasswordResetController(
        IHostEnvironment environment,
        IUserRepository users,
        IUserRoleRepository userRoles,
        ITenantUserMembershipRepository memberships,
        IPlatformAdministratorStatusClient platformAdmins,
        ITokenService tokens,
        IRefreshTokenHasher tokenHasher,
        ITenantUserInvitationEmailService links)
    {
        _environment = environment;
        _users = users;
        _userRoles = userRoles;
        _memberships = memberships;
        _platformAdmins = platformAdmins;
        _tokens = tokens;
        _tokenHasher = tokenHasher;
        _links = links;
    }

    [HttpPost("{tenantId:guid}")]
    public async Task<IActionResult> Reset(Guid tenantId, [FromBody] LocalTenantAdminResetRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["ENABLED"] = Environment.GetEnvironmentVariable("DITEN_LOCAL_TENANT_ADMIN_RESET_ENABLED"),
            ["TENANT_ID"] = Environment.GetEnvironmentVariable("DITEN_LOCAL_TENANT_ADMIN_RESET_TENANT_ID"),
            ["USER_ID"] = Environment.GetEnvironmentVariable("DITEN_LOCAL_TENANT_ADMIN_RESET_USER_ID"),
            ["OPERATOR_ID"] = Environment.GetEnvironmentVariable("DITEN_LOCAL_TENANT_ADMIN_RESET_OPERATOR_ID"),
            ["EMAIL"] = Environment.GetEnvironmentVariable("DITEN_LOCAL_TENANT_ADMIN_RESET_EMAIL")
        };
        if (!LocalTenantAdminPasswordResetBoundary.TryAuthorize(_environment, User, settings, tenantId,
                request.Email, out var userId, out var operatorId))
        {
            return NotFound();
        }

        var platformUser = await _users.GetByIdAndTenantAsync(operatorId,
            Guid.Parse("00000000-0000-0000-0000-000000000001"), ct);
        var actorEmail = User.FindAll(ClaimTypes.Email).Concat(User.FindAll("email")).Select(c => c.Value).ToArray();
        if (platformUser is null || !platformUser.IsActive || platformUser.IsDeleted ||
            actorEmail.Length != 1 || !string.Equals(actorEmail[0], platformUser.Email, StringComparison.OrdinalIgnoreCase) ||
            !await _platformAdmins.IsActiveAsync(platformUser.Email, ct))
        {
            return NotFound();
        }

        var target = await _users.GetByIdAndTenantAsync(userId, tenantId, ct);
        if (target is null || !target.IsActive || target.IsDeleted || !target.EmailConfirmed || target.MustChangePassword ||
            !string.Equals(target.Email, request.Email?.Trim(), StringComparison.OrdinalIgnoreCase) ||
            !(await _userRoles.GetRolesByUserAsync(userId, tenantId, ct)).Contains("Admin", StringComparer.Ordinal) ||
            !(await _memberships.GetByUserIdAsync(userId, ct)).Any(m => m.TenantId == tenantId && !m.IsDeleted))
        {
            return NotFound();
        }

        var token = _tokens.GenerateRefreshToken();
        target.SetPasswordResetToken(_tokenHasher.Hash(token), DateTime.UtcNow.AddDays(7));
        target.RequirePasswordChange(null);
        await _users.UpdateForTenantAsync(target, tenantId, ct);
        var url = _links.BuildTenantSetPasswordUrl(target.Email, token);
        return Ok(new { setupUrl = url });
    }
}

public sealed record LocalTenantAdminResetRequest(string? Email);
