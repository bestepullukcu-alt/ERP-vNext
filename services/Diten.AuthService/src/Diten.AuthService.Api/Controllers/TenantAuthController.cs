using System.Security.Claims;
using Diten.AuthService.Api.Controllers.Common;
using Diten.AuthService.Api.Models;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Auth.Commands;
using Diten.AuthService.Application.Features.ESignReauth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.AuthService.Api.Controllers;

[Route("api/tenant-auth")]
public sealed class TenantAuthController : CustomBaseController
{
    private readonly IMediator _mediator;

    public TenantAuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] TenantLoginRequest request, CancellationToken ct)
    {
        var command = new LoginCommand(request.Email, request.Password, ResolveRequestIp(HttpContext), ResolveUserAgent(HttpContext), request.RememberMe);
        var result = await _mediator.Send(command, ct);
        return CreateActionResultInstance(result);
    }

    [HttpPost("mfa/verify")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyMfa([FromBody] VerifyMfaRequest request, CancellationToken ct)
    {
        var command = new VerifyMfaCommand(request.ChallengeId, request.Code, ResolveRequestIp(HttpContext), ResolveUserAgent(HttpContext));
        var result = await _mediator.Send(command, ct);
        return CreateActionResultInstance(result);
    }

    [HttpPost("mfa/resend")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendMfa([FromBody] ResendMfaRequest request, CancellationToken ct)
    {
        var command = new ResendMfaCommand(request.ChallengeId, ResolveRequestIp(HttpContext), ResolveUserAgent(HttpContext));
        var result = await _mediator.Send(command, ct);
        return CreateActionResultInstance(result);
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var command = new RegisterCommand(
            request.Email,
            request.Password,
            request.FirstName,
            request.LastName,
            ResolveRequestIp(HttpContext),
            ResolveUserAgent(HttpContext));
        var result = await _mediator.Send(command, ct);
        return CreateActionResultInstance(result);
    }

    // FIX-TENANT-MUSTCHANGEPW — forced first-login password change for tenant_user (mirrors PlatformAuthController's
    // change-password/forced). Identity is taken from the validated JWT (sub + tenant_id), not the body.
    [HttpPost("change-password/forced")]
    [Authorize]
    public async Task<IActionResult> ForcedChangePassword([FromBody] TenantForcedChangePasswordRequest request, CancellationToken ct)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        var tenantIdString = User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(userIdString, out var userId) || !Guid.TryParse(tenantIdString, out var tenantId))
        {
            return CreateActionResultInstance(Response<AuthResponse>.Fail("Unauthorized.", 401));
        }

        var command = new ForcedChangeTenantPasswordCommand(
            userId,
            tenantId,
            request.CurrentPassword,
            request.NewPassword,
            ResolveRequestIp(HttpContext),
            ResolveUserAgent(HttpContext),
            request.RememberMe);
        var result = await _mediator.Send(command, ct);
        return CreateActionResultInstance(result);
    }

    /// <summary>
    /// MOD-0022 E1 — re-enter the password for an e-signature → a short-lived, single-use ticket (ESIGN-BUNDLE §4.1).
    /// Identity from the validated JWT (sub, tenant_id, actor_type); opens no session and issues no token.
    /// </summary>
    [HttpPost("esign/reauth")]
    [Authorize]
    public async Task<IActionResult> ESignReauth([FromBody] ESignReauthRequest request, CancellationToken ct)
    {
        if (!TryResolveTenantUser(out var userId, out var tenantId, out var actorType))
        {
            return CreateActionResultInstance(Response<ESignReauthTicketDto>.Fail("Unauthorized.", 401));
        }

        var command = new ReauthenticateForESignCommand(userId, tenantId, actorType, request.Password, request.Intent,
            request.OpenWindowMinutes, ResolveRequestIp(HttpContext), ResolveUserAgent(HttpContext));
        return CreateActionResultInstance(await _mediator.Send(command, ct));
    }

    /// <summary>
    /// MOD-0022 E1 — a ticket from an open KS-3 window, without the password (ESIGN-BUNDLE §4.2). The signer re-types the
    /// user name (CT-D7).
    /// </summary>
    [HttpPost("esign/ticket")]
    [Authorize]
    public async Task<IActionResult> ESignWindowTicket([FromBody] ESignWindowTicketRequest request, CancellationToken ct)
    {
        if (!TryResolveTenantUser(out var userId, out var tenantId, out var actorType))
        {
            return CreateActionResultInstance(Response<ESignReauthTicketDto>.Fail("Unauthorized.", 401));
        }

        var command = new IssueESignWindowTicketCommand(userId, tenantId, actorType, request.Intent, request.UserName,
            ResolveRequestIp(HttpContext), ResolveUserAgent(HttpContext));
        return CreateActionResultInstance(await _mediator.Send(command, ct));
    }

    private bool TryResolveTenantUser(out Guid userId, out Guid tenantId, out string? actorType)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        var tenantIdString = User.FindFirst("tenant_id")?.Value;
        actorType = User.FindFirst("actor_type")?.Value;
        tenantId = Guid.Empty;
        return Guid.TryParse(userIdString, out userId) && Guid.TryParse(tenantIdString, out tenantId);
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
}
