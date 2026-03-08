using Diten.AuthService.Application.Features.Permissions.Queries;
using Diten.AuthService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.AuthService.Api.Controllers;

[ApiController]
[Route("api/permissions")]
[Authorize]
public sealed class PermissionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PermissionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission("auth.roles.read")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAllPermissionsQuery(), ct);
        return Ok(result);
    }

    [HttpGet("by-module/{module}")]
    [HasPermission("auth.roles.read")]
    public async Task<IActionResult> GetByModule(string module, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPermissionsByModuleQuery(module), ct);
        return Ok(result);
    }
}
