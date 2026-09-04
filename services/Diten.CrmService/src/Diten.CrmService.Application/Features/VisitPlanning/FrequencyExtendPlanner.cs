using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Queries;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Resolve;

namespace Diten.CrmService.Application.Features.VisitPlanning;

/// <summary>
/// MOD-0155 FU05 — frequency-extend of weeks 2..n (§4.1 ⑦, D-FREQUENCY-EXTEND = B, LOCKED). For each week-1 target it
/// calls MOD-0165 <see cref="IVisitFrequencyPolicyResolver"/> (READ-ONLY, signature unchanged) to resolve the target's
/// cadence, then reports HOW MANY weeks the visit should repeat across. The engine re-runs the route PER WEEK for the
/// extended weeks so each week stays route-continuous (a one-pass whole-month VRP is deferred behind FU03's F-SOLVER).
/// <para>It holds no algorithm of its own: the cadence is the resolver's number; this class only maps
/// <c>RequiredVisitCount</c> onto week slots, deterministically, capped by the period's week count.</para>
/// </summary>
public sealed class FrequencyExtendPlanner
{
    private readonly IVisitFrequencyPolicyResolver _frequency;

    public FrequencyExtendPlanner(IVisitFrequencyPolicyResolver frequency) => _frequency = frequency;

    /// <summary>
    /// The week indices (0-based) a target's visit should occupy across the period. Week 0 (the base week) is always
    /// included; additional weeks are added at the resolved cadence up to <paramref name="weekCount"/>. When frequency
    /// is unknown (no policy — a default is NEVER invented) only week 0 is returned.
    /// </summary>
    public async Task<FrequencyExtendResult> ResolveWeeksAsync(
        string targetType,
        Guid targetId,
        Guid? segmentId,
        Guid? campaignId,
        DateTimeOffset effectiveAt,
        int weekCount,
        CancellationToken cancellationToken)
    {
        var weeks = new List<int> { 0 };
        if (weekCount <= 1 || targetId == Guid.Empty)
        {
            return new FrequencyExtendResult(weeks, FrequencyStatus.Unknown, null);
        }

        var result = await _frequency.ResolveAsync(
            new ResolveVisitFrequencyPolicyQuery(
                TargetType: targetType,
                TargetId: targetId,
                EffectiveAt: effectiveAt,
                SegmentId: segmentId,
                CampaignId: campaignId),
            cancellationToken);

        // Unknown / no policy → base week only; a cadence is never fabricated (the resolver's contract).
        if (result.RequiredVisitCount is not { } required || required <= 1)
        {
            return new FrequencyExtendResult(weeks, result.FrequencyStatus, result.RequiredVisitCount);
        }

        // Spread the required visits evenly across the period's weeks (base week already counted). Deterministic: even
        // stride, never past the last week.
        var target = Math.Min(required, weekCount);
        if (target <= 1)
        {
            return new FrequencyExtendResult(weeks, result.FrequencyStatus, required);
        }

        var stride = (double)weekCount / target;
        for (var i = 1; i < target; i++)
        {
            var week = (int)Math.Round(i * stride, MidpointRounding.AwayFromZero);
            if (week >= weekCount)
            {
                week = weekCount - 1;
            }

            if (!weeks.Contains(week))
            {
                weeks.Add(week);
            }
        }

        weeks.Sort();
        return new FrequencyExtendResult(weeks, result.FrequencyStatus, required);
    }
}

/// <summary>The weeks a target repeats across + the resolved cadence provenance.</summary>
public sealed record FrequencyExtendResult(
    IReadOnlyList<int> WeekIndices,
    string FrequencyStatus,
    int? RequiredVisitCount);
