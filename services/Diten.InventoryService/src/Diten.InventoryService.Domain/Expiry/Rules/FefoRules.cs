using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Domain.Traceability.Rules;

namespace Diten.InventoryService.Domain.Expiry.Rules;

// MOD-0176 F2 — the pure rules of the three hooks (pack §8.2, §8.3). No I/O: the posting pre-check, the in-transaction re-check and
// (F4) the FEFO order read all call these same functions.

/// <summary>Why a lot with stock is not in the FEFO order (pack §8.2 table; F4 shows the label).</summary>
public enum FefoExclusionReason
{
    Expired,
    RetestDue,
    Blocked,
    Held,
    BelowMinRemaining,
    Quarantined,

    /// <summary>FEFO 1.3.0 (MOD-0177 CT-R12): the lot is under an active recall in the tenant — it cannot leave (KR-C), so it is never first.</summary>
    Recalled
}

/// <summary>One lot of the same company + warehouse + SKU with its AVAILABLE on-hand and its other (held) on-hand.</summary>
/// <summary><paramref name="Recalled"/>: FEFO 1.3.0 (MOD-0177 CT-R12) — the lot is under an active recall (read tenant-wide by the candidate reader).</summary>
public sealed record FefoCandidate(Lot Lot, decimal AvailableOnHand, decimal HeldOnHand, bool Recalled = false);

public sealed record FefoEligibleLot(int Rank, Lot Lot, decimal Available);

public sealed record FefoExcludedLot(Lot Lot, FefoExclusionReason Reason, decimal OnHand);

public sealed record FefoOrder(IReadOnlyList<FefoEligibleLot> Eligible, IReadOnlyList<FefoExcludedLot> Excluded);

/// <summary>
/// Pack §8.2 — the FEFO order of one company + warehouse + SKU. A lot is eligible when it has AVAILABLE on-hand and none of the
/// exclusion reasons applies on <c>asOf</c> (the posting date for a posting, today for a read). Order: expiry ascending, lots
/// without expiry last; ties → manufacturing date (nulls last) → retest date (nulls last) → lot number (ordinal) → lot id. The
/// tie-break only makes the order deterministic; compliance (<see cref="FefoIssueRule"/>) compares expiry dates only.
/// </summary>
public static class FefoOrderRule
{
    public static FefoOrder Order(IEnumerable<FefoCandidate> candidates, DateTimeOffset asOf, int? minRemainingShelfLifeDays)
    {
        var eligible = new List<(Lot Lot, decimal Available)>();
        var excluded = new List<FefoExcludedLot>();
        foreach (var c in candidates)
        {
            if (c.AvailableOnHand <= 0m && c.HeldOnHand <= 0m)
            {
                continue;
            }

            var reason = ExclusionOf(c, asOf, minRemainingShelfLifeDays);
            if (reason is { } r)
            {
                excluded.Add(new FefoExcludedLot(c.Lot, r, c.AvailableOnHand + c.HeldOnHand));
            }
            else
            {
                eligible.Add((c.Lot, c.AvailableOnHand));
            }
        }

        var ordered = eligible
            .OrderBy(e => e.Lot.ExpiryDate is null ? 1 : 0).ThenBy(e => Day(e.Lot.ExpiryDate))
            .ThenBy(e => e.Lot.ManufacturingDate is null ? 1 : 0).ThenBy(e => Day(e.Lot.ManufacturingDate))
            .ThenBy(e => e.Lot.RetestDate is null ? 1 : 0).ThenBy(e => Day(e.Lot.RetestDate))
            .ThenBy(e => e.Lot.LotNumber, StringComparer.Ordinal)
            .ThenBy(e => e.Lot.Id)
            .Select((e, i) => new FefoEligibleLot(i + 1, e.Lot, e.Available))
            .ToList();
        return new FefoOrder(ordered, excluded);
    }

