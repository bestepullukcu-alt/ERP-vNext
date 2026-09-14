using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.SectorMobilityIntelligence;
using Diten.TalentEcosystemService.Application.Features.SectorMobilityIntelligence.Commands;
using Diten.TalentEcosystemService.Application.Features.SectorMobilityIntelligence.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/sector-mobility-intelligence")]
[Authorize]
public sealed class SectorMobilityIntelligenceController : CustomBaseController
{
    private readonly IMediator _mediator;

    public SectorMobilityIntelligenceController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(SectorMobilityIntelligenceGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetSectorMobilityIntelligenceReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(SectorMobilityIntelligenceGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetSectorMobilityIntelligenceReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(SectorMobilityIntelligenceGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] SectorMobilityIntelligenceReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateSectorMobilityIntelligenceReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(SectorMobilityIntelligenceGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateSectorMobilityIntelligenceReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(SectorMobilityIntelligenceGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteSectorMobilityIntelligenceReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(SectorMobilityIntelligenceGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetSectorMobilityIntelligenceAuditMetadataQuery(id), ct));
}
