using Diten.ManufacturingService.Domain.Entities;

namespace Diten.ManufacturingService.Domain.Rules;

/// <summary>MOD-0193 saf iş kuralları — depo ya da saat bilmez; testler doğrudan çağırır.</summary>
public static class BomRules
{
    /// <summary>
    /// Tek seviye patlatma (ASSUMPTION-BOM-02): her bileşen için <c>quantity × bileşen miktarı</c>; aynı
    /// (bileşen, uom) satırları toplanır. Sıra: ilk görülme sırası (position'a göre).
    /// </summary>
    public static IReadOnlyList<BomRequirement> Explode(BomVersion bom, string quantity)
    {
        var result = new List<BomRequirement>();
        foreach (var line in bom.Components.OrderBy(c => c.Position))
        {
            var required = BomDecimal.Multiply(quantity, line.Quantity);
            var index = result.FindIndex(r => r.ComponentItemId == line.ComponentItemId && r.UomId == line.UomId);
            if (index < 0)
            {
                result.Add(new BomRequirement(line.ComponentItemId, required, line.UomId));
            }
            else
            {
                result[index] = result[index] with { RequiredQuantity = BomDecimal.Add(result[index].RequiredQuantity, required) };
            }
        }

        return result;
    }

    /// <summary>
    /// <paramref name="parentItemId"/> için <paramref name="componentItemIds"/> yürürlüğe girerse döngü oluşur mu.
    /// <paramref name="effectiveComponentsOf"/> bir item'ın şu an yürürlükteki BOM'unun bileşenlerini döner
    /// (yoksa boş). Bileşenlerden başlayıp aşağı doğru yürür; ana ürüne ulaşan yol döngüdür.
    /// </summary>
    public static async Task<bool> WouldCreateCycleAsync(
        Guid parentItemId,
        IEnumerable<Guid> componentItemIds,
        Func<IReadOnlyCollection<Guid>, Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>> effectiveComponentsOf,
        int maxDepth = 64)
    {
        var frontier = componentItemIds.Distinct().ToList();
        if (frontier.Contains(parentItemId))
        {
            return true;
        }

        var visited = new HashSet<Guid>(frontier);
        for (var depth = 0; depth < maxDepth && frontier.Count > 0; depth++)
        {
            var children = await effectiveComponentsOf(frontier);
            var next = new List<Guid>();
            foreach (var child in children.Values.SelectMany(x => x))
            {
                if (child == parentItemId)
                {
                    return true;
                }

                if (visited.Add(child))
                {
                    next.Add(child);
                }
            }

            frontier = next;
        }

        // Derinlik sınırı aşıldıysa yapı makul değildir; döngü varsayılır (fail-closed).
        return frontier.Count > 0;
    }

    /// <summary>ASSUMPTION-BOM-05: <c>asOfDate</c> o UTC gününün sonudur; verilmezse şimdi.</summary>
    public static DateTimeOffset ResolveAsOf(DateOnly? asOfDate, DateTimeOffset now) =>
        asOfDate is { } date
            ? new DateTimeOffset(date.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero)
            : now;

    /// <summary>Taslak ile yeni hali arasında değişen alanların ADLARI (AUD-001 §3: değer yazılmaz).</summary>
    public static List<string> ChangedFields(BomVersion before, string? description, IReadOnlyList<BomComponentLine> components, BomRouting? routing)
    {
        var changed = new List<string>();
        if (!string.Equals(before.Description, description, StringComparison.Ordinal))
        {
            changed.Add("description");
        }

        if (!ComponentsEqual(before.Components, components))
        {
            changed.Add("components");
        }

        if (!RoutingEqual(before.Routing, routing))
        {
            changed.Add("routing");
        }

        return changed;
    }

    private static bool ComponentsEqual(IReadOnlyList<BomComponentLine> a, IReadOnlyList<BomComponentLine> b) =>
        a.Count == b.Count && a.Zip(b).All(p =>
            p.First.ComponentItemId == p.Second.ComponentItemId
            && p.First.Quantity == p.Second.Quantity
            && p.First.UomId == p.Second.UomId
            && p.First.Position == p.Second.Position
            && (p.First.Alternates ?? []).SequenceEqual(p.Second.Alternates ?? []));

    private static bool RoutingEqual(BomRouting? a, BomRouting? b)
    {
        if (a is null || b is null)
        {
            return (a?.Steps.Count ?? 0) == 0 && (b?.Steps.Count ?? 0) == 0;
        }

        return a.Steps.Count == b.Steps.Count && a.Steps.Zip(b.Steps).All(p =>
            p.First.StepNo == p.Second.StepNo && p.First.Operation == p.Second.Operation && p.First.WorkCenter == p.Second.WorkCenter);
    }
}

public sealed record BomRequirement(Guid ComponentItemId, string RequiredQuantity, string UomId);
