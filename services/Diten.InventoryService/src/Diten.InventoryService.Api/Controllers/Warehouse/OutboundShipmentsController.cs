using Diten.InventoryService.Api.Warehouse;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.HandlingUnits;
using Diten.InventoryService.Application.Features.OutboundShipments;
using Diten.InventoryService.Application.Features.OutboundShipments.Commands;
using Diten.InventoryService.Application.Features.OutboundShipments.Queries;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.InventoryService.Api.Controllers.Warehouse;

/// <summary>
/// WAREHOUSE-OUTBOUND 1.0.0 (FROZEN, <c>warehouse-outbound.openapi.yaml</c>; pack AC-18) — the two GETs MVP-6 reads — plus the S2 outbound paths
/// of WMS draft §11 (DL-4: create with allocation, allocation preview, screen search, view, header edit, timeline).
/// <para>The frozen GETs stay as S1 served them in DL-4: the list is <c>{ "items": [], "contractVersion": "v1" }</c> (no <c>nextCursor</c> member —
/// MVP-6's reader refuses a JSON null, GAP-0183-03) and the detail 404 <c>UNKNOWN_OUTBOUND</c> with the frozen <c>Error</c>. The frozen
/// <c>{outboundId}</c> is a string with no <c>:guid</c> constraint (r23); the fixed segments <c>search</c> / <c>allocation-preview</c> win over
/// it; the S2 paths' own <c>{outboundId}</c> must be a canonical UUID (else 404 <c>UNKNOWN_OUTBOUND</c>).</para>
/// <para>Every action carries its <c>warehouse.outbound-shipments.*</c> key (the search and the view hold personal data — the ship-to — and need
/// <c>.read</c>, which the default Viewer does not get, Q-10a). No business rule here.</para>
/// </summary>
[Route("api/warehouse/outbound-shipments")]
[Authorize]
public sealed class OutboundShipmentsController : CustomBaseController
{
    public const string UnknownOutbound = WarehouseErrorCodes.UnknownOutbound;

    private static readonly string[] Statuses = ["Allocated", "Picked", "Packed", "ReadyToShip", "Shipped", "Cancelled"];

    private readonly IMediator _mediator;

    public OutboundShipmentsController(IMediator mediator) => _mediator = mediator;

    // ── frozen WAREHOUSE-OUTBOUND 1.0.0 ──────────────────────────────────────────────────────────────────