    private static FefoExclusionReason? ExclusionOf(FefoCandidate c, DateTimeOffset asOf, int? minRemaining)
    {
        var lot = c.Lot;
        if (lot.Status == LotStatus.Blocked) return FefoExclusionReason.Blocked;
        if (LotPostingRules.IsExpiredOn(lot, asOf)) return FefoExclusionReason.Expired;
        if (c.Recalled) return FefoExclusionReason.Recalled; // FEFO 1.3.0, CT-R12
        if (LotPostingRules.IsRetestDueOn(lot, asOf)) return FefoExclusionReason.RetestDue;
        if (LotPostingRules.HoldsStock(lot)) return FefoExclusionReason.Held;
        if (MinRemainingShelfLifeRule.Evaluate(lot.ExpiryDate, minRemaining, asOf).Outcome == MinRemainingOutcome.Below)
            return FefoExclusionReason.BelowMinRemaining;
        return c.AvailableOnHand <= 0m ? FefoExclusionReason.Quarantined : null;
    }

    internal static DateOnly Day(DateTimeOffset? value) => value is { } v ? DateOnly.FromDateTime(v.UtcDateTime) : DateOnly.MinValue;
}

/// <summary>The FEFO verdict of one issue: compliant, or which eligible lot should have gone first.</summary>
public sealed record FefoIssueDecision(bool Compliant, Lot? FirstEligible);

/// <summary>
/// Pack §8.3.1 (KF-B) — an issue from <paramref name="issued"/> is COMPLIANT when no OTHER eligible lot of the same company +
/// warehouse + SKU has a strictly earlier expiry date. Equal expiry = compliant (the secondary keys never create a bypass). An
/// issued lot without expiry while an eligible lot with expiry exists is a bypass.
/// </summary>
public static class FefoIssueRule
{
    public static FefoIssueDecision Evaluate(Lot issued, FefoOrder order)
    {
        var others = order.Eligible.Where(e => e.Lot.Id != issued.Id).ToList();
        FefoEligibleLot? first = issued.ExpiryDate is null
            ? others.FirstOrDefault(e => e.Lot.ExpiryDate is not null)
            : others.FirstOrDefault(e => e.Lot.ExpiryDate is not null && FefoOrderRule.Day(e.Lot.ExpiryDate) < FefoOrderRule.Day(issued.ExpiryDate));
        return first is null ? new FefoIssueDecision(true, null) : new FefoIssueDecision(false, first.Lot);
    }
}

public enum MinRemainingOutcome
{
    /// <summary>The lot has no expiry date — the rule does not apply ("SKT takibi yok", ASSUMPTION-0176-06).</summary>
    NotApplicable,

    /// <summary>The SKU states no minimum (null) — the rule is NOT applied (KF-C, "Tanımsız").</summary>
    NotDefined,

    Ok,
    Below
}

public sealed record MinRemainingCheck(MinRemainingOutcome Outcome, int? RemainingDays, int? RequiredDays);

/// <summary>Pack §8.3.2 (KF-C) — remaining shelf life = expiry day − posting day, compared with the SKU's minimum (days).</summary>
public static class MinRemainingShelfLifeRule
{
    public static MinRemainingCheck Evaluate(DateTimeOffset? expiry, int? minRemainingShelfLifeDays, DateTimeOffset postingDate)
    {
        if (expiry is null) return new MinRemainingCheck(MinRemainingOutcome.NotApplicable, null, minRemainingShelfLifeDays);
        if (minRemainingShelfLifeDays is not { } required) return new MinRemainingCheck(MinRemainingOutcome.NotDefined, null, null);
        var remaining = FefoOrderRule.Day(expiry).DayNumber - DateOnly.FromDateTime(postingDate.UtcDateTime).DayNumber;
        return new MinRemainingCheck(remaining < required ? MinRemainingOutcome.Below : MinRemainingOutcome.Ok, remaining, required);
    }
}

public sealed record LotDateSuggestion(DateOnly? Expiry, DateOnly? Retest)
{
    public bool IsEmpty => Expiry is null && Retest is null;
}

