using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting.Ports;
using Diten.InventoryService.Application.Features.Inventory;
using Diten.InventoryService.Application.Features.Inventory.Commands;
using Diten.InventoryService.Application.Features.Inventory.Services;

namespace Diten.InventoryService.Application.Features.CycleCounting.Services;

/// <summary>
/// <see cref="ICycleCountMovementPoster"/> → MOD-0173 <see cref="InternalMovementPostingService"/> (the <c>WarehouseMovementPoster</c> pattern, pack §2.3
/// rule 2): the same parser, idempotency, master-data / lot / recall / FEFO rules and the atomic ledger posting as <c>postMovement</c>. MOD-0181
/// never touches the ledger store itself; the ledger's own type exemptions (recall, FEFO — pack §8.10) apply unchanged.
/// </summary>
public sealed class CycleCountMovementPoster : ICycleCountMovementPoster
{
    private readonly InternalMovementPostingService _posting;

    public CycleCountMovementPoster(InternalMovementPostingService posting) => _posting = posting;

    public Task<Response<MovementResponseDto>> PostAsync(PostMovementCommand request, Guid legalEntityId, Guid actorUserId, CancellationToken cancellationToken)
        => _posting.PostAsync(request, legalEntityId, actorUserId, cancellationToken);
}
