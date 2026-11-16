using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Inventory.Ports;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Application.Features.Traceability.Ports;
using Diten.InventoryService.Application.Features.Traceability.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Traceability.Rules;

namespace Diten.InventoryService.Persistence.Repositories;

/// <summary>
/// <see cref="IReservationLotRule"/> on the in-process lot master (<see cref="ILotMasterClient"/>, read outside any transaction — no
/// <c>StockTouchSeq</c> write) and the recall status reader. The rule a reservation must pass is the MOD-0174 rule of a sales issue out of
/// AVAILABLE (<see cref="LotPostingRules.Evaluate(Domain.Traceability.Entities.Lot, Guid, SkuLevel, MovementType, DateTimeOffset, StockStatus?, StockStatus?)"/>)
/// with the same codes and details as the ledger's refusal; a lot whose status changes later keeps its reservation (FU03 §8.2).
/// </summary>
public sealed class ReservationLotRule : IReservationLotRule
{
    private readonly ILotMasterClient _lots;
    private readonly IRecallLotStatusReader _recall;

    public ReservationLotRule(ILotMasterClient lots, IRecallLotStatusReader recall)
    {
        _lots = lots;
        _recall = recall;
    }

    public async Task<ReservationLotCheck> CheckAsync(string lotId, Guid skuId, SkuLevel skuLevel, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var lot = LotPostingRules.IsRegisteredLotForm(lotId, out _) ? await _lots.ResolveLotAsync(lotId, cancellationToken) : null;
        if (lot is null)
        {
            return new ReservationLotCheck(null, InventoryErrorCodes.UnknownLot, "lotId does not name a registered lot of this tenant.",
                new Dictionary<string, object?> { ["lotId"] = lotId });
        }

        if (LotPostingRules.Evaluate(lot, skuId, skuLevel, MovementType.GoodsIssueSales, now, StockStatus.Available, null) is { } violation)
        {
            var (code, message, details) = violation == LotPostingViolation.RetestDue ? InventoryErrorCodes.RetestDue(lot) : LotPostingErrors.For(violation, lot);
            return new ReservationLotCheck(null, code, message, details);
        }

        if ((await _recall.UnderRecallAsync([lotId], cancellationToken)).Contains(lotId))
        {
            return new ReservationLotCheck(null, InventoryErrorCodes.LotUnderRecall,
                $"The lot {lot.LotNumber} is under recall: its stock cannot be reserved.",
                new Dictionary<string, object?> { ["lotId"] = lot.Id.ToString("D"), ["lotNumber"] = lot.LotNumber });
        }

        return new ReservationLotCheck(lot.Id);
    }
}
