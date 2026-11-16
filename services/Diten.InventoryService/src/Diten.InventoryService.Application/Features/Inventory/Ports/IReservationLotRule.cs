using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Application.Features.Inventory.Ports;

/// <summary>
/// MOD-0173-FU03 §8.2 "Lot kuralı (yaratmada)" — may this lot be reserved for this SKU now? The lot is registered (422 UNKNOWN_LOT), of
/// this SKU (LOT_SKU_MISMATCH), passes the MOD-0174 lot rule of a sales issue out of AVAILABLE (LOT_BLOCKED / LOT_EXPIRED /
/// LOT_RETEST_DUE; a Draft lot is accepted — ASSUMPTION-0174-07) and is not under recall (LOT_UNDER_RECALL, tenant-wide — CT-R7).
/// Implemented next to the ledger in Persistence, where the lot master is read in process: the ledger feature itself keeps its MOD-0174
/// links to the named hook points (MOD-0174 WP-0174-P2).
/// </summary>
public interface IReservationLotRule
{
    Task<ReservationLotCheck> CheckAsync(string lotId, Guid skuId, SkuLevel skuLevel, DateTimeOffset now, CancellationToken cancellationToken = default);
}

/// <summary>Accepted → <see cref="LotId"/> is the registered lot; refused → the 422 code, message and details of the lot rule.</summary>
public sealed record ReservationLotCheck(Guid? LotId, string? Code = null, string? Message = null, IReadOnlyDictionary<string, object?>? Details = null)
{
    public bool Accepted => Code is null;
}
