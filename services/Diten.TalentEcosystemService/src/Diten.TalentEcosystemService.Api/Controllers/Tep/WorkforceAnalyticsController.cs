using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.WorkforceAnalytics;
using Diten.TalentEcosystemService.Application.Features.WorkforceAnalytics.Commands;
using Diten.TalentEcosystemService.Application.Features.WorkforceAnalytics.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/workforce-analytics")]
[Authorize]
public sealed class WorkforceAnalyticsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public WorkforceAnalyticsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(WorkforceAnalyticsGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetWorkforceAnalyticsReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(WorkforceAnalyticsGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetWorkforceAnalyticsReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(WorkforceAnalyticsGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] WorkforceAnalyticsReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateWorkforceAnalyticsReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(WorkforceAnalyticsGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateWorkforceAnalyticsReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(WorkforceAnalyticsGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteWorkforceAnalyticsReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(WorkforceAnalyticsGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetWorkforceAnalyticsAuditMetadataQuery(id), ct));
}
