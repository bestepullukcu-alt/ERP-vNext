using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.CycleCapacity.Rules;
using Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney;
using Diten.CrmService.Application.Features.Knowledge.Path.Release;
using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using CapacityEntity = Diten.CrmService.Domain.Entities.CycleCapacity;

namespace Diten.CrmService.Application.Features.VisitContentSequence;

/// <summary>
/// MOD-0155 FU04 — <b>Visit Content Sequence resolver</b>. It answers exactly one question for a planned visit to a
/// doctor: <i>"which content does the visit tell, and how long does that make it?"</i>
/// <para><b>This is NOT an engine (D8).</b> It produces no plan, packs nothing, mutates no journey / strategy / segment
/// / capacity / progress, and — like the FU06B calculator it calls — <b>persists NOTHING</b>. Its only I/O is READ calls
/// to already-shipped seams; the planning engine freezes the result onto the PlannedVisit (2.2 boundary).</para>
/// <para><b>v2 (WP-SB-3b, DESIGN-SB-3 §3.3).</b> doctor → (optional segment membership gate) → StrategyTemplate ("play")
/// → its product lines split by role (promo / non-promo; template-level content bindings are NOT read, S3-2) → per role
/// the weighted rotation (<see cref="VisitContentRotation"/>, S3-5) up to the cycle capacity's MaxPromoProducts /
/// MaxNonPromoProducts → per product: the line's journey → the doctor's <c>JourneyProgress</c> stage (+ the visits
/// planned before this one) over the journey's ordered active stages, wrapping back to the first (S3-7) → the stage's
/// path version (the current release for <c>latest-published</c>, the pinned version otherwise — KP-3 release rules)
/// → its active steps and claims. A product that cannot resolve is DROPPED with a coded reason and the next candidate
/// takes its place. Duration = FU06B <see cref="ActivityTimeBudgetCalculator.VisitDuration"/> over the PRODUCT counts.</para>
/// </summary>
public sealed class VisitContentSequenceResolver
{
    private readonly ITenantContext _tenant;
    private readonly IStrategyTemplateReader _strategies;
    private readonly ISegmentMembershipReader _segments;
    private readonly IContentEngagementJourneyReader _journeys;
    private readonly ICycleCapacityRepository _capacities;
    private readonly IVisitContentSourceReader _sources;

