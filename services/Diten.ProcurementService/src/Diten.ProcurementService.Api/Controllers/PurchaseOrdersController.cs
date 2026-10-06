using Diten.ProcurementService.Application.Features.PurchaseOrder;
using Diten.ProcurementService.Application.Features.PurchaseOrder.Commands;
using Diten.ProcurementService.Application.Features.PurchaseOrder.Queries;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.ProcurementService.Api.Controllers;

/// <summary>
/// Purchase Order API (MOD-0141). REQUISITION-PO owned contract (requisition-po.openapi.yaml) yüzeyini birebir
/// uygular: POST/GET /purchase-orders, GET /purchase-orders/{poId}, POST /purchase-orders/{poId}/approve.
/// Delete/bulk-delete pack §14 yetki yüzeyi (additive; ASSUMPTION-02). Tenant + LegalEntity server-resolved
/// (TenantResolutionMiddleware); payload'da YOK. Yanıtlar Response&lt;T&gt; zarfı içinde contract alanlarını korur.
/// Her aksiyon [HasPermission("procurement.purchase-orders.&lt;action&gt;")] ile korunur (pack §14, UAS-001).
/// State-değiştiren aksiyonlar (create/approve) Idempotency-Key header ile idempotent. Approved PO = G2A upstream.
/// </summary>
[Authorize]
[ApiController]
[Route("api/purchase-orders")]
public sealed class PurchaseOrdersController : CustomBaseController
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    private readonly IMediator _mediator;

    public PurchaseOrdersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>listPurchaseOrders — contract GET /purchase-orders. Tenant+LE filtreli; opsiyonel supplierId + status + cursor.</summary>
    [HttpGet]
    [HasPermission("procurement.purchase-orders.read")]
    public async Task<IActionResult> GetList(
        [FromQuery] string? supplierId,
        [FromQuery] PoStatus? status,
        [FromQuery] string? cursor,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetPurchaseOrderListQuery(supplierId, status, cursor), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>getPurchaseOrder — contract GET /purchase-orders/{poId} (GRN 0142 poId/poLineId ile okur).</summary>
    [HttpGet("{poId}")]
    [HasPermission("procurement.purchase-orders.read")]
    public async Task<IActionResult> GetById(string poId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetPurchaseOrderByIdQuery(poId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>createPurchaseOrder — contract POST /purchase-orders (Draft). Idempotency-Key ile idempotent.</summary>
    [HttpPost]
    [HasPermission("procurement.purchase-orders.create")]
    public async Task<IActionResult> Create([FromBody] PurchaseOrderUpsertRequest body, CancellationToken cancellationToken)
    {
        var command = new CreatePurchaseOrderCommand(
            body.SupplierId,
            body.RequisitionId,
            body.Currency,
            body.Lines,
            body.SourceSystem,
            body.ExternalRef,
            ReadHeader(IdempotencyKeyHeader));

        var response = await _mediator.Send(command, cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>approvePurchaseOrder — contract POST /purchase-orders/{poId}/approve. Idempotency-Key ile idempotent.</summary>
    [HttpPost("{poId}/approve")]
    [HasPermission("procurement.purchase-orders.approve")]
    public async Task<IActionResult> Approve(string poId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new ApprovePurchaseOrderCommand(poId, ReadHeader(IdempotencyKeyHeader)), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>Soft delete — pack §14 procurement.purchase-orders.delete (yalnız Draft; contract dışı pack yetki yüzeyi).</summary>
    [HttpDelete("{poId}")]
    [HasPermission("procurement.purchase-orders.delete")]
    public async Task<IActionResult> Delete(string poId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new DeletePurchaseOrderCommand(poId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>Toplu soft delete — pack §14 procurement.purchase-orders.bulk-delete (yalnız Draft).</summary>
    [HttpDelete("bulk")]
    [HasPermission("procurement.purchase-orders.bulk-delete")]
    public async Task<IActionResult> BulkDelete([FromBody] List<string> poIds, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new BulkDeletePurchaseOrderCommand(poIds ?? new List<string>()), cancellationToken);
        return CreateActionResultInstance(response);
    }

    private string? ReadHeader(string name)
        => Request.Headers.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.ToString()
            : null;
}
