using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.StockBehaviour;

/// <summary>
/// WP-MDM-ITEM-MASTER (MOD-0290-FU04) S3b — the stock team's R-04 condition: an Active (IdentityApproved) product SKU
/// never carries an unset lot / serial flag at its own level. Checked when the identity is SENT for approval; the flags
/// cannot be emptied afterwards (a stock edit never clears a field, and the record is fenced while the request is open),
/// so the decision is not re-checked. Serial is decided at LSKU / Finished Good level — a GSKU has none.
/// <list type="bullet">
/// <item>GSKU: <c>lotControlled</c>.</item>
/// <item>LSKU: <c>serialControlled</c> and its GSKU's <c>lotControlled</c> (an LSKU's lot is its GSKU's, O-3).</item>
/// <item>Finished Good: <c>lotControlled</c> and <c>serialControlled</c>.</item>
/// </list>
/// An older approved record without a flag is not touched: the read contract answers it <c>stockable: false</c> (the
/// completeness rules in <c>StockBehaviourRules</c>, read by <c>ProductMasterContract</c>) until the flag is filled.
/// </summary>
public static class StockFlagGate
{
    public const string Required = "STOCK_FLAGS_REQUIRED_FOR_APPROVAL";

    public const string LotControlled = "lotControlled";
    public const string SerialControlled = "serialControlled";
    public const string GskuLotControlled = "gsku.lotControlled";

    public static IReadOnlyList<string> MissingForGsku(Gsku gsku) =>
        gsku.LotControlled is null ? [LotControlled] : [];

    public static IReadOnlyList<string> MissingForLsku(Lsku lsku, Gsku gsku)
    {
        var missing = new List<string>();
        if (lsku.SerialControlled is null) missing.Add(SerialControlled);
        if (gsku.LotControlled is null) missing.Add(GskuLotControlled);
        return missing;
    }

    // No Finished Good identity submit exists yet (measured, S3b); the rule is ready for it.
    public static IReadOnlyList<string> MissingForFinishedGood(FinishedGood finishedGood)
    {
        var missing = new List<string>();
        if (finishedGood.LotControlled is null) missing.Add(LotControlled);
        if (finishedGood.SerialControlled is null) missing.Add(SerialControlled);
        return missing;
    }

    /// <summary>The refusal: the code first, then the names of the unset flags (409 — the record is not ready yet).</summary>
    public static IReadOnlyList<string> Refusal(IReadOnlyList<string> missing) => [Required, .. missing];
}
