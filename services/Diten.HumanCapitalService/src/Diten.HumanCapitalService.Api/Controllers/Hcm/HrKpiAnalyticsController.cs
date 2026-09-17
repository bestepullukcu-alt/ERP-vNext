using Diten.HumanCapitalService.Api.Controllers.Common;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Features.HrKpiAnalytics;
using Diten.HumanCapitalService.Application.Features.HrKpiAnalytics.Commands;
using Diten.HumanCapitalService.Application.Features.HrKpiAnalytics.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.HumanCapitalService.Api.Controllers.Hcm;

[Route("api/hr-kpi-analytics")]
[Authorize]
public sealed class HrKpiAnalyticsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public HrKpiAnalyticsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(HrKpiAnalyticsGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetHrKpiAnalyticsReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(HrKpiAnalyticsGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetHrKpiAnalyticsReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(HrKpiAnalyticsGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] HrKpiAnalyticsReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateHrKpiAnalyticsReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(HrKpiAnalyticsGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateHrKpiAnalyticsReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(HrKpiAnalyticsGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteHrKpiAnalyticsReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(HrKpiAnalyticsGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetHrKpiAnalyticsAuditMetadataQuery(id), ct));
}
