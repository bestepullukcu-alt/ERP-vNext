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
    Expired
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

    /// <summary>CT-D1 (a): the lot's AVAILABLE quantity is not available (SKU position, getAvailability, reservations).</summary>
    public static bool HoldsStock(LotStatus status) => status is LotStatus.Blocked or LotStatus.Expired;

    public static bool IsExpiredOn(Lot lot, DateTimeOffset postingDate)
        => lot.Status == LotStatus.Expired
           || (lot.ExpiryDate is { } expiry && DateOnly.FromDateTime(expiry.UtcDateTime) < DateOnly.FromDateTime(postingDate.UtcDateTime));

    public static LotPostingViolation? Evaluate(Lot lot, Guid skuId, SkuLevel skuLevel, MovementType type, DateTimeOffset postingDate)
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
