namespace Diten.InventoryService.Persistence.Repositories;

/// <summary>
/// Aborts the surrounding Mongo transaction when a guarded decrease is not covered — a balance row (DEC-INV-12) or the
/// SKU's unreserved position (pack ASSUMPTION-0173-14). Carries the numbers for the contract <c>details</c>.
/// </summary>
internal sealed class UncoveredQuantityException : Exception
{
    public UncoveredQuantityException(decimal available, decimal requested, string? level = null)
        : base("A guarded decrease is not covered.")
    {
        Available = available;
        Requested = requested;
        Level = level;
    }

    /// <summary>MOD-0173-FU03 §8.2: <c>SKU</c> | <c>WAREHOUSE</c> | <c>LOT</c> when a reservation scope guard refused; null for a balance row.</summary>
    public string? Level { get; }

    public decimal Available { get; }

    public decimal Requested { get; }
}
