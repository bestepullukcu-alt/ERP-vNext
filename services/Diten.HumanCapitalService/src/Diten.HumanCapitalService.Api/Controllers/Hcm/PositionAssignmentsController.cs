using Diten.HumanCapitalService.Api.Controllers.Common;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Features.PositionAssignments;
using Diten.HumanCapitalService.Application.Features.PositionAssignments.Commands;
using Diten.HumanCapitalService.Application.Features.PositionAssignments.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.HumanCapitalService.Api.Controllers.Hcm;

[Route("api/position-assignments")]
[Authorize]
public sealed class PositionAssignmentsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public PositionAssignmentsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(PositionAssignmentGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPositionAssignmentListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PositionAssignmentGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPositionAssignmentByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(PositionAssignmentGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] PositionAssignmentCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreatePositionAssignmentCommand(request), ct));

    [HttpPut("{id:guid}")]
    [HasPermission(PositionAssignmentGuard.ManagePermission)]
    public async Task<IActionResult> Update(Guid id, [FromBody] PositionAssignmentUpdateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdatePositionAssignmentCommand(id, request), ct));

    [HttpPost("{id:guid}/reference-link")]
    [HasPermission(PositionAssignmentGuard.ReferenceLinkManagePermission)]
    public async Task<IActionResult> UpdateReferenceLink(Guid id, [FromBody] PositionAssignmentReferenceLinkRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdatePositionAssignmentReferenceLinkCommand(id, request), ct));

    [HttpPatch("{id:guid}/archive")]
    [HasPermission(PositionAssignmentGuard.ArchivePermission)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ArchivePositionAssignmentCommand(id), ct));

    [HttpGet("health")]
    [HasPermission(PositionAssignmentGuard.ReadPermission)]
    public async Task<IActionResult> GetHealth(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPositionAssignmentHealthQuery(), ct));
}
