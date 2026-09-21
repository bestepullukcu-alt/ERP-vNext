using Diten.ProcurementService.Application.Features.Sourcing;
using Diten.ProcurementService.Application.Features.Sourcing.Commands;
using Diten.ProcurementService.Application.Features.Sourcing.Queries;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.ProcurementService.Api.Controllers;

/// <summary>
/// Sourcing (RFQ/RFP) API (MOD-0145). SOURCING owned contract (sourcing.openapi.yaml) yüzeyini birebir uygular:
/// POST/GET /events, GET /events/{rfxId}, POST /events/{rfxId}/publish, POST/GET /events/{rfxId}/bids,
/// POST /events/{rfxId}/award. Tenant + LegalEntity server-resolved (TenantResolutionMiddleware); payload'da YOK.
/// Yanıtlar Response&lt;T&gt; zarfı içinde contract alanlarını korur. Her aksiyon
/// [HasPermission("procurement.sourcing.&lt;action&gt;")] ile korunur (pack §14, UAS-001). State-değiştiren aksiyonlar
/// (create/publish/bid/award) Idempotency-Key header ile idempotent.
/// </summary>
[Authorize]
[ApiController]
[Route("api/sourcing")]
public sealed class SourcingController : CustomBaseController
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    private readonly IMediator _mediator;

    public SourcingController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>listRfxEvents — contract GET /events. Tenant+LE filtreli; opsiyonel status + cursor.</summary>
    [HttpGet("events")]
    [HasPermission("procurement.sourcing.read")]
    public async Task<IActionResult> GetEvents(
        [FromQuery] RfxStatus? status,
        [FromQuery] string? cursor,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetRfxEventListQuery(status, cursor), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>getRfxEvent — contract GET /events/{rfxId}.</summary>
    [HttpGet("events/{rfxId}")]
    [HasPermission("procurement.sourcing.read")]
    public async Task<IActionResult> GetEvent(string rfxId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetRfxEventByIdQuery(rfxId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>createRfxEvent — contract POST /events (Draft). Idempotency-Key ile idempotent.</summary>
    [HttpPost("events")]
    [HasPermission("procurement.sourcing.create")]
    public async Task<IActionResult> CreateEvent([FromBody] RfxUpsertRequest body, CancellationToken cancellationToken)
    {
        var command = new CreateRfxEventCommand(
            body.Type,
            body.Title,
            body.ClosesAt,
            body.InvitedSupplierIds,
            body.Lines,
            ReadHeader(IdempotencyKeyHeader));

        var response = await _mediator.Send(command, cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>publishRfxEvent — contract POST /events/{rfxId}/publish. Idempotency-Key ile idempotent.</summary>
    [HttpPost("events/{rfxId}/publish")]
    [HasPermission("procurement.sourcing.publish")]
    public async Task<IActionResult> Publish(string rfxId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new PublishRfxEventCommand(rfxId, ReadHeader(IdempotencyKeyHeader)), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>listBids — contract GET /events/{rfxId}/bids.</summary>
    [HttpGet("events/{rfxId}/bids")]
    [HasPermission("procurement.sourcing.read")]
    public async Task<IActionResult> GetBids(string rfxId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetBidListQuery(rfxId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>submitBid — contract POST /events/{rfxId}/bids. Idempotency-Key ile idempotent.</summary>
    [HttpPost("events/{rfxId}/bids")]
    [HasPermission("procurement.sourcing.bid")]
    public async Task<IActionResult> SubmitBid(string rfxId, [FromBody] BidUpsertRequest body, CancellationToken cancellationToken)
    {
        var command = new SubmitBidCommand(
            rfxId,
            body.SupplierId,
            body.Lines,
            ReadHeader(IdempotencyKeyHeader));

        var response = await _mediator.Send(command, cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>awardRfxEvent — contract POST /events/{rfxId}/award. Idempotency-Key ile idempotent.</summary>
    [HttpPost("events/{rfxId}/award")]
    [HasPermission("procurement.sourcing.award")]
    public async Task<IActionResult> Award(string rfxId, [FromBody] AwardRequest body, CancellationToken cancellationToken)
    {
        var command = new AwardRfxEventCommand(
            rfxId,
            body.AwardedBidId,
            body.Rationale,
            ReadHeader(IdempotencyKeyHeader));

        var response = await _mediator.Send(command, cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>Soft delete — pack §14 procurement.sourcing.delete (yalnız Draft; contract dışı pack yetki yüzeyi).</summary>
    [HttpDelete("events/{rfxId}")]
    [HasPermission("procurement.sourcing.delete")]
    public async Task<IActionResult> Delete(string rfxId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new DeleteRfxEventCommand(rfxId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>Toplu soft delete — pack §14 procurement.sourcing.bulk-delete (yalnız Draft).</summary>
    [HttpDelete("events/bulk")]
    [HasPermission("procurement.sourcing.bulk-delete")]
    public async Task<IActionResult> BulkDelete([FromBody] List<string> rfxIds, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new BulkDeleteRfxEventCommand(rfxIds ?? new List<string>()), cancellationToken);
        return CreateActionResultInstance(response);
    }

    private string? ReadHeader(string name)
        => Request.Headers.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.ToString()
            : null;
}
