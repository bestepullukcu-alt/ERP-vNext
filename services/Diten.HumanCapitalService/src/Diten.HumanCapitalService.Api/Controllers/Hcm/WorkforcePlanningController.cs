using Diten.HumanCapitalService.Api.Controllers.Common;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Features.WorkforcePlanning;
using Diten.HumanCapitalService.Application.Features.WorkforcePlanning.Commands;
using Diten.HumanCapitalService.Application.Features.WorkforcePlanning.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.HumanCapitalService.Api.Controllers.Hcm;

[Route("api/workforce-planning")]
[Authorize]
public sealed class WorkforcePlanningController : CustomBaseController
{
    private readonly IMediator _mediator;

    public WorkforcePlanningController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(WorkforcePlanningGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetWorkforcePlanningReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(WorkforcePlanningGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetWorkforcePlanningReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(WorkforcePlanningGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] WorkforcePlanningReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateWorkforcePlanningReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(WorkforcePlanningGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateWorkforcePlanningReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(WorkforcePlanningGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteWorkforcePlanningReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(WorkforcePlanningGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetWorkforcePlanningAuditMetadataQuery(id), ct));
}
