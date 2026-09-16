using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.HiringRiskIndicators;
using Diten.TalentEcosystemService.Application.Features.HiringRiskIndicators.Commands;
using Diten.TalentEcosystemService.Application.Features.HiringRiskIndicators.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/hiring-risk-indicators")]
[Authorize]
public sealed class HiringRiskIndicatorsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public HiringRiskIndicatorsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(HiringRiskIndicatorsGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetHiringRiskIndicatorsReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(HiringRiskIndicatorsGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetHiringRiskIndicatorsReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(HiringRiskIndicatorsGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] HiringRiskIndicatorsReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateHiringRiskIndicatorsReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(HiringRiskIndicatorsGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateHiringRiskIndicatorsReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(HiringRiskIndicatorsGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteHiringRiskIndicatorsReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(HiringRiskIndicatorsGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetHiringRiskIndicatorsAuditMetadataQuery(id), ct));
}
