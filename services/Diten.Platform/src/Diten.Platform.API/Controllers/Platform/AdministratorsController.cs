using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.PlatformAdministrators;
using Diten.Platform.Application.Features.PlatformAdministrators.Commands;
using Diten.Platform.Application.Features.PlatformAdministrators.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

[ApiController]
[Route("api/platform/administrators")]
[Authorize(Policy = "PlatformActor")]
public sealed class AdministratorsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public AdministratorsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission("platform.administrators.read")]
    public async Task<IActionResult> GetItems([FromQuery] PlatformAdministratorFilterRequest filter, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetPlatformAdministratorsQuery(filter), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("stats")]
    [HasPermission("platform.administrators.read")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetPlatformAdministratorStatsQuery(), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{id:guid}")]
    [HasPermission("platform.administrators.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetPlatformAdministratorByIdQuery(id), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost]
    [HasPermission("platform.administrators.create")]
    public async Task<IActionResult> Invite([FromBody] InvitePlatformAdministratorRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new InvitePlatformAdministratorCommand(request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPut("{id:guid}")]
    [HasPermission("platform.administrators.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePlatformAdministratorRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdatePlatformAdministratorCommand(id, request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{id:guid}/suspend")]
    [HasPermission("platform.administrators.suspend")]
    public async Task<IActionResult> Suspend(Guid id, [FromBody] PlatformAdministratorStatusRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new SuspendPlatformAdministratorCommand(id, request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{id:guid}/reactivate")]
    [HasPermission("platform.administrators.suspend")]
    public async Task<IActionResult> Reactivate(Guid id, [FromBody] PlatformAdministratorStatusRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new ReactivatePlatformAdministratorCommand(id, request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission("platform.administrators.update")]
    public async Task<IActionResult> Delete(Guid id, [FromBody] PlatformAdministratorVersionRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new DeletePlatformAdministratorCommand(id, request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpDelete("bulk")]
    [HasPermission("platform.administrators.update")]
    public async Task<IActionResult> BulkDelete([FromBody] IReadOnlyList<PlatformAdministratorBulkDeleteItemRequest> request, CancellationToken ct)
    {
        var response = await _mediator.Send(new BulkDeletePlatformAdministratorsCommand(request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{id:guid}/roles")]
    [HasPermission("platform.administrators.assign-roles")]
    public async Task<IActionResult> AssignRoles(Guid id, [FromBody] AssignPlatformAdministratorRolesRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new AssignPlatformAdministratorRolesCommand(id, request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{id:guid}/resend-invite")]
    [HasPermission("platform.administrators.create")]
    public async Task<IActionResult> ResendInvite(Guid id, [FromBody] PlatformAdministratorVersionRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new ResendPlatformAdministratorInviteCommand(id, request), ct);
        return CreateActionResultInstance(response);
    }
}
