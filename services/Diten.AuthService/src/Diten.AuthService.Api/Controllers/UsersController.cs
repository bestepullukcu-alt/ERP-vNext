using Diten.AuthService.Api.Controllers.Common;
using Diten.AuthService.Api.Models;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Features.Users.Commands;
using Diten.AuthService.Application.Features.Users.Queries;
using Diten.AuthService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    [HttpPost]
    [HasPermission("auth.users.create")]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var command = new CreateUserCommand(request.Email, request.Password, request.FirstName, request.LastName);
        var result = await _mediator.Send(command, ct);
        return CreateActionResultInstance(result);
    }

    [HttpPut("{id:guid}")]
    [HasPermission("auth.users.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var command = new UpdateUserCommand(id, request.FirstName, request.LastName, request.IsActive);
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
}
