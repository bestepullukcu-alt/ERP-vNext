using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;

namespace Diten.InventoryService.Domain.Traceability.Rules;

/// <summary>What a registered lot forbids for one ledger posting (pack §12 "Ledger posting rules").</summary>
public enum LotPostingViolation
{
    /// <summary><c>LOT_SKU_MISMATCH</c> — the lot belongs to another SKU than the movement.</summary>
    SkuMismatch,

    /// <summary><c>LOT_BLOCKED</c> — the lot is Blocked (CT-D4 exceptions apart).</summary>
    Blocked,

    /// <summary><c>LOT_EXPIRED</c> — the lot is Expired or its expiry date is before the posting date (CT-D4 exceptions apart).</summary>
    Expired,

    /// <summary>
    /// MOD-0175 Q4 (KQ-H, CT-Q3): <c>LOT_RETEST_DUE</c> — the lot's retest date is before the posting date (UTC days). Exceptions:
    /// CT-D4 (SCRAP, WRITE_OFF, REVERSAL), STATUS_TRANSFER AVAILABLE → QUARANTINE / QUALITY_INSPECTION / BLOCKED (taking the stock
    /// out of use), and MOD-0175 decision movements (QA-approved; a retest release sets the new date first).
    /// </summary>
    RetestDue
}

/// <summary>
/// WP-0174-P2 — the lot side of a ledger posting, pure (no I/O), used by the pre-check AND re-evaluated inside the posting
/// transaction on the lot read there:
/// <list type="bullet">
/// <item>pack §12 order: <c>LOT_SKU_MISMATCH</c> → <c>LOT_BLOCKED</c> → <c>LOT_EXPIRED</c>;</item>
/// <item>CT-D4: out of / into a Blocked or Expired lot only SCRAP, WRITE_OFF and REVERSAL are accepted;</item>
/// <item>expiry is checked BY DATE too (<c>expiryDate &lt; postingDate</c>, UTC days), so a lagging sweeper never lets an
/// expired lot through (pack §8 B-3);</item>
/// <item>CT-D1 (a): a Blocked / Expired lot holds its AVAILABLE stock out of availability — by its STORED status, the same
/// status the SKU position was adjusted for when it changed.</item>
/// </list>
/// Draft lots receive and issue stock (ASSUMPTION-0174-07).
/// </summary>
public static class LotPostingRules
{
    public static bool IsCtD4Exception(MovementType type)
        => type is MovementType.Scrap or MovementType.WriteOff or MovementType.Reversal;

    /// <summary>
    /// CT-D1 (a) + MOD-0175 CT-Q16 — THE answer to "is this lot's AVAILABLE quantity out of availability?" (SKU position, ledger
    /// posting, rebuild, re-key, migration, getAvailability, reservations): Blocked, Expired, or a STORED retest hold. Never a
    /// date comparison: the position was adjusted when the stored state changed, so every reader must read the stored state.
    /// </summary>
    public static bool HoldsStock(Lot lot) => HoldsStock(lot.Status, lot.RetestHeldAtUtc);

    /// <summary>The same answer for a status the lot is moving from / to, with its stored retest hold.</summary>
    public static bool HoldsStock(LotStatus status, DateTimeOffset? retestHeldAtUtc)
        => status is LotStatus.Blocked or LotStatus.Expired || retestHeldAtUtc is not null;

    /// <summary>KQ-H: the retest date is before the posting date (UTC days).</summary>
    public static bool IsRetestDueOn(Lot lot, DateTimeOffset postingDate)
        => lot.RetestDate is { } retest && DateOnly.FromDateTime(retest.UtcDateTime) < DateOnly.FromDateTime(postingDate.UtcDateTime);

    public static bool IsExpiredOn(Lot lot, DateTimeOffset postingDate)
        => lot.Status == LotStatus.Expired
           || (lot.ExpiryDate is { } expiry && DateOnly.FromDateTime(expiry.UtcDateTime) < DateOnly.FromDateTime(postingDate.UtcDateTime));

    public static LotPostingViolation? Evaluate(Lot lot, Guid skuId, SkuLevel skuLevel, MovementType type, DateTimeOffset postingDate)
        => Evaluate(lot, skuId, skuLevel, type, postingDate, null, null);

    /// <summary>
    /// The full rule (pre-check and in-transaction re-check pass the movement's resolved statuses): pack §12 order, then MOD-0175 KQ-H
    /// <see cref="LotPostingViolation.RetestDue"/> — a retest-due lot's stock may not be used or come into use: a movement out of or
    /// into AVAILABLE is refused (sales / production issue, transfer, supplier return, a receipt or release into AVAILABLE), except
    /// STATUS_TRANSFER AVAILABLE → QUARANTINE / QUALITY_INSPECTION / BLOCKED and CT-D4's SCRAP / WRITE_OFF / REVERSAL. Movements that keep
    /// the stock out of use (QUARANTINE / QUALITY_INSPECTION → BLOCKED, BLOCKED → QUARANTINE, a disposal of BLOCKED stock — every MOD-0175
    /// decision movement except a release) pass: ASSUMPTION-0175-Q4-01b — the pack's "0175 apply movements" exception is read by the
    /// movement's direction, never by its claimed <c>sourceModule</c> (any caller of postMovement may write any source module). A release
    /// into AVAILABLE passes only once the approved new retest date is on the lot (KQ-H retest release). No status known (the short form)
    /// counts as AVAILABLE.
    /// </summary>
    public static LotPostingViolation? Evaluate(
        Lot lot, Guid skuId, SkuLevel skuLevel, MovementType type, DateTimeOffset postingDate,
        StockStatus? fromStatus, StockStatus? toStatus)
    {
        var classic = EvaluateClassic(lot, skuId, skuLevel, type, postingDate);
        if (classic is not null || IsCtD4Exception(type) || !IsRetestDueOn(lot, postingDate))
        {
            return classic;
        }

        var touchesUsable = fromStatus == StockStatus.Available || toStatus == StockStatus.Available || (fromStatus is null && toStatus is null);
        var outOfUse = type == MovementType.StatusTransfer && fromStatus == StockStatus.Available
                       && toStatus is StockStatus.Quarantine or StockStatus.QualityInspection or StockStatus.Blocked;
        return touchesUsable && !outOfUse ? LotPostingViolation.RetestDue : null;
    }

    private static LotPostingViolation? EvaluateClassic(Lot lot, Guid skuId, SkuLevel skuLevel, MovementType type, DateTimeOffset postingDate)
    {
        if (lot.SkuId != skuId || lot.SkuLevel != skuLevel)
        {
            return LotPostingViolation.SkuMismatch;
        }

        if (IsCtD4Exception(type))
        {
            return null;
        }

        if (lot.Status == LotStatus.Blocked)
        {
            return LotPostingViolation.Blocked;
        }

        return IsExpiredOn(lot, postingDate) ? LotPostingViolation.Expired : null;
    }

    /// <summary>
    /// A ledger <c>lotId</c> names a registered lot only in the canonical lower-case UUID form the TRACE API returns. The
    /// typed lot of W-UI-02 is upper-case <c>[A-Z0-9./-]</c> text (MVC rule), so the two forms never overlap in practice.
    /// </summary>
    public static bool IsRegisteredLotForm(string? lotId, out Guid id)
        => Guid.TryParseExact(lotId, "D", out id) && string.Equals(lotId, id.ToString("D"), StringComparison.Ordinal);
}
