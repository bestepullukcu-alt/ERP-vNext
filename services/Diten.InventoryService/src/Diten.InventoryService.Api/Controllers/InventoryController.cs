using Diten.InventoryService.Api.Contract;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Inventory.Commands;
using Diten.InventoryService.Application.Features.Inventory.Queries;
using Diten.InventoryService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.InventoryService.Api.Controllers;

/// <summary>
/// INVENTORY-BUNDLE v1 — Slice S1 + S2 operations (pack §1): <c>postMovement</c>, <c>createReservation</c>,
/// <c>releaseReservation</c>, <c>getAvailability</c>,
/// <c>getBalance</c>, <c>getTransactions</c> — the six operations of the frozen contract, plus <c>getReservation</c> (1.6.0, MOD-0173-FU03). There is deliberately no
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
    [HasPermission(InventoryPermissions.CreateMovements)]
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

    [HttpPost("reservations")]
    [HasPermission(InventoryPermissions.ManageReservations)]
    public async Task<IActionResult> CreateReservation(CancellationToken cancellationToken)
    {
        var (command, error) = await ReservationRequestReader.ReadAsync(
            Request.Body,
            Request.Headers[InventoryContract.IdempotencyKeyHeader].ToString(),
            cancellationToken);
        if (command is null)
        {
            return ContractFailure(StatusCodes.Status400BadRequest, InventoryErrorCodes.InvalidRequest, error!);
        }

        return CreateActionResultInstance(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>INVENTORY 1.6.0 <c>getReservation</c> (MOD-0173-FU03 §8.8): the reservation with its current <c>ETag: W/"n"</c>.</summary>
    [HttpGet("reservations/{id}")]
    [HasPermission(InventoryPermissions.Read)]
    public async Task<IActionResult> GetReservation(string id, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetReservationByIdQuery(id), cancellationToken);
        if (response.IsSuccessful && response.Data is { } view)
        {
            Response.Headers.ETag = view.Etag;
        }

        return CreateActionResultInstance(response);
    }

    [HttpPost("reservations/{id}/release")]
    [HasPermission(InventoryPermissions.ManageReservations)]
    public async Task<IActionResult> ReleaseReservation(string id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ReleaseReservationCommand(id, Request.Headers[InventoryContract.IfMatchHeader].ToString()), cancellationToken));

    [HttpGet("availability")]
    [HasPermission(InventoryPermissions.Read)]
    public async Task<IActionResult> GetAvailability(
        [FromQuery] string? itemId,
        [FromQuery] string? skuId,
        [FromQuery] string? skuLevel,
        [FromQuery] string? warehouseId,
        [FromQuery] string? locationId,
        [FromQuery] string? lotId,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetAvailabilityQuery(itemId, skuId, skuLevel, warehouseId, locationId, lotId), cancellationToken));

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
