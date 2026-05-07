using Diten.AuthService.Api.Controllers.Common;
using Diten.AuthService.Api.Models;
using Diten.AuthService.Application.Features.Auth.Commands;
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
        var command = new LoginCommand(request.Email, request.Password, ResolveRequestIp(HttpContext), ResolveUserAgent(HttpContext));
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
