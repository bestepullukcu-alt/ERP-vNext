using Diten.AuthService.Api.Models;
using Diten.AuthService.Application.Features.Auth.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.AuthService.Api.Controllers;

[ApiController]
[Route("api/platform-auth")]
public sealed class PlatformAuthController : ControllerBase
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
        var command = new PlatformLoginCommand(request.Email, request.Password);
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }
}
