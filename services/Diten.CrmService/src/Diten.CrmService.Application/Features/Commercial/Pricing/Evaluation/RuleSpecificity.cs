using Diten.CrmService.Domain.Entities.Commercial;

namespace Diten.CrmService.Application.Features.Commercial.Pricing.Evaluation;

/// <summary>
/// WP-VW-W6 W6-BE-b — MOD-0156 §4.6.2 (D8, §20.3 AÇIK-4): the most specific rule wins. segment + period (3) &gt; segment (2)
/// &gt; period (1) &gt; general (0). The first release is SEGMENT-LESS (§20.3 AÇIK-3): a rule with a segment is never a
/// candidate, so in practice period &gt; general.
/// </summary>
public static class RuleSpecificity
{
    /// <summary>§20.3 AÇIK-3 — the segment field is not yet a criterion (MOD-0149 / MOD-0048 field + set missing).</summary>
    public static readonly bool SegmentsEnabled = false;

    public static int Of(PricingRule rule)
    {
        var segment = !string.IsNullOrWhiteSpace(rule.SegmentCode);
        var period = rule.ValidFrom is not null || rule.ValidTo is not null;
        return (segment, period) switch
        {
            (true, true) => 3,
            (true, false) => 2,
            (false, true) => 1,
            _ => 0
        };
    }

    public static bool CoversDay(PricingRule rule, DateOnly day)
        => (rule.ValidFrom is null || rule.ValidFrom <= day) && (rule.ValidTo is null || rule.ValidTo >= day);

    /// <summary>The winning active rule of a SKU on a day for a pharmacy segment (null = no rule: no limit).</summary>
    public static PricingRule? Pick(IEnumerable<PricingRule> activeRulesOfSku, DateOnly day, string? pharmacySegmentCode)
    {
        var candidates = activeRulesOfSku
            .Where(r => r.IsActive() && !r.IsDeleted && CoversDay(r, day))
            .Where(r => string.IsNullOrWhiteSpace(r.SegmentCode)
                        || (SegmentsEnabled && string.Equals(r.SegmentCode, pharmacySegmentCode, StringComparison.Ordinal)))
            // a rule key counts once: its highest active version (a supersede that did not land never doubles a rule)
            .GroupBy(r => r.RuleKey)
            .Select(g => g.OrderByDescending(r => r.RuleVersion).First());
        return candidates
            .OrderByDescending(Of)
            .ThenByDescending(r => r.ValidFrom ?? DateOnly.MinValue)
            .ThenBy(r => r.RuleKey)
            .FirstOrDefault();
    }

    /// <summary>Two rules of the same (legal entity, SKU, segment) and the same specificity whose periods intersect (§4.3).</summary>
    public static bool Overlaps(PricingRule a, PricingRule b)
    {
        if (a.LegalEntityId != b.LegalEntityId || a.SkuId != b.SkuId
            || !string.Equals(a.SegmentCode ?? string.Empty, b.SegmentCode ?? string.Empty, StringComparison.Ordinal)
            || Of(a) != Of(b))
        {
            return false;
        }

        var aFrom = a.ValidFrom ?? DateOnly.MinValue;
        var aTo = a.ValidTo ?? DateOnly.MaxValue;
        var bFrom = b.ValidFrom ?? DateOnly.MinValue;
        var bTo = b.ValidTo ?? DateOnly.MaxValue;
        return aFrom <= bTo && bFrom <= aTo;
    }
}
