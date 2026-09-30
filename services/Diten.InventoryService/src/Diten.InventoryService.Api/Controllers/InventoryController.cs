using Diten.InventoryService.Api.Contract;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Inventory.Queries;
using Diten.InventoryService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.InventoryService.Api.Controllers;

/// <summary>
/// INVENTORY-BUNDLE v1 — Slice S1 operations only (pack §1): <c>postMovement</c>, <c>getAvailability</c>,
/// <c>getBalance</c>, <c>getTransactions</c>. Reservations are S2 and not routed here. There is deliberately no
/// PUT / PATCH / DELETE: stock changes only through POST /movements, and a transaction is never edited (AC17).
/// No business rule lives here — every action is read → send → render.
/// </summary>
[Route("api/inventory")]
[Authorize]
public sealed class InventoryController : CustomBaseController
{
    private readonly IMediator _mediator;

    public InventoryController(IMediator mediator) => _mediator = mediator;

    [HttpPost("movements")]
    [HasPermission(InventoryPermissions.PostMovements)]
    public async Task<IActionResult> PostMovement(CancellationToken cancellationToken)
    {
        var (command, error) = await MovementRequestReader.ReadAsync(
            Request.Body,
            Request.Headers[InventoryContract.IdempotencyKeyHeader].ToString(),
            cancellationToken);
        if (command is null)
        {
            return ContractFailure(StatusCodes.Status400BadRequest, InventoryErrorCodes.InvalidRequest, error!);
        }

        return CreateActionResultInstance(await _mediator.Send(command, cancellationToken));
    }

    [HttpGet("availability")]
    [HasPermission(InventoryPermissions.Read)]
    public async Task<IActionResult> GetAvailability(
        [FromQuery] string? itemId,
        [FromQuery] string? skuId,
        [FromQuery] string? skuLevel,
        [FromQuery] string? warehouseId,
        [FromQuery] string? locationId,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetAvailabilityQuery(itemId, skuId, skuLevel, warehouseId, locationId), cancellationToken));

    [HttpGet("balance")]
    [HasPermission(InventoryPermissions.Read)]
    public async Task<IActionResult> GetBalance(
        [FromQuery] string? itemId,
        [FromQuery] string? skuId,
        [FromQuery] string? skuLevel,
        [FromQuery] string? warehouseId,
        [FromQuery] string? locationId,
        [FromQuery] string? lotId,
        [FromQuery] string? stockStatus,
        [FromQuery] string? page,
        [FromQuery] string? pageSize,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetBalanceQuery(itemId, skuId, skuLevel, warehouseId, locationId, lotId, stockStatus, page, pageSize),
            cancellationToken));

    [HttpGet("transactions")]
    [HasPermission(InventoryPermissions.Read)]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] string? itemId,
        [FromQuery] string? lotId,
        [FromQuery] string? locationId,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? page,
        [FromQuery] string? pageSize,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetTransactionsQuery(itemId, lotId, locationId, from, to, page, pageSize), cancellationToken));
}