    public VisitContentSequenceResolver(
        ITenantContext tenant,
        IStrategyTemplateReader strategies,
        ISegmentMembershipReader segments,
        IContentEngagementJourneyReader journeys,
        ICycleCapacityRepository capacities,
        IVisitContentSourceReader sources)
    {
        _tenant = tenant;
        _strategies = strategies;
        _segments = segments;
        _journeys = journeys;
        _capacities = capacities;
        _sources = sources;
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

        // 2 ─ The candidates are the play's product lines (never its template-level content bindings, S3-2).
        var lines = bindings.ProductLines.Where(l => l.GlobalProductId != Guid.Empty).ToList();
        if (lines.Count == 0)
        {
            return VisitContentSequenceResult.NotResolved(
                VisitContentSequenceStatus.NoJourney,
                new[] { VisitContentSequenceReasonCodes.ContentSplitUnresolved }, at,
                strategyTemplateId: bindings.TemplateId);
        }

        var capacity = await LoadCapacityAsync(request, cancellationToken);
        var context = new ResolutionContext(
            request, at,
            await _sources.ListProgressAsync(request.SubjectId, cancellationToken),
            (request.PendingExposures ?? Array.Empty<VisitContentPendingExposure>())
                .Where(p => p.Count > 0)
                .GroupBy(p => (p.ProductId, p.JourneyId))
                .ToDictionary(g => g.Key, g => g.Sum(p => p.Count)),
            (await _journeys.ResolvePublishedJourneysAsync(
                    new ContentEngagementJourneyCriteria(EffectiveAt: at), cancellationToken))
                .GroupBy(j => j.JourneyId)
                .ToDictionary(g => g.Key, g => g.First()));

        // 3 ─ Per role: weighted rotation, then walk the order until the role's limit is met (a dropped product's place
        //     goes to the next candidate).
        var reasons = new List<string>();
        var items = new List<VisitContentItem>();
        foreach (var role in new[] { StrategyProductLineRoles.Promo, StrategyProductLineRoles.NonPromo })
        {
            var max = role == StrategyProductLineRoles.Promo
                ? capacity?.EffectiveMaxPromoProducts() ?? CycleCapacityLimits.DefaultMaxProductsPerVisit
                : capacity?.EffectiveMaxNonPromoProducts() ?? CycleCapacityLimits.DefaultMaxProductsPerVisit;

            var candidates = lines
                .Where(l => RoleOf(l) == role)
                .Select(l => new VisitContentRotationCandidate<StrategyTemplateProductMixLine>(
                    l, l.LineWeightPercentage, l.SortOrder, l.LineId, context.ExposureOf(l)))
                .ToList();

            var taken = 0;
            foreach (var candidate in VisitContentRotation.Order(candidates))
            {
                if (taken >= max)
                {
                    break;
                }

                var item = await BuildItemAsync(candidate.Line, role, context, reasons, cancellationToken);
                if (item is null)
                {
                    continue;
                }

                items.Add(item);
                taken++;
            }
        }

        if (items.Count == 0)
        {
            return VisitContentSequenceResult.NotResolved(
                VisitContentSequenceStatus.NoJourney, reasons.Distinct(StringComparer.Ordinal).ToList(), at,
                strategyTemplateId: bindings.TemplateId);
        }

        // 4 ─ Duration = FU06B calculator over the PRODUCT counts. FU04 supplies the numbers; it never does the arithmetic.
        var promoCount = items.Count(i => i.Role == StrategyProductLineRoles.Promo);
        var nonPromoCount = items.Count - promoCount;
        var duration = ComputeDuration(capacity, promoCount, nonPromoCount, reasons);

        // Backward compatibility: the top-level journey / stage is the first promo item (else the first item).
        var lead = items.FirstOrDefault(i => i.Role == StrategyProductLineRoles.Promo) ?? items[0];
        return new VisitContentSequenceResult(
            VisitContentSequenceStatus.Resolved,
            lead.JourneyId,
            lead.StageId,
            lead.StageIndex,
            lead.StageCode,
            lead.StageName,
            PlannedVisitContentSource.Strategy,
            bindings.TemplateId,
            promoCount,
            nonPromoCount,
            duration,
            reasons.Distinct(StringComparer.Ordinal).ToList(),
            at,
            items);
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

        // WP-E2E-FIX-3 (E5-B2) — the reader's single effective-version rule decides; no local re-sort.
        var summaries = await _strategies.ListBySegmentAsync(segmentId, at, cancellationToken);
        var first = StrategyTemplateReader.InPreferenceOrder(summaries).FirstOrDefault();

        return first is null
            ? null
            : await _strategies.GetActiveBindingsAsync(first.TemplateId, at, cancellationToken);
    }

