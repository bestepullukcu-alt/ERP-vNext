using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.TenantOrganization;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Application.Features.TenantOrganization.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

[Route("api/platform/position-assignments")]
[Authorize]
public sealed class PositionAssignmentsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public PositionAssignmentsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission("platform.position-assignments.read")]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPositionAssignmentsQuery(), ct));

    [HttpPost]
    [HasPermission("platform.position-assignments.create")]
    public async Task<IActionResult> Create([FromBody] PositionAssignmentRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreatePositionAssignmentCommand(request), ct));

    [HttpPut("{id:guid}")]
    [HasPermission("platform.position-assignments.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] PositionAssignmentRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdatePositionAssignmentCommand(id, request), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission("platform.position-assignments.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeletePositionAssignmentCommand(id), ct));
}
