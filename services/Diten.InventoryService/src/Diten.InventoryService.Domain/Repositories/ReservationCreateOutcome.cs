namespace Diten.InventoryService.Domain.Repositories;

public enum ReservationCreateOutcome
{
    /// <summary>Committed: reservation, position, audit and outbox.</summary>
    Created,

    /// <summary>SKU <c>AvailableOnHand − Reserved</c> did not cover the quantity; nothing written.</summary>
    InsufficientAvailable,

    /// <summary>A concurrent request committed the same idempotency key first.</summary>
    IdempotencyKeyTaken
}
