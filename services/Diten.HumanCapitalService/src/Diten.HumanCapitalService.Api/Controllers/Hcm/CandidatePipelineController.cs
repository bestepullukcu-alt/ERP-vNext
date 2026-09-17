using Diten.HumanCapitalService.Api.Controllers.Common;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Features.CandidatePipeline;
using Diten.HumanCapitalService.Application.Features.CandidatePipeline.Commands;
using Diten.HumanCapitalService.Application.Features.CandidatePipeline.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.HumanCapitalService.Api.Controllers.Hcm;

[Route("api/candidate-pipeline")]
[Authorize]
public sealed class CandidatePipelineController : CustomBaseController
{
    private readonly IMediator _mediator;

    public CandidatePipelineController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(CandidatePipelineGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetCandidatePipelineReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(CandidatePipelineGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetCandidatePipelineReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(CandidatePipelineGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] CandidatePipelineCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateCandidatePipelineReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(CandidatePipelineGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateCandidatePipelineReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(CandidatePipelineGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteCandidatePipelineReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(CandidatePipelineGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetCandidatePipelineAuditMetadataQuery(id), ct));
}
