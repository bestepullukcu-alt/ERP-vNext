using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.CycleCapacity.Rules;
using Diten.CrmService.Application.Features.Knowledge.Content;
using Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney;
using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.VisitContentSequence;

/// <summary>
/// MOD-0155 FU04 — <b>Visit Content Sequence resolver</b>. It answers exactly one question for a planned visit to a
/// doctor: <i>"which content stage comes NEXT, and how long does that make the visit?"</i>
/// <para><b>This is NOT an engine (D8).</b> It produces no plan, packs nothing, mutates no journey / strategy / segment
/// / capacity, and — like the FU06B calculator it calls — <b>persists NOTHING</b>. Its only I/O is READ calls to
/// already-shipped seams; the storage of the chosen position stays FU01's <see cref="PlannedVisitContentRef"/>, which
/// the FU01 handler writes from this result (2.2 boundary). "Auto-advance" is a deterministic ordinal step on the
/// pinned published journey — no scoring, no "best journey", no branch evaluation.</para>
/// <para><b>The chain (§1.3).</b> doctor → (optional segment membership gate) → StrategyTemplate ("play") → bound
/// ContentEngagementJourney → ordered ACTIVE stages → NEXT stage (priorIndex + 1) → content set → promo / non-promo
/// split against the play's promoted product lines (D-CONTENT-SPLIT = C, §4.5) → FU06B
/// <see cref="ActivityTimeBudgetCalculator.VisitDuration"/>. Every gap is a CODED <see cref="VisitContentSequenceStatus"/>
/// + reason code, never a silent default.</para>
/// </summary>
public sealed class VisitContentSequenceResolver
{
    private readonly ITenantContext _tenant;
    private readonly IStrategyTemplateReader _strategies;
    private readonly ISegmentMembershipReader _segments;
    private readonly IContentEngagementJourneyReader _journeys;
    private readonly IKnowledgeContentLinkageReader _content;
    private readonly ICycleCapacityRepository _capacities;

    public VisitContentSequenceResolver(
        ITenantContext tenant,
        IStrategyTemplateReader strategies,
        ISegmentMembershipReader segments,
        IContentEngagementJourneyReader journeys,
        IKnowledgeContentLinkageReader content,
        ICycleCapacityRepository capacities)
    {
        _tenant = tenant;
        _strategies = strategies;
        _segments = segments;
        _journeys = journeys;
        _content = content;
        _capacities = capacities;
    }

    public async Task<VisitContentSequenceResult> ResolveAsync(
        VisitContentSequenceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var at = request.EffectiveAt ?? DateTimeOffset.UtcNow;

        // 1 ─ Resolve the play (StrategyTemplate). Direct id wins; otherwise the doctor's segment. When both a segment
        //     and a subject are given, membership is the optional gate (unknown is never a member — the reader's rule).
        var bindings = await ResolveBindingsAsync(request, at, cancellationToken);
        if (bindings is null)
        {
            return VisitContentSequenceResult.NotResolved(
                VisitContentSequenceStatus.NoStrategy,
                new[] { VisitContentSequenceReasonCodes.StrategyNotFound }, at);
        }

        // 2 ─ Resolve the bound journey (first content-engagement-journey binding, in author sort order).
        var journeyId = bindings.ContentBindings
            .Where(c => string.Equals(
                c.ContentRefType, StrategyContentRefTypes.ContentEngagementJourney, StringComparison.Ordinal))
            .OrderBy(c => c.SortOrder)
            .Select(c => (Guid?)c.ContentRefId)
            .FirstOrDefault();

        if (journeyId is not { } jid || jid == Guid.Empty)
        {
            return VisitContentSequenceResult.NotResolved(
                VisitContentSequenceStatus.NoJourney,
                new[] { VisitContentSequenceReasonCodes.JourneyNotPublished }, at,
                strategyTemplateId: bindings.TemplateId);
        }

        // The published + effective journey context (subject / topic / audience / language) drives the content set.
        var published = await _journeys.ResolvePublishedJourneysAsync(
            new ContentEngagementJourneyCriteria(EffectiveAt: at), cancellationToken);
        var journey = published.FirstOrDefault(j => j.JourneyId == jid);
        if (journey is null)
        {
            return VisitContentSequenceResult.NotResolved(
                VisitContentSequenceStatus.NoJourney,
                new[] { VisitContentSequenceReasonCodes.JourneyNotPublished }, at,
                journeyId: jid, strategyTemplateId: bindings.TemplateId);
        }

        // 3 ─ Ordered ACTIVE stages, through the named seam (published + effective + StageOrder → StageCode).
        var stages = await _journeys.GetOrderedStagesAsync(jid, at, cancellationToken);
        if (stages.Count == 0)
        {
            return VisitContentSequenceResult.NotResolved(
                VisitContentSequenceStatus.NoJourney,
                new[] { VisitContentSequenceReasonCodes.JourneyNotPublished }, at,
                journeyId: jid, strategyTemplateId: bindings.TemplateId);
        }

        // 4 ─ Next-stage (deterministic ordinal). First visit (no prior index) → index 0; otherwise prior + 1.
        var nextIndex = request.PriorStageIndex is { } prior ? prior + 1 : 0;

        // D-END-OF-JOURNEY = flag: past the last stage the resolver STOPS — no loop, no wrap-around, no repeat (§4.4).
        if (nextIndex > stages.Count - 1)
        {
            return VisitContentSequenceResult.NotResolved(
                VisitContentSequenceStatus.EndOfJourney,
                new[] { VisitContentSequenceReasonCodes.JourneyCompleted }, at,
                journeyId: jid, strategyTemplateId: bindings.TemplateId);
        }

        var nextStage = stages[Math.Max(0, nextIndex)];

        // 5 ─ Promo / non-promo split = content set JOINED against the play's promoted product lines (D-CONTENT-SPLIT=C).
        var reasons = new List<string>();
        var (promoCount, nonPromoCount) = await SplitContentAsync(bindings, journey, at, reasons, cancellationToken);

        // 6 ─ Duration = FU06B calculator over the two counts. FU04 supplies the numbers; it never does the arithmetic.
        var duration = await ComputeDurationAsync(request, promoCount, nonPromoCount, reasons, cancellationToken);

        return new VisitContentSequenceResult(
            VisitContentSequenceStatus.Resolved,
            jid,
            nextStage.StageId,
            nextIndex,
            nextStage.StageCode,
            nextStage.StageName,
            PlannedVisitContentSource.Strategy,
            bindings.TemplateId,
            promoCount,
            nonPromoCount,
            duration,
            reasons,
            at);
    }

