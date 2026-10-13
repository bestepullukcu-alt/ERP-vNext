using Diten.InventoryService.Api.Traceability;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Traceability;
using Diten.InventoryService.Application.Features.Traceability.Queries;
using Diten.InventoryService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.InventoryService.Api.Controllers.Traceability;

/// <summary>
/// TRACE-BUNDLE — MOD-0174 (base <c>/api/trace</c>, contract <c>trace.openapi.yaml</c> 1.1.0), slice P1:
/// <list type="bullet">
/// <item>frozen v1: <c>getLot</c>, <c>listLots</c>, <c>createLot</c>, <c>getSerial</c>, <c>getGenealogy</c>,
/// <c>validateTrace</c>;</item>
/// <item>v1.1: <c>updateLot</c> (PATCH), <c>changeLotStatus</c>, <c>getLotHistory</c>, <c>getLotDetail</c>,
/// <c>listSerials</c>, <c>checkGs1Code</c>, <c>linkGenealogy</c>;</item>
/// <item>slice P4 (contract 1.2.0): <c>listManualLotGroups</c>, <c>commitManualLotMigration</c> (<c>trace.lots.migrate</c>),
/// <c>getMovementLotLinks</c> and <c>getManualLotMigrationStatus</c> (<c>trace.lots.read</c>).</item>
/// </list>
/// There is no PUT and no DELETE: a lot is never removed (pack §4). Every endpoint carries <c>[HasPermission]</c>, except
/// PATCH, whose key depends on the lot's status (Draft: <c>trace.lots.update</c>, otherwise
/// <c>trace.lots.update-released</c> — K-C): it admits a caller holding either and the handler decides per lot. A
/// response carrying one lot sends its version as <c>ETag: W/"n"</c>. No business rule here: read → send → render.
/// </summary>
[Route("api/trace")]
[Authorize]
public sealed class TraceController : CustomBaseController
{
    private readonly IMediator _mediator;

    public TraceController(IMediator mediator) => _mediator = mediator;

    // ── lots ──────────────────────────────────────────────────────────────────────────────────────────────

    [HttpGet("lots/{lotId}")]
    [HasPermission(TracePermissions.LotsRead)]
    public async Task<IActionResult> GetLot(string lotId, CancellationToken cancellationToken)
        => WithETag(await _mediator.Send(new GetLotQuery(lotId), cancellationToken));

