using Diten.DataKnowledgeService.Api.Controllers.Common;
using Diten.DataKnowledgeService.Api.Security;
using Diten.DataKnowledgeService.Application.Features.ScorecardsDashboards;
using Diten.DataKnowledgeService.Application.Features.ScorecardsDashboards.Commands;
using Diten.DataKnowledgeService.Application.Features.ScorecardsDashboards.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.DataKnowledgeService.Api.Controllers.Dki;

[Route("api/scorecards-dashboards")]
[Authorize]
public sealed class ScorecardsDashboardsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ScorecardsDashboardsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(ScorecardsDashboardsGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetScorecardsDashboardsReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(ScorecardsDashboardsGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetScorecardsDashboardsReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(ScorecardsDashboardsGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] ScorecardsDashboardsReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateScorecardsDashboardsReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(ScorecardsDashboardsGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateScorecardsDashboardsReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(ScorecardsDashboardsGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteScorecardsDashboardsReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(ScorecardsDashboardsGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetScorecardsDashboardsAuditMetadataQuery(id), ct));
}
