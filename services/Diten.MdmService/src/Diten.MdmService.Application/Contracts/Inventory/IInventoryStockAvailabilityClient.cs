namespace Diten.MdmService.Application.Contracts.Inventory;

// MOD-0290-FU04 §8.3: "is there non-zero stock for this SKU anywhere in the tenant" — every legal entity, every stock
// status (a negative balance is not zero), called with MDM's service identity. The inventory team's tenant-wide query
// does not exist yet (O-11); until it does the production client answers Unavailable and retirement is refused.
public interface IInventoryStockAvailabilityClient
{
    Task<InventoryStockCheck> CheckNonZeroStockAsync(
        Guid skuId,
        string skuLevel,
        CancellationToken cancellationToken = default);
}

public enum InventoryStockCheckOutcome
{
    Zero = 1,
    NonZero = 2,
    Unavailable = 3
}

// Unavailable carries why (unreachable, budget, 5xx, unreadable answer, partial legal-entity coverage).
public sealed record InventoryStockCheck(InventoryStockCheckOutcome Outcome, string? Cause = null)
{
    public static InventoryStockCheck Zero() => new(InventoryStockCheckOutcome.Zero);
    public static InventoryStockCheck NonZero() => new(InventoryStockCheckOutcome.NonZero);
    public static InventoryStockCheck Unavailable(string cause) => new(InventoryStockCheckOutcome.Unavailable, cause);
}
