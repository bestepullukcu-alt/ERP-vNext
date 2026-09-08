using Diten.DataKnowledgeService.Api.Controllers.Common;
using Diten.DataKnowledgeService.Api.Security;
using Diten.DataKnowledgeService.Application.Features.DataWarehouseLakehouse;
using Diten.DataKnowledgeService.Application.Features.DataWarehouseLakehouse.Commands;
using Diten.DataKnowledgeService.Application.Features.DataWarehouseLakehouse.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.DataKnowledgeService.Api.Controllers.Dki;

[Route("api/data-warehouse-lakehouse")]
[Authorize]
public sealed class DataWarehouseLakehouseController : CustomBaseController
{
    private readonly IMediator _mediator;

    public DataWarehouseLakehouseController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(DataWarehouseLakehouseGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetDataWarehouseLakehouseReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(DataWarehouseLakehouseGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetDataWarehouseLakehouseReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(DataWarehouseLakehouseGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] DataWarehouseLakehouseReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateDataWarehouseLakehouseReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(DataWarehouseLakehouseGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateDataWarehouseLakehouseReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(DataWarehouseLakehouseGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteDataWarehouseLakehouseReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(DataWarehouseLakehouseGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetDataWarehouseLakehouseAuditMetadataQuery(id), ct));
}
