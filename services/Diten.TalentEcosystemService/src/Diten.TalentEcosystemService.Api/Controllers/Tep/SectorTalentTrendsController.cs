using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.SectorTalentTrends;
using Diten.TalentEcosystemService.Application.Features.SectorTalentTrends.Commands;
using Diten.TalentEcosystemService.Application.Features.SectorTalentTrends.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/sector-talent-trends")]
[Authorize]
public sealed class SectorTalentTrendsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public SectorTalentTrendsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(SectorTalentTrendsGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetSectorTalentTrendsReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(SectorTalentTrendsGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetSectorTalentTrendsReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(SectorTalentTrendsGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] SectorTalentTrendsReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateSectorTalentTrendsReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(SectorTalentTrendsGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateSectorTalentTrendsReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(SectorTalentTrendsGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteSectorTalentTrendsReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(SectorTalentTrendsGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetSectorTalentTrendsAuditMetadataQuery(id), ct));
}