/// <summary>
/// Pack §8.3.3 (KF-D) — the suggestion at lot creation: manufacturing date + <c>shelfLifeDays</c> (expiry) / + <c>retestDays</c> (retest),
/// from getSku. No manufacturing date or no day value → no suggestion for that date (and no rule). A date that differs from its
/// suggestion — by day; an empty date while a suggestion exists counts as different (ASSUMPTION-0176-07) — needs an override.
/// </summary>
public static class LotDateSuggestionRule
{
    public static LotDateSuggestion Suggest(DateOnly? manufacturing, int? shelfLifeDays, int? retestDays)
        => manufacturing is not { } m
            ? new LotDateSuggestion(null, null)
            : new LotDateSuggestion(shelfLifeDays is { } s ? m.AddDays(s) : null, retestDays is { } r ? m.AddDays(r) : null);

    public static bool ExpiryDiffers(LotDateSuggestion s, DateOnly? entered) => s.Expiry is { } e && entered != e;

    public static bool RetestDiffers(LotDateSuggestion s, DateOnly? entered) => s.Retest is { } r && entered != r;

    public static bool Differs(LotDateSuggestion s, DateOnly? expiry, DateOnly? retest) => ExpiryDiffers(s, expiry) || RetestDiffers(s, retest);
}

public enum ExpiryHookKind
{
    None,
    Issue,
    Receipt
}

/// <summary>
/// Pack §8.3 + CT-F3 / CT-F5 — which hook a posting is under. Exempt first: REVERSAL, SCRAP, WRITE_OFF (CT-D4), STATUS_TRANSFER,
/// LOCATION_TRANSFER, WAREHOUSE_TRANSFER (CT-F3), COUNT_GAIN, COUNT_LOSS, SUPPLIER_RETURN. Then: an outbound movement from AVAILABLE
/// with a lot is an ISSUE (what is left: GOODS_ISSUE_SALES, GOODS_ISSUE_PROD — whoever posts it, the MVP-6 shipment included); a
/// GOODS_RECEIPT_PO with a lot is a RECEIPT (KF-C, CT-F5). Everything else: no hook.
/// <para>MOD-0175's movements (STATUS_TRANSFER, SCRAP, SUPPLIER_RETURN, LOCATION_TRANSFER) are exempt by TYPE. <c>sourceModule</c> is
/// caller data (INVENTORY <c>MovementRequest.sourceModule</c>) and never exempts — WP-0176-F2-FIX1, CT finding O-F2-2.</para>
/// </summary>
public static class ExpiryHookScope
{
    private static readonly HashSet<MovementType> Exempt =
    [
        MovementType.Reversal, MovementType.Scrap, MovementType.WriteOff, MovementType.StatusTransfer, MovementType.LocationTransfer,
        MovementType.WarehouseTransfer, MovementType.CountGain, MovementType.CountLoss, MovementType.SupplierReturn
    ];

    /// <param name="sourceModule">Kept for the callers' shape; it does not enter the decision (O-F2-2).</param>
    public static ExpiryHookKind For(MovementType type, StockStatus? from, StockStatus? to, string? sourceModule, bool hasRegisteredLot)
    {
        if (!hasRegisteredLot || Exempt.Contains(type)) return ExpiryHookKind.None;
        if (from == StockStatus.Available && to is null) return ExpiryHookKind.Issue;
        return type == MovementType.GoodsReceiptPo && to is not null ? ExpiryHookKind.Receipt : ExpiryHookKind.None;
    }
}

/// <summary>
/// What the posting pre-check decided for the in-transaction re-check (carried on <c>LotPostingContext.Expiry</c>): the hook, the
/// company policy read at pre-check, the caller's bypass reason (INVENTORY 1.3.0 <c>fefoBypassReason</c>) and the SKU's minimum
/// remaining shelf life (getSku; null = not stated). Null on the context = no hook (exempt, no registered lot, REVERSAL).
/// </summary>
public sealed record ExpiryPostingInput(
    ExpiryHookKind Kind,
    Entities.ExpiryPolicy Policy,
    string? BypassReason,
    int? MinRemainingShelfLifeDays);
