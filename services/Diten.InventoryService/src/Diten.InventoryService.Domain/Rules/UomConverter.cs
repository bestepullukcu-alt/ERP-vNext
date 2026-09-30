namespace Diten.InventoryService.Domain.Rules;

/// <summary>
/// Converts a posted quantity to the SKU's base UoM using the declared PRODUCT-MASTER conversions (pack §4
/// <c>BaseQuantity</c>, §12 <c>UOM_CONVERSION_MISSING</c>). Only declared directions are followed (a declared
/// <c>BOX→EA</c> does not imply <c>EA→BOX</c>); chains such as <c>PALLET→BOX→EA</c> are followed up to
/// <see cref="MaxHops"/> steps. A result that needs more than <see cref="InventoryQuantity.MaxFractionDigits"/>
/// fraction digits is refused rather than rounded — the ledger never loses precision silently.
/// </summary>
public static class UomConverter
{
    public const int MaxHops = 4;

    public static bool TryConvertToBase(
        decimal quantity,
        string uomId,
        string baseUomId,
        IEnumerable<UomConversionRule> conversions,
        DateTimeOffset asOf,
        out decimal baseQuantity)
    {
        baseQuantity = 0m;
        if (string.Equals(uomId, baseUomId, StringComparison.Ordinal))
        {
            baseQuantity = quantity;
            return true;
        }

        var edges = conversions
            .Where(c => c.Denominator != 0m && c.Numerator > 0m && c.Denominator > 0m && c.IsEffectiveAt(asOf))
            .ToList();

        var frontier = new List<(string Uom, decimal Factor)> { (uomId, 1m) };
        var visited = new HashSet<string>(StringComparer.Ordinal) { uomId };

        for (var hop = 0; hop < MaxHops && frontier.Count > 0; hop++)
        {
            var next = new List<(string Uom, decimal Factor)>();
            foreach (var (uom, factor) in frontier)
            {
                foreach (var edge in edges.Where(e => string.Equals(e.FromUom, uom, StringComparison.Ordinal)))
                {
                    if (!visited.Add(edge.ToUom))
                    {
                        continue;
                    }

                    decimal stepFactor;
                    try
                    {
                        stepFactor = factor * edge.Numerator / edge.Denominator;
                    }
                    catch (OverflowException)
                    {
                        continue;
                    }

                    if (string.Equals(edge.ToUom, baseUomId, StringComparison.Ordinal))
                    {
                        try
                        {
                            baseQuantity = quantity * stepFactor;
                        }
                        catch (OverflowException)
                        {
                            return false;
                        }

                        return InventoryQuantity.Scale(baseQuantity) <= InventoryQuantity.MaxFractionDigits;
                    }

                    next.Add((edge.ToUom, stepFactor));
                }
            }

            frontier = next;
        }

        return false;
    }
}