    [HttpGet("lots")]
    [HasPermission(TracePermissions.LotsRead)]
    public async Task<IActionResult> ListLots(
        [FromQuery] string? itemId,
        [FromQuery] string? status,
        [FromQuery] string? expiryBefore,
        [FromQuery] string? q,
        [FromQuery] string? skuId,
        [FromQuery] string? skuLevel,
        [FromQuery] string? warn,
        [FromQuery] string? includeStock,
        [FromQuery] string? page,
        [FromQuery] string? pageSize,
        [FromQuery] string? sort,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ListLotsQuery(itemId, status, expiryBefore, q, skuId, skuLevel, warn, includeStock, page, pageSize, sort), cancellationToken));

    [HttpPost("lots")]
    [HasPermission(TracePermissions.LotsCreate)]
    public async Task<IActionResult> CreateLot(CancellationToken cancellationToken)
    {
        var read = await TraceRequestReader.ReadCreateAsync(Request.Body, Request.Headers[InventoryContract.IdempotencyKeyHeader].ToString(), cancellationToken);
        return read.Value is null ? Refused(read) : WithETag(await _mediator.Send(read.Value, cancellationToken));
    }

    [HttpPatch("lots/{lotId}")]
    public async Task<IActionResult> UpdateLot(string lotId, CancellationToken cancellationToken)
    {
        var canDraft = Holds(TracePermissions.LotsUpdate);
        var canReleased = Holds(TracePermissions.LotsUpdateReleased);
        if (!canDraft && !canReleased)
        {
            return ContractFailure(StatusCodes.Status403Forbidden, null, "The caller lacks the permission required for this operation.");
        }

        var read = await TraceRequestReader.ReadUpdateAsync(
            Request.Body, lotId, Request.Headers[InventoryContract.IfMatchHeader].ToString(), canDraft, canReleased, cancellationToken);
        return read.Value is null ? Refused(read) : WithETag(await _mediator.Send(read.Value, cancellationToken));
    }

    [HttpPost("lots/{lotId}/status")]
    [HasPermission(TracePermissions.LotsChangeStatus)]
    public async Task<IActionResult> ChangeLotStatus(string lotId, CancellationToken cancellationToken)
    {
        var read = await TraceRequestReader.ReadStatusAsync(Request.Body, lotId, Request.Headers[InventoryContract.IfMatchHeader].ToString(),
            Request.Headers[InventoryContract.IdempotencyKeyHeader].ToString(), cancellationToken);
        return read.Value is null ? Refused(read) : WithETag(await _mediator.Send(read.Value, cancellationToken));
    }

    [HttpGet("lots/{lotId}/history")]
    [HasPermission(TracePermissions.LotsRead)]
    public async Task<IActionResult> GetLotHistory(string lotId, [FromQuery] string? page, [FromQuery] string? pageSize, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetLotHistoryQuery(lotId, page, pageSize), cancellationToken));

    [HttpGet("lots/{lotId}/detail")]
    [HasPermission(TracePermissions.LotsRead)]
    public async Task<IActionResult> GetLotDetail(string lotId, CancellationToken cancellationToken)
        => WithETag(await _mediator.Send(new GetLotDetailQuery(lotId), cancellationToken));

    [HttpGet("lots/{lotId}/genealogy")]
    [HasPermission(TracePermissions.LotsRead)]
    public async Task<IActionResult> GetGenealogy(string lotId, [FromQuery] string? depth, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetGenealogyQuery(lotId, depth), cancellationToken));

    [HttpPost("lots/{lotId}/genealogy")]
    [HasPermission(TracePermissions.GenealogyLink)]
    public async Task<IActionResult> LinkGenealogy(string lotId, CancellationToken cancellationToken)
    {
        var read = await TraceRequestReader.ReadGenealogyAsync(Request.Body, lotId, Request.Headers[InventoryContract.IdempotencyKeyHeader].ToString(), cancellationToken);
        return read.Value is null ? Refused(read) : CreateActionResultInstance(await _mediator.Send(read.Value, cancellationToken));
    }

    [HttpPost("validate")]
    [HasPermission(TracePermissions.LotsRead)]
    public async Task<IActionResult> ValidateTrace(CancellationToken cancellationToken)
    {
        var read = await TraceRequestReader.ReadValidateAsync(Request.Body, cancellationToken);
        return read.Value is null ? Refused(read) : CreateActionResultInstance(await _mediator.Send(read.Value, cancellationToken));
    }

    // ── serials ───────────────────────────────────────────────────────────────────────────────────────────

    [HttpGet("serials/{serialId}")]
    [HasPermission(TracePermissions.SerialsRead)]
    public async Task<IActionResult> GetSerial(string serialId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetSerialQuery(serialId), cancellationToken));

    [HttpGet("serials")]
    [HasPermission(TracePermissions.SerialsRead)]
    public async Task<IActionResult> ListSerials(
        [FromQuery] string? q,
        [FromQuery] string? skuId,
        [FromQuery] string? skuLevel,
        [FromQuery] string? lotId,
        [FromQuery] string? status,
        [FromQuery] string? page,
        [FromQuery] string? pageSize,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new ListSerialsQuery(q, skuId, skuLevel, lotId, status, page, pageSize), cancellationToken));

    [HttpPost("serials/gs1-check")]
    [HasPermission(TracePermissions.LotsRead)]
    public async Task<IActionResult> CheckGs1Code(CancellationToken cancellationToken)
    {
        var read = await TraceRequestReader.ReadGs1Async(Request.Body, cancellationToken);
        return read.Value is null ? Refused(read) : CreateActionResultInstance(await _mediator.Send(read.Value, cancellationToken));
    }

    // ── typed-lot migration (WP-0174-P4) ──────────────────────────────────────────────────────────────────

    [HttpGet("migration/manual-lots")]
    [HasPermission(TracePermissions.LotsMigrate)]
    public async Task<IActionResult> ListManualLotGroups([FromQuery] string? view, [FromQuery] string? page, [FromQuery] string? pageSize, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new ListManualLotGroupsQuery(view, page, pageSize), cancellationToken));

    [HttpPost("migration/manual-lots/commit")]
    [HasPermission(TracePermissions.LotsMigrate)]
    public async Task<IActionResult> CommitManualLotMigration(CancellationToken cancellationToken)
    {
        var read = await TraceRequestReader.ReadCommitAsync(Request.Body, Request.Headers[InventoryContract.IdempotencyKeyHeader].ToString(), cancellationToken);
        return read.Value is null ? Refused(read) : CreateActionResultInstance(await _mediator.Send(read.Value, cancellationToken));
    }

    [HttpGet("migration/links")]
    [HasPermission(TracePermissions.LotsRead)]
    public async Task<IActionResult> GetMovementLotLinks([FromQuery] string? transactionId, [FromQuery] string? lotId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetMovementLotLinksQuery(transactionId, lotId), cancellationToken));

    [HttpGet("migration/status")]
    [HasPermission(TracePermissions.LotsRead)]
    public async Task<IActionResult> GetManualLotMigrationStatus(CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetManualLotMigrationStatusQuery(), cancellationToken));

    // ── helpers ───────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The same claim test as <see cref="PermissionAuthorizationHandler"/> (claim type "permission", case-insensitive).</summary>
    private bool Holds(string permission)
        => User.Claims.Any(c => c.Type == "permission" && string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase));

    private IActionResult Refused<T>(TraceRequestReader.ReadResult<T> read) where T : class
        => ContractFailure(StatusCodes.Status400BadRequest, read.ErrorCode, read.Error!);

    /// <summary>A successful single-lot answer carries its version as <c>ETag: W/"n"</c>.</summary>
    private IActionResult WithETag<T>(Response<T> response) where T : LotRefDto
    {
        if (response.IsSuccessful && response.Data?.ETagVersion is { } version)
        {
            Response.Headers.ETag = TraceMapping.ETag(version);
        }

        return CreateActionResultInstance(response);
    }
}
