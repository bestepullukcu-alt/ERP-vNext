using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Application.Features.Inventory.Ports;

/// <summary>
/// MOD-0173-FU03 §8.5 (DL-3, Q-01 = A) — the reserved quantity per lot of one company + warehouse + SKU: the remaining quantity of the
/// ACTIVE, not yet due lot-scoped reservations (<c>ReservationActiveFilter</c>), keyed by <c>Lot.Id</c>; a lot nobody reserved is not in the
/// dictionary (= 0). Warehouse reservations without a lot are not counted per lot. The FEFO hook subtracts it from a lot's AVAILABLE on-hand,
/// so stock reserved for another outbound is not in the FEFO order. No session in the signature (Application has no MongoDB): the
/// implementation reads in the posting transaction while one is bound to the request, outside any transaction otherwise.
/// </summary>
public interface ILotReservationReader
{
    Task<IReadOnlyDictionary<Guid, decimal>> ReservedByLotAsync(
        Guid itemId, Guid skuId, SkuLevel skuLevel, string warehouseId, CancellationToken cancellationToken = default);
}
