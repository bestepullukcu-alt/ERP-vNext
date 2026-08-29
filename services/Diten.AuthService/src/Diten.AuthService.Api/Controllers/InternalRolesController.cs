using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Microsoft.AspNetCore.Mvc;

namespace Diten.AuthService.Api.Controllers;

[ApiController]
[Route("internal/roles")]
public sealed class InternalRolesController : ControllerBase
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";

    private readonly IInternalEventAuthService _internalEventAuthService;
    private readonly IRoleRepository _roles;
    private readonly IUserRoleRepository _userRoles;

    public InternalRolesController(
        IInternalEventAuthService internalEventAuthService,
        IRoleRepository roles,
        IUserRoleRepository userRoles)
    {
        _internalEventAuthService = internalEventAuthService;
        _roles = roles;
        _userRoles = userRoles;
    }

    [HttpPost("resolve")]
    public async Task<IActionResult> Resolve([FromBody] ResolveRolesRequest request, CancellationToken ct)
    {
        if (!_internalEventAuthService.IsAuthorized(Request.Headers[InternalApiKeyHeader].FirstOrDefault()))
        {
            return Unauthorized(new { message = "internal authentication failed" });
        }

        if (request.TenantId == Guid.Empty || request.Names is null || request.Names.Count == 0)
        {
            return BadRequest(new { message = "tenantId and at least one role name are required" });
        }

        var requestedNames = request.Names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var roles = (await _roles.GetAllByTenantAsync(request.TenantId, ct))
            .Where(role => requestedNames.Contains(role.Name))
            .Select(role => new ResolvedRoleResponse(role.Id, role.Name, role.DisplayName))
            .OrderBy(role => role.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(roles);
    }

    [HttpPost("authorize")]
    public async Task<IActionResult> Authorize([FromBody] AuthorizeRoleRequest request, CancellationToken ct)
    {
        if (!_internalEventAuthService.IsAuthorized(Request.Headers[InternalApiKeyHeader].FirstOrDefault()))
        {
            return Unauthorized(new { message = "internal authentication failed" });
        }

        if (request.TenantId == Guid.Empty || request.UserId == Guid.Empty || request.RoleId == Guid.Empty)
        {
            return BadRequest(new { message = "tenantId, userId and roleId are required" });
        }

        var role = await _roles.GetByIdAndTenantAsync(request.RoleId, request.TenantId, ct);
        var authorized = role is not null
            && await _userRoles.ExistsAsync(request.UserId, request.RoleId, request.TenantId, ct);

        return Ok(new AuthorizeRoleResponse(authorized));
    }

    public sealed record ResolveRolesRequest(Guid TenantId, IReadOnlyList<string> Names);
    public sealed record ResolvedRoleResponse(Guid Id, string Name, string DisplayName);
    public sealed record AuthorizeRoleRequest(Guid TenantId, Guid UserId, Guid RoleId);
    public sealed record AuthorizeRoleResponse(bool Authorized);
}
