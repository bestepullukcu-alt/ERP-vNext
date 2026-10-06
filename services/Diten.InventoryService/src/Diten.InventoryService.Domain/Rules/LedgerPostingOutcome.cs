namespace Diten.InventoryService.Domain.Rules;

public enum LedgerPostingOutcome
{
    /// <summary>Committed: transaction, balances, audit and outbox are all written.</summary>
    Posted,

    /// <summary>A decrease leg was not covered; nothing was written.</summary>
    InsufficientStock,

    /// <summary>Another request committed the same idempotency key first; nothing was written by this one.</summary>
    IdempotencyKeyTaken,

    /// <summary>Another REVERSAL of the same original committed first; nothing was written by this one.</summary>
    AlreadyReversed,

    /// <summary>
    /// K-13 (MOD-0173-FU02): a leg location is not an Active stockable location of this company under the named
    /// warehouse at commit time (deactivated, archived, made non-stockable or moved meanwhile); nothing was written.
    /// </summary>
    LocationNotPostable,

    /// <summary>
    /// WP-0174-P2: re-checked inside the posting transaction, a registered lot or a serial forbids the posting
    /// (<c>LOT_BLOCKED</c> / <c>LOT_EXPIRED</c> after a concurrent status change, a serial taken meanwhile …); the code is
    /// in <see cref="LedgerPostingResult.RefusalCode"/>. Nothing was written.
    /// </summary>
    LotOrSerialRefused
}
