using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.ValueObjects;

namespace Diten.InventoryService.Domain.Rules;

/// <summary>
/// Derives the balance legs of a transaction: a source leg (−BaseQuantity) when it has a from-side, a target leg
/// (+BaseQuantity) when it has a to-side. Decreases come first so a transfer is refused before anything moves.
/// The snapshot leg (the row returned as <c>balanceSnapshot</c>) is the target when there is one, else the source.
/// </summary>
public static class LedgerLegs
{
    public static IReadOnlyList<LedgerLeg> For(InventoryTransaction transaction)
    {
        var legs = new List<LedgerLeg>(2);

        if (transaction.FromLocationId is not null)
        {
            legs.Add(new LedgerLeg(
                new InventoryDimension(
                    transaction.ItemId,
                    transaction.SkuId,
                    transaction.SkuLevel,
                    transaction.FromWarehouseId,
                    transaction.FromLocationId,
                    transaction.LotId,
                    transaction.FromStockStatus ?? throw new InvalidOperationException("Source leg without a stock status.")),
                -transaction.BaseQuantity,
                transaction.BaseUomId));
        }

        if (transaction.ToLocationId is not null)
        {
            legs.Add(new LedgerLeg(
                new InventoryDimension(
                    transaction.ItemId,
                    transaction.SkuId,
                    transaction.SkuLevel,
                    transaction.ToWarehouseId,
                    transaction.ToLocationId,
                    transaction.LotId,
                    transaction.ToStockStatus ?? throw new InvalidOperationException("Target leg without a stock status.")),
                transaction.BaseQuantity,
                transaction.BaseUomId));
        }

        if (legs.Count == 0)
        {
            throw new InvalidOperationException("A transaction must move stock on at least one side.");
        }

        return legs;
    }

    public static LedgerLeg SnapshotLeg(IReadOnlyList<LedgerLeg> legs)
        => legs.LastOrDefault(l => !l.IsDecrease) ?? legs[0];
}
