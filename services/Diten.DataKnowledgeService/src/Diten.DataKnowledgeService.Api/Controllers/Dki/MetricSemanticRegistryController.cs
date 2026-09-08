using Diten.DataKnowledgeService.Api.Controllers.Common;
using Diten.DataKnowledgeService.Api.Security;
using Diten.DataKnowledgeService.Application.Features.MetricSemanticRegistry;
using Diten.DataKnowledgeService.Application.Features.MetricSemanticRegistry.Commands;
using Diten.DataKnowledgeService.Application.Features.MetricSemanticRegistry.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.DataKnowledgeService.Api.Controllers.Dki;

[Route("api/metric-semantic-registry")]
[Authorize]
public sealed class MetricSemanticRegistryController : CustomBaseController
{
    private readonly IMediator _mediator;

    public MetricSemanticRegistryController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(MetricSemanticRegistryGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetMetricSemanticRegistryReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(MetricSemanticRegistryGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetMetricSemanticRegistryReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(MetricSemanticRegistryGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] MetricSemanticRegistryReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateMetricSemanticRegistryReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(MetricSemanticRegistryGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateMetricSemanticRegistryReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(MetricSemanticRegistryGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteMetricSemanticRegistryReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(MetricSemanticRegistryGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetMetricSemanticRegistryAuditMetadataQuery(id), ct));
}
