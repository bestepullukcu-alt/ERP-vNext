namespace Diten.CrmService.Application.Features.VisitContentSequence;

/// <summary>WP-SB-3b — one rotation candidate: a strategy line of one role, its weight / author order and how many times
/// the doctor was already told the product (<see cref="Exposure"/>).</summary>
public sealed record VisitContentRotationCandidate<T>(T Line, decimal? Weight, int SortOrder, Guid TieBreakId, int Exposure);

/// <summary>
/// WP-SB-3b (DESIGN-SB-3 S3-5) — the <b>weighted rotation</b> that picks which products of one role a visit tells. Pure
/// and deterministic: no clock, no randomness, no I/O.
/// <list type="bullet">
/// <item>weight = the line's <c>LineWeightPercentage</c>; when any line of the role has none, every line counts equally;</item>
/// <item>deserved = (Σ exposure of the role) × weight / Σ weight; <b>gap = deserved − exposure</b>;</item>
/// <item>order: largest gap first; on a tie the higher weight, then the author <c>SortOrder</c> (then the line id).</item>
/// </list>
/// On a first visit every gap is 0, so the order is weight then SortOrder. The caller walks the order and takes the
/// first <c>MaxPromoProducts</c> / <c>MaxNonPromoProducts</c> products that resolve (a dropped product's place goes to
/// the next one).
/// </summary>
public static class VisitContentRotation
{
    public static IReadOnlyList<VisitContentRotationCandidate<T>> Order<T>(IReadOnlyList<VisitContentRotationCandidate<T>> candidates)
    {
        if (candidates.Count == 0)
        {
            return candidates;
        }

        var weighted = candidates.All(c => c.Weight is > 0m);
        decimal WeightOf(VisitContentRotationCandidate<T> c) => weighted ? c.Weight!.Value : 1m;

        var totalExposure = candidates.Sum(c => (decimal)Math.Max(0, c.Exposure));
        var totalWeight = candidates.Sum(WeightOf);

        return candidates
            .Select(c => (Candidate: c, Weight: WeightOf(c), Gap: Gap(totalExposure, WeightOf(c), totalWeight, c.Exposure)))
            .OrderByDescending(x => x.Gap)
            .ThenByDescending(x => x.Weight)
            .ThenBy(x => x.Candidate.SortOrder)
            .ThenBy(x => x.Candidate.TieBreakId)
            .Select(x => x.Candidate)
            .ToList();
    }

    /// <summary>How far a product is behind its share: deserved − told.</summary>
    public static decimal Gap(decimal totalExposure, decimal weight, decimal totalWeight, int exposure)
        => (totalWeight <= 0m ? 0m : totalExposure * weight / totalWeight) - Math.Max(0, exposure);
}
