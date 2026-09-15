using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.SkillsGapHeatmap;
using Diten.TalentEcosystemService.Application.Features.SkillsGapHeatmap.Commands;
using Diten.TalentEcosystemService.Application.Features.SkillsGapHeatmap.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/skills-gap-heatmap")]
[Authorize]
public sealed class SkillsGapHeatmapController : CustomBaseController
{
    private readonly IMediator _mediator;

    public SkillsGapHeatmapController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(SkillsGapHeatmapGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetSkillsGapHeatmapReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(SkillsGapHeatmapGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetSkillsGapHeatmapReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(SkillsGapHeatmapGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] SkillsGapHeatmapReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateSkillsGapHeatmapReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(SkillsGapHeatmapGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateSkillsGapHeatmapReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(SkillsGapHeatmapGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteSkillsGapHeatmapReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(SkillsGapHeatmapGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetSkillsGapHeatmapAuditMetadataQuery(id), ct));
}