    /// <summary>One product → its item, or null (dropped, the reason coded). Never throws on missing data.</summary>
    private async Task<VisitContentItem?> BuildItemAsync(
        StrategyTemplateProductMixLine line, string role, ResolutionContext context, List<string> reasons,
        CancellationToken cancellationToken)
    {
        // A line written before SB-3a has no journey: nothing to tell for that product.
        if (line.JourneyId is not { } journeyId || journeyId == Guid.Empty)
        {
            reasons.Add(VisitContentSequenceReasonCodes.ProductHasNoJourney);
            return null;
        }

        var stages = await context.StagesAsync(journeyId, _journeys, cancellationToken);
        if (!context.Journeys.TryGetValue(journeyId, out var journey) || stages.Count == 0)
        {
            reasons.Add(VisitContentSequenceReasonCodes.JourneyUnpublished);
            return null;
        }

        // The doctor's position on this product's journey (+ the visits planned before this one), wrapping at the end.
        var warnings = new List<string>();
        var stageIndex = 0;
        if (context.ProgressOf(line.GlobalProductId, journeyId) is { } progress)
        {
            if (progress.IsStageIndexStale(stages.Count))
            {
                warnings.Add(VisitContentSequenceReasonCodes.StageIndexReset);
                reasons.Add(VisitContentSequenceReasonCodes.StageIndexReset);
            }

            stageIndex = progress.EffectiveStageIndex(stages.Count);
        }

        stageIndex = JourneyProgress.StageAfter(stageIndex, context.PendingOf(line.GlobalProductId, journeyId), stages.Count);
        var stage = stages[stageIndex];

        var path = ResolveStagePath(stage, await context.PathsAsync(_sources, cancellationToken), context.At);
        if (path is null)
        {
            reasons.Add(VisitContentSequenceReasonCodes.StagePathUnpublished);
            return null;
        }

        if (journey.AudienceProfileId is { } profileId && profileId != Guid.Empty
            && VisitContentAudiencePolicy.Covers(
                await context.AudienceAsync(profileId, _sources, cancellationToken),
                await context.SpecialtyAsync(_sources, cancellationToken)) == false)
        {
            reasons.Add(VisitContentSequenceReasonCodes.JourneyAudienceMismatch);
            if (VisitContentAudiencePolicy.DropOnMismatch)
            {
                return null;
            }

            warnings.Add(VisitContentSequenceReasonCodes.JourneyAudienceMismatch);
        }

        return new VisitContentItem(
            line.GlobalProductId,
            line.GlobalProductCodeDisplay,
            role,
            journeyId,
            journey.JourneyCode,
            stage.StageId,
            stageIndex,
            stage.StageCode,
            stage.StageName,
            path.Id,
            path.PathCode,
            path.PathVersion,
            path.OrderedActiveSteps()
                .Select(s => new VisitContentStep(
                    s.StepId, s.ContentId, s.ContentCode, s.StepTitle, s.StepType, s.EstimatedDurationMinutes))
                .ToList(),
            path.Claims.Select(c => new VisitContentClaim(c.ClaimId, c.ClaimCode)).ToList(),
            warnings);
    }

    /// <summary>
    /// The path version a stage tells, by the KP-3 release definition (<see cref="KnowledgePathReleaseRules"/>):
    /// <c>latest-published</c> → the current release of the same PathCode + country + language as the stage's path;
    /// <c>pinned</c> → the pinned version, only while it is still the released one (published, not archived, effective).
    /// A pinned version that a later release made inactive is NOT silently replaced: null (the product drops).
    /// </summary>
    public static KnowledgePath? ResolveStagePath(
        ContentEngagementJourneyStageDto stage, IReadOnlyList<KnowledgePath> paths, DateTimeOffset at)
    {
        var recommended = paths.FirstOrDefault(p => p.Id == stage.RecommendedKnowledgePathId);
        if (string.Equals(
                stage.PathVersionPinPolicy, ContentEngagementJourneyPathPin.LatestPublished, StringComparison.OrdinalIgnoreCase))
        {
            return recommended is null
                ? null
                : KnowledgePathReleaseRules.CurrentReleaseOf(
                    paths,
                    string.IsNullOrWhiteSpace(stage.PathCode) ? recommended.PathCode : stage.PathCode,
                    recommended.CountryCode, recommended.LanguageCode, at);
        }

        return KnowledgePathReleaseRules.IsCurrentRelease(recommended) && recommended!.IsEffectiveAt(at) ? recommended : null;
    }

    private static string RoleOf(StrategyTemplateProductMixLine line)
        => string.Equals(line.Role?.Trim(), StrategyProductLineRoles.NonPromo, StringComparison.OrdinalIgnoreCase)
            ? StrategyProductLineRoles.NonPromo
            : StrategyProductLineRoles.Promo;

    private async Task<CapacityEntity?> LoadCapacityAsync(
        VisitContentSequenceRequest request, CancellationToken cancellationToken)
        => _tenant.TenantId is { } tenantId && request.CyclePeriodId is { } cyclePeriodId && cyclePeriodId != Guid.Empty
            ? await _capacities.GetByCyclePeriodAsync(tenantId, cyclePeriodId, cancellationToken)
            : null;

