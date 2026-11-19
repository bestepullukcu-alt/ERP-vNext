using Diten.InventoryService.Api.Warehouse;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.HandlingUnits;
using Diten.InventoryService.Application.Features.OutboundShipments;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.InventoryService.Api.Controllers.Warehouse;

/// <summary>
/// WMS-BUNDLE v1.1.0 (draft §11) — <c>PUT /api/warehouse/packages/{packageId}</c> (an Open package's content / weight / dimensions — never stock)
/// and <c>POST /api/warehouse/packages/{packageId}/close</c> (DL-5). The package list and create live on the outbound
/// (<c>/outbound-shipments/{outboundId}/packages</c>). If-Match on the package. No answer carries the ship-to (Q-10a). No business rule here.
/// </summary>
[Route("api/warehouse/packages")]
[Authorize]
public sealed class PackagesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public PackagesController(IMediator mediator) => _mediator = mediator;

    [HttpPut("{packageId}")]
    [HasPermission(WarehousePermissions.PackagesUpdate)]
    public async Task<IActionResult> Update(string packageId, CancellationToken ct)
    {
        if (!WarehouseSupport.TryParseId(packageId, out var id)) return NotFound();
        var read = await WarehouseRequestReader.ReadAsync(Request.Body, WarehouseRequestReader.PackageMembers,
            b => new { Package = b.Package(), Key = b.String("idempotencyKey") }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return WithETag(await _mediator.Send(new UpdateHandlingUnitCommand(id, body.Package, key, version), ct));
    }

    [HttpPost("{packageId}/close")]
    [HasPermission(WarehousePermissions.PackagesUpdate)]
    public async Task<IActionResult> Close(string packageId, CancellationToken ct)
    {
        if (!WarehouseSupport.TryParseId(packageId, out var id)) return NotFound();
        var read = await WarehouseRequestReader.ReadAsync(Request.Body, WarehouseRequestReader.KeyMembers, b => new { Key = b.String("idempotencyKey") }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return WithETag(await _mediator.Send(new CloseHandlingUnitCommand(id, key, version), ct));
    }

    private IActionResult WithETag(Response<PackageDto> response)
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

    private new IActionResult NotFound() => ContractFailure(StatusCodes.Status404NotFound, WarehouseErrorCodes.PackageNotFound, "Package not found.");
}
