using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.Rules;

/// <summary>How a published lifecycle status lets stock move (WP-0173-PM1).</summary>
public enum StockLifecycle
{
    /// <summary><c>Active</c>: every movement.</summary>
    Active = 0,

    /// <summary><c>Deactivated</c> (material items, reversible): no new stock; existing stock may leave, move, be counted or scrapped (OD-3).</summary>
    Deactivated = 1,

    /// <summary><c>Retired</c> — and the v1.0 <c>Archived</c>, which the contract says to read as Retired: only SCRAP / WRITE_OFF (OD-4).</summary>
    Retired = 2,

    /// <summary><c>Draft</c>, a value outside the published list, or no value at all: nothing moves (CT-PM1-2, CT-PM1-3).</summary>
    NotUsable = 3
}

/// <summary>Why the stock behaviour of a SKU could not be decided (each maps to one INVENTORY 1.2.0 error code).</summary>
public enum StockBehaviourRefusal
{
    /// <summary>A flag the level needs is unknown (null / missing / not a boolean) — <c>PRODUCT_MASTER_DATA_INCOMPLETE</c> (OD-5).</summary>
    DataIncomplete,

    /// <summary>A GSKU movement of a product that is not explicitly serial-free — <c>SKU_LEVEL_NOT_STOCKABLE</c> (OD-10).</summary>
    LevelNotStockable
}

/// <summary>The lot / serial behaviour a movement must follow, or why it cannot be decided.</summary>
public readonly record struct StockBehaviour(bool LotControlled, bool SerialControlled, StockBehaviourRefusal? Refusal)
{
    public bool IsDecided => Refusal is null;

    public static StockBehaviour Refused(StockBehaviourRefusal refusal) => new(false, false, refusal);
}

/// <summary>
/// WP-0173-PM1 — the PRODUCT-MASTER v1.1 rules the ledger applies to every movement (pure, no I/O). Sources: MOD-0290 requirements
/// §8.1 OD-1 / OD-3 / OD-4 / OD-5, §8.5 OD-10 + Q-1, CT-PM1-2 / CT-PM1-3; `product-master-bundle.openapi.yaml` 1.1.0-draft.8
/// (<c>LifecycleStatus</c>, <c>SkuRef</c>). An unknown flag is NEVER read as false anywhere here.
/// </summary>
public static class ProductMasterStockRules
{
    /// <summary>The published status → how stock may move. Ordinal, case-sensitive (the contract enumerates exact values).</summary>
    public static StockLifecycle Lifecycle(string? published) => published switch
    {
        "Active" => StockLifecycle.Active,
        "Deactivated" => StockLifecycle.Deactivated,
        "Retired" or "Archived" => StockLifecycle.Retired,
        _ => StockLifecycle.NotUsable
    };

    /// <summary>The most restrictive of the statuses read for one movement (item row, SKU row, SKU) — fail-closed.</summary>
    public static StockLifecycle MostRestrictive(IEnumerable<string?> published)
        => published.Select(Lifecycle).DefaultIfEmpty(StockLifecycle.NotUsable).Max();

    /// <summary>
    /// DEC-INV-16 movement types × lifecycle. REVERSAL never reaches this rule: a reversal copies its original and is not
    /// validated against PRODUCT-MASTER (the contract: "Her durumda ters kayıt kabul edilir").
    /// </summary>
    public static bool Allows(StockLifecycle lifecycle, MovementType type) => lifecycle switch
    {
        StockLifecycle.Active => true,
        StockLifecycle.Deactivated => type is not (MovementType.GoodsReceiptPo or MovementType.GoodsReceiptProd or MovementType.OpeningBalance),
        StockLifecycle.Retired => type is MovementType.Scrap or MovementType.WriteOff or MovementType.Reversal,
        _ => false
    };

    /// <summary>
    /// The lot / serial flags of the movement's SKU (OD-1: from <c>getSku</c>; the deprecated product flags are not used — except
    /// the product's serial flag at GSKU, where the SKU has none, OD-10):
    /// <list type="bullet">
    /// <item>lot flag null → refused (OD-5), at every level;</item>
    /// <item>Item: the SKU's own serial flag; <c>false</c> by contract, <c>true</c> honoured, null refused;</item>
    /// <item>Gsku: serial "not applicable" at this level — the movement only when the PRODUCT's flag is explicitly false;
    /// true or null → <see cref="StockBehaviourRefusal.LevelNotStockable"/>;</item>
    /// <item>Lsku / FinishedGood: the SKU's own serial flag; null refused.</item>
    /// </list>
    /// </summary>
    public static StockBehaviour Behaviour(SkuLevel level, bool? skuLot, bool? skuSerial, bool? productSerial)
    {
        if (skuLot is not { } lot)
        {
            return StockBehaviour.Refused(StockBehaviourRefusal.DataIncomplete);
        }

        if (level == SkuLevel.Gsku)
        {
            return productSerial == false
                ? new StockBehaviour(lot, false, null)
                : StockBehaviour.Refused(StockBehaviourRefusal.LevelNotStockable);
        }

        return skuSerial is { } serial
            ? new StockBehaviour(lot, serial, null)
            : StockBehaviour.Refused(StockBehaviourRefusal.DataIncomplete);
    }

    /// <summary>
    /// The tracking mode of a NEW lot (createLot, the typed-lot migration): over-requires under doubt — an unknown serial flag
    /// makes the lot Lot + Serial, never Lot only (OD-9's "round up" applied on our side).
    /// </summary>
    public static bool LotNeedsSerials(SkuLevel level, bool? skuSerial, bool? productSerial)
        => level == SkuLevel.Gsku ? productSerial != false : skuSerial != false;
}