    /// <summary>The visit duration, delegated to FU06B. When no capacity is pinned to the period, the duration is 0 and
    /// <c>capacity_not_found</c> is coded — no arithmetic is attempted (V5/§4.3).</summary>
    private static int ComputeDuration(CapacityEntity? capacity, int promoCount, int nonPromoCount, List<string> reasons)
    {
        if (capacity is null)
        {
            reasons.Add(VisitContentSequenceReasonCodes.CapacityNotFound);
            return 0;
        }

        return ActivityTimeBudgetCalculator.VisitDuration(capacity, promoCount, nonPromoCount);
    }

    /// <summary>The per-call read state: progress, pending projection, journeys and lazily read paths / stages / audience.</summary>
    private sealed class ResolutionContext
    {
        private readonly VisitContentSequenceRequest _request;
        private readonly IReadOnlyList<JourneyProgress> _progress;
        private readonly IReadOnlyDictionary<(Guid ProductId, Guid JourneyId), int> _pending;
        private readonly Dictionary<Guid, IReadOnlyList<ContentEngagementJourneyStageDto>> _stages = new();
        private readonly Dictionary<Guid, AudienceProfile?> _audiences = new();
        private IReadOnlyList<KnowledgePath>? _paths;
        private (bool Read, string? Value) _specialty;

        public ResolutionContext(
            VisitContentSequenceRequest request,
            DateTimeOffset at,
            IReadOnlyList<JourneyProgress> progress,
            IReadOnlyDictionary<(Guid ProductId, Guid JourneyId), int> pending,
            IReadOnlyDictionary<Guid, ContentEngagementJourneyDto> journeys)
        {
            _request = request;
            At = at;
            _progress = progress;
            _pending = pending;
            Journeys = journeys;
        }

        public DateTimeOffset At { get; }

        public IReadOnlyDictionary<Guid, ContentEngagementJourneyDto> Journeys { get; }

        public JourneyProgress? ProgressOf(Guid productId, Guid journeyId)
            => _progress.FirstOrDefault(p => p.ProductId == productId && p.JourneyId == journeyId && !p.IsDeleted);

        public int PendingOf(Guid productId, Guid journeyId)
            => _pending.TryGetValue((productId, journeyId), out var count) ? count : 0;

        /// <summary>Times the doctor was told the line's product on the line's journey (completed + planned before).</summary>
        public int ExposureOf(StrategyTemplateProductMixLine line)
            => line.JourneyId is { } journeyId
                ? (ProgressOf(line.GlobalProductId, journeyId)?.ExposureCount ?? 0) + PendingOf(line.GlobalProductId, journeyId)
                : 0;

        public async Task<IReadOnlyList<ContentEngagementJourneyStageDto>> StagesAsync(
            Guid journeyId, IContentEngagementJourneyReader reader, CancellationToken cancellationToken)
        {
            if (!_stages.TryGetValue(journeyId, out var stages))
            {
                stages = await reader.GetOrderedStagesAsync(journeyId, At, cancellationToken);
                _stages[journeyId] = stages;
            }

            return stages;
        }

        public async Task<IReadOnlyList<KnowledgePath>> PathsAsync(
            IVisitContentSourceReader sources, CancellationToken cancellationToken)
            => _paths ??= await sources.ListPathsAsync(cancellationToken);

        public async Task<AudienceProfile?> AudienceAsync(
            Guid profileId, IVisitContentSourceReader sources, CancellationToken cancellationToken)
        {
            if (!_audiences.TryGetValue(profileId, out var profile))
            {
                profile = await sources.GetAudienceProfileAsync(profileId, cancellationToken);
                _audiences[profileId] = profile;
            }

            return profile;
        }

        public async Task<string?> SpecialtyAsync(IVisitContentSourceReader sources, CancellationToken cancellationToken)
        {
            if (!_specialty.Read)
            {
                _specialty = (true, await sources.GetContactSpecialtyAsync(_request.SubjectId, cancellationToken));
            }

            return _specialty.Value;
        }
    }
}
