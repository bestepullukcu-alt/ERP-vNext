using Diten.AuthService.Api.Models;
using Diten.AuthService.Application.Features.Roles.Commands;
using Diten.AuthService.Application.Features.Roles.Queries;
using Diten.AuthService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.AuthService.Api.Controllers;

[ApiController]
[Route("api/roles")]
[Authorize]
public sealed class RolesController : ControllerBase
{
    private readonly IMediator _mediator;

    public RolesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission("auth.roles.read")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAllRolesQuery(), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [HasPermission("auth.roles.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetRoleByIdQuery(id), ct);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("auth.roles.create")]
    public async Task<IActionResult> Create([FromBody] CreateRoleRequest request, CancellationToken ct)
    {
        var command = new CreateRoleCommand(request.Name, request.DisplayName, request.Description);
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [HasPermission("auth.roles.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRoleRequest request, CancellationToken ct)
    {
        var command = new UpdateRoleCommand(id, request.DisplayName, request.Description);
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission("auth.roles.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteRoleCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/permissions")]
    [HasPermission("auth.roles.assign-permission")]
    public async Task<IActionResult> AssignPermission(Guid id, [FromBody] AssignPermissionRequest request, CancellationToken ct)
    {
        await _mediator.Send(new AssignPermissionCommand(id, request.PermissionId), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/permissions/{permissionId:guid}")]
    [HasPermission("auth.roles.assign-permission")]
    public async Task<IActionResult> RevokePermission(Guid id, Guid permissionId, CancellationToken ct)
    {
        await _mediator.Send(new RevokePermissionCommand(id, permissionId), ct);
        return NoContent();
    }
}
