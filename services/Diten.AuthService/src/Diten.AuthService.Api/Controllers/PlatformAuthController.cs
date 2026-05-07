using Diten.AuthService.Api.Controllers.Common;
using Diten.AuthService.Api.Models;
using Diten.AuthService.Application.Features.Auth.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.AuthService.Api.Controllers;

[Route("api/platform-auth")]
public sealed class PlatformAuthController : CustomBaseController
{
    private readonly IMediator _mediator;

    public PlatformAuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] PlatformLoginRequest request, CancellationToken ct)
    {
        var command = new PlatformLoginCommand(request.Email, request.Password, ResolveRequestIp(HttpContext), ResolveUserAgent(HttpContext));
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
