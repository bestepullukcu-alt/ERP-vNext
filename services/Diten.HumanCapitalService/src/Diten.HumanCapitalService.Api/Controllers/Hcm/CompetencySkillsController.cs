using Diten.HumanCapitalService.Api.Controllers.Common;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Features.CompetencySkills;
using Diten.HumanCapitalService.Application.Features.CompetencySkills.Commands;
using Diten.HumanCapitalService.Application.Features.CompetencySkills.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.HumanCapitalService.Api.Controllers.Hcm;

[Route("api/competency-skills")]
[Authorize]
public sealed class CompetencySkillsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public CompetencySkillsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(CompetencySkillsGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetCompetencySkillsReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(CompetencySkillsGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetCompetencySkillsReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(CompetencySkillsGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] CompetencySkillsReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateCompetencySkillsReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(CompetencySkillsGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateCompetencySkillsReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(CompetencySkillsGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteCompetencySkillsReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(CompetencySkillsGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetCompetencySkillsAuditMetadataQuery(id), ct));
}
