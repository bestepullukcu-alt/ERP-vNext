using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.ValueObjects;

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

    /// <summary>
    /// S3b FIX1 (R2): the action a draft's read carries INSTEAD of SUBMIT when the gate would refuse it — the screen says
    /// why (enter the stock flags first). The same Missing… functions decide both: no copy of the rule.
    /// </summary>
    public const string SubmitNeedsStockFlagsAction = "STOCK_FLAGS_REQUIRED";

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

    /// <summary>
    /// S5 (D-14, CT decision) — the GSKU submit's whole refusal, or none. The lot flag first (R-04); then the days: a draft
    /// may hold a retest interval / minimum remaining shelf life without its shelf life (a draft is free), but it is not
    /// SENT that way — approved, those days are locked, and a shelf life entered later below them would be refused
    /// (R-3's trap). So: days set → the shelf life too (<see cref="StockBehaviourRules.DaysRequireShelfLife"/> + their
    /// names), and not below them (<see cref="StockBehaviourRules.DaysInvalid"/> + <see cref="DaysDetail"/>). The action
    /// list asks this same function.
    /// </summary>
    public static IReadOnlyList<string>? SubmitRefusalForGsku(Gsku gsku)
    {
        if (MissingForGsku(gsku) is { Count: > 0 } missing) return Refusal(missing);
        var days = DaysSet(gsku.RetestDays, gsku.MinRemainingShelfLifeDays);
        if (days.Count == 0) return null;
        if (gsku.ShelfLifeDays is null) return [StockBehaviourRules.DaysRequireShelfLife, .. days.Select(x => x.Field)];
        return DaysDetail(gsku.ShelfLifeDays, gsku.RetestDays, gsku.MinRemainingShelfLifeDays) is { } detail
            ? [StockBehaviourRules.DaysInvalid, .. detail]
            : null;
    }

    /// <summary>
    /// S5 (D-15) — what <see cref="StockBehaviourRules.DaysInvalid"/> names when the days exceed a known shelf life: each
    /// exceeding field, then the bound the shelf life must reach (<c>shelfLifeDaysAtLeast=N</c>). Null when within.
    /// </summary>
    public static IReadOnlyList<string>? DaysDetail(int? shelfLifeDays, int? retestDays, int? minRemainingShelfLifeDays)
    {
        if (shelfLifeDays is not { } shelf) return null;
        var over = DaysSet(retestDays, minRemainingShelfLifeDays).Where(x => x.Days > shelf).ToList();
        return over.Count == 0 ? null : [.. over.Select(x => x.Field), $"{ShelfLifeAtLeast}={over.Max(x => x.Days)}"];
    }

    public const string ShelfLifeAtLeast = "shelfLifeDaysAtLeast";
    public const string RetestDays = "retestDays";
    public const string MinRemainingShelfLifeDays = "minRemainingShelfLifeDays";

    private static List<(string Field, int Days)> DaysSet(int? retestDays, int? minRemainingShelfLifeDays)
    {
        var days = new List<(string, int)>();
        if (retestDays is { } retest) days.Add((RetestDays, retest));
        if (minRemainingShelfLifeDays is { } minimum) days.Add((MinRemainingShelfLifeDays, minimum));
        return days;
    }
}
