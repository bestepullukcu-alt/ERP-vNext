using Diten.ProcurementService.Application.Features.InvoiceMatch;
using Diten.ProcurementService.Application.Features.InvoiceMatch.Commands;
using Diten.ProcurementService.Application.Features.InvoiceMatch.Queries;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.ProcurementService.Api.Controllers;

/// <summary>
/// Invoice Capture &amp; 3-Way Match API (MOD-0143). MATCH owned contract (invoice-match.openapi.yaml, server
/// /api/invoice-match) yüzeyini birebir uygular: POST /invoices (captureInvoice, Idempotency-Key),
/// GET /invoices/{invoiceId} (getInvoice), POST /invoices/{invoiceId}/match (runThreeWayMatch, Idempotency-Key),
/// GET /exceptions (listMatchExceptions; reasonCode + cursor), POST /exceptions/{exceptionId}/resolve
/// (resolveMatchException, Idempotency-Key). Delete/bulk-delete pack §14 yetki yüzeyi (additive). Tenant + LegalEntity
/// server-resolved (TenantResolutionMiddleware); payload'da YOK. Yanıtlar Response&lt;T&gt; zarfı içinde contract
/// alanlarını korur. Her aksiyon [HasPermission("procurement.invoice-match.&lt;action&gt;")] ile korunur (pack §14,
/// UAS-001). 3-way match tolerans POLICY-DRIVEN (ASSUMPTION-P2P-01; sabit sayı YOK). Ödeme YÜRÜTMEZ — yalnız match
/// outcome üretir (AP/payment = Finance/Treasury). G2A golden flow tail: PO → GRN → 0173 → invoice 3-way match.
/// </summary>
[Authorize]
[ApiController]
[Route("api/invoice-match")]
public sealed class InvoiceMatchController : CustomBaseController
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    private readonly IMediator _mediator;

    public InvoiceMatchController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>getInvoice — contract GET /api/invoice-match/invoices/{invoiceId}. Cross-tenant/LE → 404 NOT_FOUND.</summary>
    [HttpGet("invoices/{invoiceId}")]
    [HasPermission("procurement.invoice-match.read")]
    public async Task<IActionResult> GetInvoice(string invoiceId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetInvoiceByIdQuery(invoiceId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>captureInvoice — contract POST /api/invoice-match/invoices. Idempotency-Key ile idempotent.</summary>
    [HttpPost("invoices")]
    [HasPermission("procurement.invoice-match.create")]
    public async Task<IActionResult> Capture([FromBody] InvoiceUpsertBody body, CancellationToken cancellationToken)
    {
        var command = new CaptureInvoiceCommand(
            body.SupplierId,
            body.PoId,
            body.InvoiceNumber,
            body.Currency,
            body.Lines,
            body.SourceSystem,
            body.ExternalRef,
            ReadHeader(IdempotencyKeyHeader));

        var response = await _mediator.Send(command, cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>runThreeWayMatch — contract POST /api/invoice-match/invoices/{invoiceId}/match. Idempotency-Key ile idempotent.</summary>
    [HttpPost("invoices/{invoiceId}/match")]
    [HasPermission("procurement.invoice-match.match")]
    public async Task<IActionResult> Match(string invoiceId, [FromBody] RunThreeWayMatchBody? body, CancellationToken cancellationToken)
    {
        var command = new RunThreeWayMatchCommand(invoiceId, body?.ToleranceProfileId, ReadHeader(IdempotencyKeyHeader));
        var response = await _mediator.Send(command, cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>listMatchExceptions — contract GET /api/invoice-match/exceptions. reasonCode + cursor filtreli.</summary>
    [HttpGet("exceptions")]
    [HasPermission("procurement.invoice-match.read")]
    public async Task<IActionResult> ListExceptions(
        [FromQuery] MatchExceptionReason? reasonCode,
        [FromQuery] string? cursor,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new ListMatchExceptionsQuery(reasonCode, cursor), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>resolveMatchException — contract POST /api/invoice-match/exceptions/{exceptionId}/resolve. Idempotency-Key.</summary>
    [HttpPost("exceptions/{exceptionId}/resolve")]
    [HasPermission("procurement.invoice-match.resolve-exception")]
    public async Task<IActionResult> ResolveException(string exceptionId, [FromBody] ResolveMatchExceptionBody body, CancellationToken cancellationToken)
    {
        var command = new ResolveMatchExceptionCommand(
            exceptionId,
            body?.Decision ?? string.Empty,
            body?.Note,
            ReadHeader(IdempotencyKeyHeader));

        var response = await _mediator.Send(command, cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>Soft delete — pack §14 procurement.invoice-match.delete (yalnız Captured; eşleşmiş → 409). Hard delete YOK.</summary>
    [HttpDelete("invoices/{invoiceId}")]
    [HasPermission("procurement.invoice-match.delete")]
    public async Task<IActionResult> Delete(string invoiceId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new DeleteInvoiceCommand(invoiceId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>Toplu soft delete — pack §14 procurement.invoice-match.bulk-delete (yalnız Captured).</summary>
    [HttpDelete("invoices/bulk")]
    [HasPermission("procurement.invoice-match.bulk-delete")]
    public async Task<IActionResult> BulkDelete([FromBody] List<string> invoiceIds, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new BulkDeleteInvoiceCommand(invoiceIds ?? new List<string>()), cancellationToken);
        return CreateActionResultInstance(response);
    }

    private string? ReadHeader(string name)
        => Request.Headers.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.ToString()
            : null;
}
