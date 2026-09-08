using Diten.DataKnowledgeService.Api.Controllers.Common;
using Diten.DataKnowledgeService.Api.Security;
using Diten.DataKnowledgeService.Application.Features.KpiCatalog;
using Diten.DataKnowledgeService.Application.Features.KpiCatalog.Commands;
using Diten.DataKnowledgeService.Application.Features.KpiCatalog.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.DataKnowledgeService.Api.Controllers.Dki;

[Route("api/kpi-catalog")]
[Authorize]
public sealed class KpiCatalogController : CustomBaseController
{
    private readonly IMediator _mediator;

    public KpiCatalogController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(KpiCatalogGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetKpiCatalogReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(KpiCatalogGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetKpiCatalogReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(KpiCatalogGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] KpiCatalogReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateKpiCatalogReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(KpiCatalogGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateKpiCatalogReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(KpiCatalogGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteKpiCatalogReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(KpiCatalogGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetKpiCatalogAuditMetadataQuery(id), ct));
}
