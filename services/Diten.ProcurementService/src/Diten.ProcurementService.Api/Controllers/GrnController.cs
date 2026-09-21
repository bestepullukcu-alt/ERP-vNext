using Diten.ProcurementService.Application.Features.Grn;
using Diten.ProcurementService.Application.Features.Grn.Commands;
using Diten.ProcurementService.Application.Features.Grn.Queries;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.ProcurementService.Api.Controllers;

/// <summary>
/// GRN API (MOD-0142 Receiving). GRN-EVENT owned-frozen contract (grn-event.openapi.yaml) yüzeyini birebir uygular:
/// POST /api/grn (recordGrn, Idempotency-Key), GET /api/grn/{grnId} (getGrn). Reverse/delete/bulk-delete pack §14
/// yetki yüzeyi (additive; ASSUMPTION-GRN-02/03). Tenant + LegalEntity server-resolved (TenantResolutionMiddleware);
/// payload'da YOK. Yanıtlar Response&lt;T&gt; zarfı içinde contract alanlarını korur. Her aksiyon
/// [HasPermission("procurement.grn.&lt;action&gt;")] ile korunur (pack §14, UAS-001). recordGrn Idempotency-Key ile
/// idempotent — replay mükerrer INVENTORY hareketi üretmez. Her satır INVENTORY'ye post edilir (GOODS_RECEIPT_PO);
/// GRN shadow stock TUTMAZ (envanter SoR MOD-0173). G2A golden flow: PO → GRN → 0173 posting → invoice 3-way match.
/// </summary>
[Authorize]
[ApiController]
[Route("api/grn")]
public sealed class GrnController : CustomBaseController
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    private readonly IMediator _mediator;

    public GrnController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>GetGrnList (pack §3). Tenant+LE filtreli; opsiyonel poId + status + cursor.</summary>
    [HttpGet]
    [HasPermission("procurement.grn.read")]
    public async Task<IActionResult> GetList(
        [FromQuery] string? poId,
        [FromQuery] GrnStatus? status,
        [FromQuery] string? cursor,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetGrnListQuery(poId, status, cursor), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>getGrn — contract GET /api/grn/{grnId}. Cross-tenant/LE → 404 UNKNOWN_GRN.</summary>
    [HttpGet("{grnId}")]
    [HasPermission("procurement.grn.read")]
    public async Task<IActionResult> GetById(string grnId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetGrnByIdQuery(grnId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>recordGrn — contract POST /api/grn (INVENTORY'ye post eder). Idempotency-Key ile idempotent.</summary>
    [HttpPost]
    [HasPermission("procurement.grn.create")]
    public async Task<IActionResult> Record([FromBody] GrnRequestBody body, CancellationToken cancellationToken)
    {
        var command = new RecordGrnCommand(
            body.PoId,
            body.WarehouseId,
            body.LocationId,
            body.Lines,
            body.SourceSystem,
            body.ExternalRef,
            ReadHeader(IdempotencyKeyHeader));

        var response = await _mediator.Send(command, cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>reverseGrn — INVENTORY REVERSAL ile (ASSUMPTION-GRN-02; contract-additive). Idempotency-Key ile idempotent.</summary>
    [HttpPost("{grnId}/reverse")]
    [HasPermission("procurement.grn.reverse")]
    public async Task<IActionResult> Reverse(string grnId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new ReverseGrnCommand(grnId, ReadHeader(IdempotencyKeyHeader)), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>Soft delete — pack §14 procurement.grn.delete (yalnız Draft; Posted → 409). Hard delete YOK.</summary>
    [HttpDelete("{grnId}")]
    [HasPermission("procurement.grn.delete")]
    public async Task<IActionResult> Delete(string grnId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new DeleteGrnCommand(grnId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>Toplu soft delete — pack §14 procurement.grn.bulk-delete (yalnız Draft).</summary>
    [HttpDelete("bulk")]
    [HasPermission("procurement.grn.bulk-delete")]
    public async Task<IActionResult> BulkDelete([FromBody] List<string> grnIds, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new BulkDeleteGrnCommand(grnIds ?? new List<string>()), cancellationToken);
        return CreateActionResultInstance(response);
    }

    private string? ReadHeader(string name)
        => Request.Headers.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.ToString()
            : null;
}
