using Diten.AuthService.Api.Models;
using Diten.AuthService.Application.Features.Auth.Commands;
using Diten.AuthService.Application.Features.Users.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.AuthService.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var command = new LoginCommand(request.Email, request.Password, ResolveRequestIp(HttpContext), ResolveUserAgent(HttpContext));
        var result = await _mediator.Send(command, ct);
        return Ok(result);
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
        return CreatedAtAction(nameof(Me), result);
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var command = new RefreshTokenCommand(request.AccessToken, request.RefreshToken, ResolveRequestIp(HttpContext), ResolveUserAgent(HttpContext));
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var command = new LogoutCommand(request.AccessToken, request.RefreshToken, ResolveRequestIp(HttpContext));
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                           ?? User.FindFirst("sub")?.Value;
        
        if (string.IsNullOrEmpty(userIdString)) return Unauthorized();

        var command = new ChangePasswordCommand(userIdString, request.CurrentPassword, request.NewPassword);
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                           ?? User.FindFirst("sub")?.Value;
        
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId)) 
            return Unauthorized();

        var result = await _mediator.Send(new GetUserByIdQuery(userId), ct);
        return Ok(result);
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
