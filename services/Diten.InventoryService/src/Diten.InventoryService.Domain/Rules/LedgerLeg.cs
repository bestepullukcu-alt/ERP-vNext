using Diten.InventoryService.Domain.ValueObjects;

namespace Diten.InventoryService.Domain.Rules;

/// <summary>
/// One balance change of a posting: <paramref name="Delta"/> (base UoM, signed) applied to the row at
/// <paramref name="Dimension"/>. A negative leg must be covered by the row (DEC-INV-12) — the repository enforces
/// that with a conditional filter inside the posting transaction.
/// </summary>
public sealed record LedgerLeg(InventoryDimension Dimension, decimal Delta, string BaseUomId)
{
    public bool IsDecrease => Delta < 0m;
}
