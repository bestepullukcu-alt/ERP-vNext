using Diten.DataKnowledgeService.Api.Controllers.Common;
using Diten.DataKnowledgeService.Api.Security;
using Diten.DataKnowledgeService.Application.Features.MetricDefinitionsOwnership;
using Diten.DataKnowledgeService.Application.Features.MetricDefinitionsOwnership.Commands;
using Diten.DataKnowledgeService.Application.Features.MetricDefinitionsOwnership.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.DataKnowledgeService.Api.Controllers.Dki;

[Route("api/metric-definitions-ownership")]
[Authorize]
public sealed class MetricDefinitionsOwnershipController : CustomBaseController
{
    private readonly IMediator _mediator;

    public MetricDefinitionsOwnershipController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(MetricDefinitionsOwnershipGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetMetricDefinitionsOwnershipReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(MetricDefinitionsOwnershipGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetMetricDefinitionsOwnershipReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(MetricDefinitionsOwnershipGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] MetricDefinitionsOwnershipReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateMetricDefinitionsOwnershipReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(MetricDefinitionsOwnershipGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateMetricDefinitionsOwnershipReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(MetricDefinitionsOwnershipGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteMetricDefinitionsOwnershipReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(MetricDefinitionsOwnershipGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetMetricDefinitionsOwnershipAuditMetadataQuery(id), ct));
}
