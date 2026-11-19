using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory;
using Diten.InventoryService.Application.Features.Inventory.Commands;
using Diten.InventoryService.Application.Features.Inventory.Services;
using Diten.InventoryService.Application.Features.Warehouse.Ports;

namespace Diten.InventoryService.Application.Features.Warehouse.Services;

/// <summary>
/// <see cref="IWarehouseMovementPoster"/> → MOD-0173 <see cref="InternalMovementPostingService"/> (the <c>QuarantineMovementPoster</c> pattern,
/// pack §2): the same parser, idempotency, master-data / lot / recall / FEFO / retest rules and the atomic ledger posting as
/// <c>postMovement</c>. MOD-0178 never touches the ledger store itself.
/// </summary>
public sealed class WarehouseMovementPoster : IWarehouseMovementPoster
{
    private readonly InternalMovementPostingService _posting;

    public WarehouseMovementPoster(InternalMovementPostingService posting) => _posting = posting;

    public Task<Response<MovementResponseDto>> PostAsync(PostMovementCommand request, Guid legalEntityId, Guid actorUserId, CancellationToken cancellationToken)
        => _posting.PostAsync(request, legalEntityId, actorUserId, cancellationToken);
}
