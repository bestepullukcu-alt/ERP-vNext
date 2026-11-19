namespace Diten.InventoryService.Domain.Repositories;

public enum ReservationCreateOutcome
{
    /// <summary>Committed: reservation, position, audit and outbox.</summary>
    Created,

    /// <summary><c>AvailableOnHand − Reserved</c> of one scope level (MOD-0173-FU03: LOT, WAREHOUSE or SKU) did not cover the quantity; nothing written.</summary>
    InsufficientAvailable,

    /// <summary>A concurrent request committed the same idempotency key first.</summary>
    IdempotencyKeyTaken
}
