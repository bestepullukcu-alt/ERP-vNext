using Diten.AuthService.Api.Controllers.Common;
using Diten.AuthService.Application.Features.Permissions.Queries;
using Diten.AuthService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.AuthService.Api.Controllers;

[Route("api/permissions")]
[Authorize]
public sealed class PermissionsController : CustomBaseController
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
        return CreateActionResultInstance(result);
    }

    [HttpGet("by-module/{module}")]
    [HasPermission("auth.roles.read")]
    public async Task<IActionResult> GetByModule(string module, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPermissionsByModuleQuery(module), ct);
        return CreateActionResultInstance(result);
    }
}
