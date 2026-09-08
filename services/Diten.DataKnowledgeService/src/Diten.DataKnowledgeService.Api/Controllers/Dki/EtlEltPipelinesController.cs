using Diten.DataKnowledgeService.Api.Controllers.Common;
using Diten.DataKnowledgeService.Api.Security;
using Diten.DataKnowledgeService.Application.Features.EtlEltPipelines;
using Diten.DataKnowledgeService.Application.Features.EtlEltPipelines.Commands;
using Diten.DataKnowledgeService.Application.Features.EtlEltPipelines.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.DataKnowledgeService.Api.Controllers.Dki;

[Route("api/etl-elt-pipelines")]
[Authorize]
public sealed class EtlEltPipelinesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public EtlEltPipelinesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(EtlEltPipelinesGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetEtlEltPipelinesReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(EtlEltPipelinesGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetEtlEltPipelinesReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(EtlEltPipelinesGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] EtlEltPipelinesReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateEtlEltPipelinesReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(EtlEltPipelinesGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateEtlEltPipelinesReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(EtlEltPipelinesGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteEtlEltPipelinesReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(EtlEltPipelinesGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetEtlEltPipelinesAuditMetadataQuery(id), ct));
}
