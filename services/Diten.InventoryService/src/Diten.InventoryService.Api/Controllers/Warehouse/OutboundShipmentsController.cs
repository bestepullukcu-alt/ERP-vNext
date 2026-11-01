using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.InventoryService.Api.Controllers.Warehouse;

/// <summary>
/// WAREHOUSE-OUTBOUND 1.0.0 (FROZEN, <c>warehouse-outbound.openapi.yaml</c>; pack AC-18) — the two GETs MVP-6 reads, served unchanged from S1.
/// S1 has no outbound shipment (S2 creates them): the list is <c>{ "items": [], "contractVersion": "v1" }</c> — no <c>nextCursor</c> member
/// (MVP-6's reader refuses a JSON null, GAP-0183-03) — and the detail is 404 <c>UNKNOWN_OUTBOUND</c> with the frozen <c>Error</c> (no details).
/// </summary>
[Route("api/warehouse/outbound-shipments")]
[Authorize]
public sealed class OutboundShipmentsController : CustomBaseController
{
    public const string UnknownOutbound = "UNKNOWN_OUTBOUND";

    private static readonly string[] Statuses = ["Allocated", "Picked", "Packed", "ReadyToShip", "Shipped", "Cancelled"];

    [HttpGet]
    [HasPermission(WarehousePermissions.OutboundShipmentsRead)]
    public IActionResult List([FromQuery] string? status, [FromQuery] string? warehouseId, [FromQuery] string? cursor)
        => status is not null && !Statuses.Contains(status, StringComparer.Ordinal)
            ? ContractFailure(StatusCodes.Status400BadRequest, InventoryErrorCodes.InvalidRequest, "status must be an OutboundStatus.")
            : Ok(new OutboundPage([], WarehouseContract.ContractVersion));

    [HttpGet("{outboundId}")]
    [HasPermission(WarehousePermissions.OutboundShipmentsRead)]
    public IActionResult Get(string outboundId)
        => ContractFailure(StatusCodes.Status404NotFound, UnknownOutbound, "Outbound shipment not found.");

    /// <summary>The frozen page; <c>nextCursor</c> is not a member at all (never written as null).</summary>
    public sealed record OutboundPage(IReadOnlyList<object> Items, string ContractVersion);
}
