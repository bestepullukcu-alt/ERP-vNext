using Diten.AuthService.Api.Controllers.Common;
using Diten.AuthService.Api.Models;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Users.Commands;
using Diten.AuthService.Application.Features.Users.Queries;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Diten.AuthService.Api.Controllers;

[Route("api/users")]
[Authorize]
public sealed class UsersController : CustomBaseController
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission("auth.users.read")]
    public async Task<IActionResult> GetAll(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAllUsersQuery(page, pageSize), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [HasPermission("auth.users.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetUserByIdQuery(id), ct);
        return CreateActionResultInstance(result);
    }

    [HttpGet("{userId:guid}/lookup-validation")]
    [HasPermission("auth.users.lookup-validation")]
    public async Task<IActionResult> ValidateLookupReference(Guid userId, CancellationToken ct)
    {
        if (HasTenantHeaderJwtMismatch(User, Request.Headers))
        {
            return CreateActionResultInstance(
                Response<TenantUserLookupValidationDto>.Fail("Tenant context mismatch.", 400));
        }

        var result = await _mediator.Send(new ValidateUserReferenceQuery(userId), ct);
        return CreateActionResultInstance(result);
    }

    // ── WP-INFRA-AUTH-ACCOUNT-KIND-01 ─────────────────────────────────────────────────────────────────
    // Two reads under the ordinary tenant key auth.users.lookup, one write under the explicit-grant-only key
    // auth.users.account-kind.manage. "lookup" as a literal segment never collides with {id:guid}.

    // GET api/users/lookup?search=&limit=  →  [{ userId, displayLabel }] (no email, no roles)
    [HttpGet("lookup")]
    [HasPermission("auth.users.lookup")]
    public async Task<IActionResult> Lookup([FromQuery] string? search, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new LookupUsersQuery(search, limit), ct);
        return CreateActionResultInstance(result);
    }

    // GET api/users/{id}/account-assertion  →  { userId, active, accountKind, assertedAt, userUpdatedAt }; 404 for a
    // missing user AND for another tenant's user, byte-identical.
    [HttpGet("{id:guid}/account-assertion")]
    [HasPermission("auth.users.lookup")]
    public async Task<IActionResult> GetAccountAssertion(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAccountAssertionQuery(id), ct);
        return CreateActionResultInstance(result);
    }

    // POST api/users/{id}/account-kind { kind }  →  the new assertion; audited (authAuditLogs) with old→new.
    [HttpPost("{id:guid}/account-kind")]
    [HasPermission(ExplicitGrantOnlyPermissions.UsersAccountKindManage)]
    public async Task<IActionResult> SetAccountKind(Guid id, [FromBody] SetAccountKindRequest request, CancellationToken ct)
    {
        // TraceIdentifier IS the correlation id here: CorrelationIdMiddleware assigns it from X-Correlation-Id
        // (or mints one) before this action runs, so the audit row and the request log share it.
        var result = await _mediator.Send(new SetAccountKindCommand(id, request.Kind, HttpContext.TraceIdentifier), ct);
        return CreateActionResultInstance(result);
    }

    // GET api/users/{id}/display-label → { userId, displayLabel, labelState }; a display/history label ONLY — never
    // eligibility, activity, or authorization evidence (see UserDisplayLabelDto remarks). 404 for a missing user AND
    // for another tenant's user, byte-identical (same shape as account-assertion).
    [HttpGet("{id:guid}/display-label")]
    [HasPermission("auth.users.lookup")]
    public async Task<IActionResult> GetDisplayLabel(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetUserDisplayLabelQuery(id), ct);
        return CreateActionResultInstance(result);
    }

    [HttpPost]
    [HasPermission("auth.users.create")]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        // WP-INFRA-AUTH-ACCOUNT-KIND-01 — whether THIS caller may classify is read from the authenticated principal's
        // permission claims (the same claim set [HasPermission] evaluates), never from the request body. The handler
        // refuses a supplied kind without it (403 PERM_DENIED); an omitted kind never needs it.
        var callerCanManageAccountKind = User.HasClaim("permission", ExplicitGrantOnlyPermissions.UsersAccountKindManage);
        var command = new CreateUserCommand(
            request.Email, request.Password, request.FirstName, request.LastName,
            request.AccountKind, callerCanManageAccountKind);
        var result = await _mediator.Send(command, ct);
        return CreateActionResultInstance(result);
    }

    // Anonymous invitation redemption: the user (located by token hash) sets their own password.
    // No tenant header/JWT is required — see TenantResolutionMiddleware public-path allowance.
    [HttpPost("set-password")]
    [AllowAnonymous]
    public async Task<IActionResult> SetPassword([FromBody] SetTenantPasswordRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new SetTenantPasswordCommand(request.Email, request.Token, request.NewPassword), ct);
        return CreateActionResultInstance(result);
    }

    // Re-issue a fresh set-password token for a pending invited user and re-send the invitation email.
    [HttpPost("resend-invite/{id:guid}")]
    [HasPermission("auth.users.create")]
    public async Task<IActionResult> ResendInvite(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new ResendUserInvitationCommand(id), ct);
        return CreateActionResultInstance(result);
    }

    // Admin disable: deactivate + revoke refresh tokens (live sessions die).
    [HttpPost("{id:guid}/disable")]
    [HasPermission("auth.users.update")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new SetUserActiveStatusCommand(id, false), ct);
        return CreateActionResultInstance(result);
    }

    // Admin enable: reactivate the account.
    [HttpPost("{id:guid}/enable")]
    [HasPermission("auth.users.update")]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new SetUserActiveStatusCommand(id, true), ct);
        return CreateActionResultInstance(result);
    }

    // Admin reset password for an already-active user (forces a change + emails a set-password link).
    [HttpPost("{id:guid}/reset-password")]
    [HasPermission("auth.users.update")]
    public async Task<IActionResult> AdminResetPassword(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new AdminResetPasswordCommand(id), ct);
        return CreateActionResultInstance(result);
    }

    [HttpPut("{id:guid}")]
    [HasPermission("auth.users.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        // WP-AUTH-USER-KIND-UPDATE-01 — the classification right is read from the principal's claims exactly as Create
        // reads it, never from the body; the handler refuses a kind CHANGE without it (403 PERM_DENIED).
        var callerCanManageAccountKind = User.HasClaim("permission", ExplicitGrantOnlyPermissions.UsersAccountKindManage);
        var command = new UpdateUserCommand(
            id, request.FirstName, request.LastName, request.IsActive,
            request.AccountKind, callerCanManageAccountKind, HttpContext.TraceIdentifier);
        var result = await _mediator.Send(command, ct);
        return CreateActionResultInstance(result);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission("auth.users.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteUserCommand(id), ct);
        return CreateActionResultInstance(result);
    }

    [HttpPost("{id:guid}/roles")]
    [HasPermission("auth.users.assign-role")]
    public async Task<IActionResult> AssignRole(Guid id, [FromBody] AssignRoleRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new AssignRoleCommand(id, request.RoleId), ct);
        return CreateActionResultInstance(result);
    }

    [HttpDelete("{id:guid}/roles/{roleId:guid}")]
    [HasPermission("auth.users.assign-role")]
    public async Task<IActionResult> RevokeRole(Guid id, Guid roleId, CancellationToken ct)
    {
        var result = await _mediator.Send(new RevokeRoleCommand(id, roleId), ct);
        return CreateActionResultInstance(result);
    }

    public static bool HasTenantHeaderJwtMismatch(ClaimsPrincipal user, IHeaderDictionary headers)
    {
        var jwtTenantValue = user.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(jwtTenantValue, out var jwtTenantId))
        {
            return false;
        }

        if (!headers.TryGetValue("X-Tenant-Id", out var headerValue) || string.IsNullOrWhiteSpace(headerValue))
        {
            return false;
        }

        return Guid.TryParse(headerValue, out var headerTenantId) && headerTenantId != jwtTenantId;
    }
}
