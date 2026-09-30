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
    AlreadyReversed
}
