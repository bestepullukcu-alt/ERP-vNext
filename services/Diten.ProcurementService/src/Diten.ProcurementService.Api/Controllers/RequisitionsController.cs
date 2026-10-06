using Diten.ProcurementService.Application.Features.Requisition;
using Diten.ProcurementService.Application.Features.Requisition.Commands;
using Diten.ProcurementService.Application.Features.Requisition.Queries;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.ProcurementService.Api.Controllers;

/// <summary>
/// Requisition (satın alma talebi) API (MOD-0141). REQUISITION-PO owned contract (requisition-po.openapi.yaml)
/// yüzeyini birebir uygular: POST/GET /requisitions, GET /requisitions/{requisitionId},
/// POST /requisitions/{requisitionId}/submit. Delete/bulk-delete pack §14 yetki yüzeyi (additive; ASSUMPTION-02).
/// Tenant + LegalEntity server-resolved (TenantResolutionMiddleware); payload'da YOK. Yanıtlar Response&lt;T&gt;
/// zarfı içinde contract alanlarını korur. Her aksiyon [HasPermission("procurement.requisitions.&lt;action&gt;")] ile
/// korunur (pack §14, UAS-001). State-değiştiren aksiyonlar (create/submit) Idempotency-Key header ile idempotent.
/// </summary>
[Authorize]
[ApiController]
[Route("api/requisitions")]
public sealed class RequisitionsController : CustomBaseController
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    private readonly IMediator _mediator;

    public RequisitionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>listRequisitions — contract GET /requisitions. Tenant+LE filtreli; opsiyonel status + cursor.</summary>
    [HttpGet]
    [HasPermission("procurement.requisitions.read")]
    public async Task<IActionResult> GetList(
        [FromQuery] RequisitionStatus? status,
        [FromQuery] string? cursor,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetRequisitionListQuery(status, cursor), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>getRequisition — contract GET /requisitions/{requisitionId}.</summary>
    [HttpGet("{requisitionId}")]
    [HasPermission("procurement.requisitions.read")]
    public async Task<IActionResult> GetById(string requisitionId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetRequisitionByIdQuery(requisitionId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>createRequisition — contract POST /requisitions (Draft). Idempotency-Key ile idempotent.</summary>
    [HttpPost]
    [HasPermission("procurement.requisitions.create")]
    public async Task<IActionResult> Create([FromBody] RequisitionUpsertRequest body, CancellationToken cancellationToken)
    {
        var command = new CreateRequisitionCommand(
            body.Lines,
            body.Justification,
            ReadHeader(IdempotencyKeyHeader));

        var response = await _mediator.Send(command, cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>submitRequisition — contract POST /requisitions/{requisitionId}/submit. Idempotency-Key ile idempotent.</summary>
    [HttpPost("{requisitionId}/submit")]
    [HasPermission("procurement.requisitions.submit")]
    public async Task<IActionResult> Submit(string requisitionId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new SubmitRequisitionCommand(requisitionId, ReadHeader(IdempotencyKeyHeader)), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>Soft delete — pack §14 procurement.requisitions.delete (yalnız Draft; contract dışı pack yetki yüzeyi).</summary>
    [HttpDelete("{requisitionId}")]
    [HasPermission("procurement.requisitions.delete")]
    public async Task<IActionResult> Delete(string requisitionId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new DeleteRequisitionCommand(requisitionId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>Toplu soft delete — pack §14 procurement.requisitions.bulk-delete (yalnız Draft).</summary>
    [HttpDelete("bulk")]
    [HasPermission("procurement.requisitions.bulk-delete")]
    public async Task<IActionResult> BulkDelete([FromBody] List<string> requisitionIds, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new BulkDeleteRequisitionCommand(requisitionIds ?? new List<string>()), cancellationToken);
        return CreateActionResultInstance(response);
    }

    private string? ReadHeader(string name)
        => Request.Headers.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.ToString()
            : null;
}
