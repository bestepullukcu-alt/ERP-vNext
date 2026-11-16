using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory;
using Diten.InventoryService.Application.Features.Inventory.Commands;

namespace Diten.InventoryService.Application.Features.Warehouse.Ports;

/// <summary>
/// The ONE stock door of MOD-0178 (pack §2 boundary rule 2, AC-6): a <c>LOCATION_TRANSFER</c> goes through the ledger's in-process posting
/// path with the confirmation's stored request. The production implementation is <c>Services.WarehouseMovementPoster</c>; tests wrap it to
/// stage a lost answer or an outage of the ledger (AC-5) — they never post around it.
/// </summary>
public interface IWarehouseMovementPoster
{
    Task<Response<MovementResponseDto>> PostAsync(PostMovementCommand request, Guid legalEntityId, Guid actorUserId, CancellationToken cancellationToken);
}