    /// <summary>The play's bindings, or null when none resolves (fail-closed — no default play is invented).</summary>
    private async Task<StrategyTemplateBindingSet?> ResolveBindingsAsync(
        VisitContentSequenceRequest request, DateTimeOffset at, CancellationToken cancellationToken)
    {
        if (request.StrategyTemplateId is { } templateId && templateId != Guid.Empty)
        {
            return await _strategies.GetActiveBindingsAsync(templateId, at, cancellationToken);
        }

        if (request.SegmentId is not { } segmentId || segmentId == Guid.Empty)
        {
            return null;
        }

        // Optional membership gate: only when the doctor id is known. unknown is never a member (the seam's contract),
        // so a doctor who is not in the segment has no play through it.
        if (request.SubjectId != Guid.Empty && !string.IsNullOrWhiteSpace(request.SubjectType))
        {
            var verdict = await _segments.IsMemberAsync(
                segmentId, request.SubjectType.Trim(), request.SubjectId, at, cancellationToken);
            if (!verdict.IsMember)
            {
                return null;
            }
        }

        var summaries = await _strategies.ListBySegmentAsync(segmentId, at, cancellationToken);
        var first = summaries
            .OrderBy(s => s.TemplateCode, StringComparer.Ordinal)
            .ThenBy(s => s.TemplateVersion)
            .FirstOrDefault();

        return first is null
            ? null
            : await _strategies.GetActiveBindingsAsync(first.TemplateId, at, cancellationToken);
    }

    /// <summary>
    /// Promo / non-promo split (§4.5). A content item is <b>promo</b> when its product is one the play promotes;
    /// everything else is non-promo. Fail-closed: when the play promotes nothing resolvable the counts are ZERO and
    /// <c>content_split_unresolved</c> is coded, so the duration is ReportDuration only — a wrong promo figure is never
    /// produced.
    /// </summary>
    private async Task<(int Promo, int NonPromo)> SplitContentAsync(
        StrategyTemplateBindingSet bindings, ContentEngagementJourneyDto journey, DateTimeOffset at,
        List<string> reasons, CancellationToken cancellationToken)
    {
        var promoted = new HashSet<Guid>();
        foreach (var line in bindings.ProductLines)
        {
            if (line.GlobalProductId != Guid.Empty)
            {
                promoted.Add(line.GlobalProductId);
            }

            foreach (var sku in line.SkuAllocations)
            {
                if (sku.GskuId != Guid.Empty)
                {
                    promoted.Add(sku.GskuId);
                }
            }
        }

        if (promoted.Count == 0)
        {
            reasons.Add(VisitContentSequenceReasonCodes.ContentSplitUnresolved);
            return (0, 0);
        }

        // The content set the play presents on this journey: published + effective knowledge content that matches the
        // journey's subject / topic / audience / language context. Each item carries the product it is tied to
        // (KnowledgeContentDto.ProductId) — the existing content→product binding FU04 READS, never creates (§19.1).
        var contentItems = await _content.ResolvePublishedContentAsync(
            new KnowledgeContentLinkageCriteria(
                SubjectId: journey.SubjectId,
                TopicId: journey.TopicId,
                AudienceProfileId: journey.AudienceProfileId,
                LanguageCode: journey.LanguageCode,
                EffectiveAt: at),
            cancellationToken);

        var promo = 0;
        var nonPromo = 0;
        foreach (var item in contentItems)
        {
            if (item.ProductId is { } productId && productId != Guid.Empty && promoted.Contains(productId))
            {
                promo++;
            }
            else
            {
                nonPromo++;
            }
        }

        return (promo, nonPromo);
    }

    /// <summary>The visit duration, delegated to FU06B. When no capacity is pinned to the period, the duration is 0 and
    /// <c>capacity_not_found</c> is coded — no arithmetic is attempted (V5/§4.3).</summary>
    private async Task<int> ComputeDurationAsync(
        VisitContentSequenceRequest request, int promoCount, int nonPromoCount, List<string> reasons,
        CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId
            || request.CyclePeriodId is not { } cyclePeriodId || cyclePeriodId == Guid.Empty)
        {
            reasons.Add(VisitContentSequenceReasonCodes.CapacityNotFound);
            return 0;
        }

        var capacity = await _capacities.GetByCyclePeriodAsync(tenantId, cyclePeriodId, cancellationToken);
        if (capacity is null)
        {
            reasons.Add(VisitContentSequenceReasonCodes.CapacityNotFound);
            return 0;
        }

        return ActivityTimeBudgetCalculator.VisitDuration(capacity, promoCount, nonPromoCount);
    }
}