    // DL-5: the frozen reads are FILLED (pack §8.21, AC-30) — the six visible statuses, never an Item-level outbound (§8.24), no JSON null.
    [HttpGet]
    [HasPermission(WarehousePermissions.OutboundShipmentsRead)]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? warehouseId, [FromQuery] string? cursor, CancellationToken ct)
    {
        if (status is not null && !Statuses.Contains(status, StringComparer.Ordinal))
            return ContractFailure(StatusCodes.Status400BadRequest, InventoryErrorCodes.InvalidRequest, "status must be an OutboundStatus.");
        var page = await _mediator.Send(new GetFrozenOutboundListQuery(status, warehouseId, cursor), ct);
        return page.IsSuccessful && page.Data is { } body
            ? Ok(body)
            : ContractFailure(StatusCodes.Status400BadRequest, InventoryErrorCodes.InvalidRequest, page.Errors.FirstOrDefault() ?? "The request is malformed.");
    }

    [HttpGet("{outboundId}")]
    [HasPermission(WarehousePermissions.OutboundShipmentsRead)]
    public async Task<IActionResult> Get(string outboundId, CancellationToken ct)
    {
        var detail = await _mediator.Send(new GetFrozenOutboundQuery(outboundId), ct);
        return detail.IsSuccessful && detail.Data is { } body ? Ok(body) : ContractFailure(StatusCodes.Status404NotFound, UnknownOutbound, "Outbound shipment not found.");
    }

    // ── S2 reads (WMS draft §11) ─────────────────────────────────────────────────────────────────────────

    [HttpGet("search")]
    [HasPermission(WarehousePermissions.OutboundShipmentsRead)]
    public async Task<IActionResult> Search([FromQuery] string[]? status, [FromQuery] string? warehouseId, [FromQuery] string? q, [FromQuery] string? orderBy,
        [FromQuery] string? page, [FromQuery] string? pageSize, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetOutboundShipmentListQuery(status ?? [], warehouseId, q, orderBy, page, pageSize), ct));

    [HttpGet("{outboundId}/view")]
    [HasPermission(WarehousePermissions.OutboundShipmentsRead)]
    public async Task<IActionResult> View(string outboundId, CancellationToken ct)
        => WarehouseSupport.TryParseId(outboundId, out var id) ? WithETag(await _mediator.Send(new GetOutboundShipmentViewQuery(id), ct)) : NotFound();

    [HttpGet("{outboundId}/events")]
    [HasPermission(WarehousePermissions.OutboundShipmentsRead)]
    public async Task<IActionResult> Events(string outboundId, [FromQuery] string? page, [FromQuery] string? pageSize, CancellationToken ct)
        => WarehouseSupport.TryParseId(outboundId, out var id)
            ? CreateActionResultInstance(await _mediator.Send(new GetOutboundEventListQuery(id, page, pageSize), ct))
            : NotFound();

    [HttpPost("allocation-preview")]
    [HasPermission(WarehousePermissions.OutboundShipmentsCreate)]
    public async Task<IActionResult> Preview(CancellationToken ct)
    {
        var read = await WarehouseRequestReader.ReadAsync(Request.Body, WarehouseRequestReader.PreviewOutboundMembers, b => new { Outbound = b.Outbound() }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        return CreateActionResultInstance(await _mediator.Send(new PreviewOutboundAllocationQuery(body.Outbound), ct));
    }

    // ── S2 writes ────────────────────────────────────────────────────────────────────────────────────────

    [HttpPost]
    [HasPermission(WarehousePermissions.OutboundShipmentsCreate)]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var read = await WarehouseRequestReader.ReadAsync(Request.Body, WarehouseRequestReader.CreateOutboundMembers,
            b => new { Outbound = b.Outbound(), Key = b.String("idempotencyKey") }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        return WithETag(await _mediator.Send(new CreateOutboundShipmentCommand(body.Outbound, key), ct));
    }

    [HttpPut("{outboundId}")]
    [HasPermission(WarehousePermissions.OutboundShipmentsUpdate)]
    public async Task<IActionResult> Update(string outboundId, CancellationToken ct)
    {
        if (!WarehouseSupport.TryParseId(outboundId, out var id)) return NotFound();
        var read = await WarehouseRequestReader.ReadAsync(Request.Body, WarehouseRequestReader.UpdateOutboundMembers,
            b => new { Change = b.OutboundChange(), Key = b.String("idempotencyKey") }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return WithETag(await _mediator.Send(new UpdateOutboundShipmentCommand(id, body.Change, key, version), ct));
    }

    // ── DL-5: cancel, release, dispatch sync, packing (WMS draft §11) ─────────────────────────────────────

    [HttpPost("{outboundId}/cancel")]
    [HasPermission(WarehousePermissions.OutboundShipmentsCancel)]
    public async Task<IActionResult> Cancel(string outboundId, CancellationToken ct)
    {
        if (!WarehouseSupport.TryParseId(outboundId, out var id)) return NotFound();
        var read = await WarehouseRequestReader.ReadAsync(Request.Body, WarehouseRequestReader.ReasonMembers,
            b => new { Reason = b.String("reason"), Key = b.String("idempotencyKey") }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return WithETag(await _mediator.Send(new CancelOutboundShipmentCommand(id, body.Reason, key, version), ct));
    }

    [HttpPost("{outboundId}/release")]
    [HasPermission(WarehousePermissions.OutboundShipmentsRelease)]
    public async Task<IActionResult> Release(string outboundId, CancellationToken ct)
    {
        if (!WarehouseSupport.TryParseId(outboundId, out var id)) return NotFound();
        var read = await WarehouseRequestReader.ReadAsync(Request.Body, WarehouseRequestReader.ReleaseOutboundMembers,
            b => new { Reasons = b.ReleaseReasons(), Key = b.String("idempotencyKey") }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return WithETag(await _mediator.Send(new ReleaseOutboundShipmentCommand(id, body.Reasons, key, version), ct));
    }

    [HttpPost("{outboundId}/sync-dispatch")]
    [HasPermission(WarehousePermissions.OutboundShipmentsUpdate)]
    public async Task<IActionResult> SyncDispatch(string outboundId, CancellationToken ct)
    {
        if (!WarehouseSupport.TryParseId(outboundId, out var id)) return NotFound();
        var read = await WarehouseRequestReader.ReadAsync(Request.Body, WarehouseRequestReader.KeyMembers, b => new { Key = b.String("idempotencyKey") }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        var answer = await _mediator.Send(new SyncOutboundDispatchCommand(id, key, version), ct);
        if (answer.IsSuccessful && answer.Data is not null) Response.Headers.ETag = $"W/\"{answer.Data.Outbound.Version}\"";
        return CreateActionResultInstance(answer);
    }

    [HttpGet("{outboundId}/packages")]
    [HasPermission(WarehousePermissions.PackagesRead)]
    public async Task<IActionResult> Packages(string outboundId, CancellationToken ct)
        => WarehouseSupport.TryParseId(outboundId, out var id) ? CreateActionResultInstance(await _mediator.Send(new GetHandlingUnitListQuery(id), ct)) : NotFound();

    [HttpPost("{outboundId}/packages")]
    [HasPermission(WarehousePermissions.PackagesCreate)]
    public async Task<IActionResult> CreatePackage(string outboundId, CancellationToken ct)
    {
        if (!WarehouseSupport.TryParseId(outboundId, out var id)) return NotFound();
        var read = await WarehouseRequestReader.ReadAsync(Request.Body, WarehouseRequestReader.PackageMembers,
            b => new { Package = b.Package(), Key = b.String("idempotencyKey") }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        var answer = await _mediator.Send(new CreateHandlingUnitCommand(id, body.Package, key), ct);
        if (answer.IsSuccessful && answer.Data is not null) Response.Headers.ETag = $"W/\"{answer.Data.Version}\"";
        return CreateActionResultInstance(answer);
    }

    // ── plumbing ──────────────────────────────────────────────────────────────────────────────────────────

    private IActionResult WithETag(Response<OutboundShipmentViewDto> response)
    {
        if (response.IsSuccessful && response.Data is not null) Response.Headers.ETag = $"W/\"{response.Data.Version}\"";
        return CreateActionResultInstance(response);
    }

    private string? KeyError(string? body, out string key)
        => WarehouseSupport.KeyError(Request.Headers[InventoryContract.IdempotencyKeyHeader].ToString(), body, out key);

    private int? IfMatch()
    {
        var header = Request.Headers[InventoryContract.IfMatchHeader].ToString();
        return string.IsNullOrWhiteSpace(header) ? null : WarehouseSupport.ExpectedVersion(header);
    }

    private IActionResult Invalid(string? message)
        => ContractFailure(StatusCodes.Status400BadRequest, InventoryErrorCodes.InvalidRequest, message ?? "The request is malformed.");

    private new IActionResult NotFound() => ContractFailure(StatusCodes.Status404NotFound, UnknownOutbound, "Outbound shipment not found.");
}
