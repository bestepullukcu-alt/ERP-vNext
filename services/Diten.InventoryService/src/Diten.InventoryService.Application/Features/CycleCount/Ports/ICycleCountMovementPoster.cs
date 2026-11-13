using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory;
using Diten.InventoryService.Application.Features.Inventory.Commands;

namespace Diten.InventoryService.Application.Features.CycleCounting.Ports;

/// <summary>
/// MOD-0181's ONE stock door (pack §2.3 rule 2, §8.6; AC-29): a <c>COUNT_GAIN</c> / <c>COUNT_LOSS</c> request goes through MOD-0173's internal posting
/// path with the same parser, idempotency and validators as <c>postMovement</c>. Its only caller is <c>CycleCountPostingEngine</c>'s
/// <c>_poster</c> field (the architecture tests measure both).
/// </summary>
public interface ICycleCountMovementPoster
{
    Task<Response<MovementResponseDto>> PostAsync(PostMovementCommand request, Guid legalEntityId, Guid actorUserId, CancellationToken cancellationToken);
}
