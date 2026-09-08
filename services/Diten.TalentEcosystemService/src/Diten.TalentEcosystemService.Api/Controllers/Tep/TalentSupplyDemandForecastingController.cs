using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.TalentSupplyDemandForecasting;
using Diten.TalentEcosystemService.Application.Features.TalentSupplyDemandForecasting.Commands;
using Diten.TalentEcosystemService.Application.Features.TalentSupplyDemandForecasting.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/talent-supply-demand-forecasting")]
[Authorize]
public sealed class TalentSupplyDemandForecastingController : CustomBaseController
{
    private readonly IMediator _mediator;

    public TalentSupplyDemandForecastingController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(TalentSupplyDemandForecastingGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTalentSupplyDemandForecastingReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(TalentSupplyDemandForecastingGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTalentSupplyDemandForecastingReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(TalentSupplyDemandForecastingGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] TalentSupplyDemandForecastingReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateTalentSupplyDemandForecastingReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(TalentSupplyDemandForecastingGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateTalentSupplyDemandForecastingReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(TalentSupplyDemandForecastingGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteTalentSupplyDemandForecastingReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(TalentSupplyDemandForecastingGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTalentSupplyDemandForecastingAuditMetadataQuery(id), ct));
}
