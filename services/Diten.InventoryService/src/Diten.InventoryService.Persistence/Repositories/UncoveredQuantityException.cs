namespace Diten.InventoryService.Persistence.Repositories;

/// <summary>
/// Aborts the surrounding Mongo transaction when a guarded decrease is not covered — a balance row (DEC-INV-12) or the
/// SKU's unreserved position (pack ASSUMPTION-0173-14). Carries the numbers for the contract <c>details</c>.
/// </summary>
internal sealed class UncoveredQuantityException : Exception
{
    public UncoveredQuantityException(decimal available, decimal requested)
        : base("A guarded decrease is not covered.")
    {
        Available = available;
        Requested = requested;
    }

    public decimal Available { get; }

    public decimal Requested { get; }
}
