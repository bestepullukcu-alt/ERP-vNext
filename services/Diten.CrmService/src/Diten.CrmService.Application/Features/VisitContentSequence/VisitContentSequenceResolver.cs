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
        //     WP-VP-3C (K-7a) — a play is no longer required: without one the rep's pick is the list.
        var bindings = await ResolveBindingsAsync(request, at, cancellationToken);

        // 2 ─ The candidates (K-7, the ONE list rule): ① the play's product lines (never its template-level content
        //     bindings, S3-2; role from the line, locked S-3) → ② the rep's pick (rep-pick; role from the pick, null =
        //     promo K-7d; a product the play already names stays the play's) → ③ last visit (empty until SB-3c) →
        //     ④ portfolio (no data: never filled automatically).
        var lines = bindings?.ProductLines.Where(l => l.GlobalProductId != Guid.Empty).ToList()
                    ?? new List<StrategyTemplateProductMixLine>();
        var playProducts = lines.Select(l => l.GlobalProductId).ToHashSet();
        var picks = (request.RepProducts ?? Array.Empty<VisitContentProductPick>())
            .Where(p => p.ProductId != Guid.Empty && !playProducts.Contains(p.ProductId))
            .GroupBy(p => p.ProductId)
            .Select(g => g.First())
            .ToList();

        var capacity = await LoadCapacityAsync(request, cancellationToken);
        if (lines.Count == 0 && picks.Count == 0)
        {
            // K-7a — no product at all: planned with the report time only, said with no_products.
            var noProductReasons = new List<string>
            {
                bindings is null ? VisitContentSequenceReasonCodes.StrategyNotFound : VisitContentSequenceReasonCodes.ContentSplitUnresolved,
                VisitContentSequenceReasonCodes.NoProducts
            };
            return VisitContentSequenceResult.NotResolved(
                    bindings is null ? VisitContentSequenceStatus.NoStrategy : VisitContentSequenceStatus.NoJourney,
                    noProductReasons, at, strategyTemplateId: bindings?.TemplateId)
                with { VisitDurationMinutes = ReportOnlyDuration(capacity, noProductReasons) };
        }

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

        // K-7f — the journeys that tell each picked product (one read set for all of them).
        var productJourneys = picks.Count == 0
            ? new Dictionary<Guid, IReadOnlyList<ContentEngagementJourneyDto>>()
            : await _journeys.ResolvePublishedJourneysForProductsAsync(
                picks.Select(p => p.ProductId).ToList(), new ContentEngagementJourneyCriteria(EffectiveAt: at), cancellationToken);

        // 3 ─ Per role: the play's lines in weighted-rotation order (a line that cannot resolve drops, the next takes its
        //     place) then the rep's picks in their rotated order (K-7e) with the previous visit's overflow first (S-2);
        //     the role's limit takes the head, the rest is this visit's overflow.
        var reasons = new List<string>();
        var items = new List<VisitContentItem>();
        var overflow = new List<VisitContentOverflow>();
        var carry = request.CarryOverProductIds ?? Array.Empty<Guid>();
        foreach (var role in new[] { StrategyProductLineRoles.Promo, StrategyProductLineRoles.NonPromo })
        {
            var max = role == StrategyProductLineRoles.Promo
                ? capacity?.EffectiveMaxPromoProducts() ?? CycleCapacityLimits.DefaultMaxProductsPerVisit
                : capacity?.EffectiveMaxNonPromoProducts() ?? CycleCapacityLimits.DefaultMaxProductsPerVisit;

            var ordered = new List<(Func<Task<VisitContentItem?>> Build, Guid ProductId, string? Code, string Source)>();
            var playCandidates = lines
                .Where(l => RoleOf(l.Role) == role)
                .Select(l => new VisitContentRotationCandidate<StrategyTemplateProductMixLine>(
                    l, l.LineWeightPercentage, l.SortOrder, l.LineId, context.ExposureOf(l)))
                .ToList();
            foreach (var candidate in VisitContentRotation.Order(playCandidates))
            {
                var line = candidate.Line;
                ordered.Add((() => BuildPlayItemAsync(line, role, context, reasons, cancellationToken),
                    line.GlobalProductId, line.GlobalProductCodeDisplay, PlannedVisitContentItemSources.Play));
            }

            foreach (var pick in RotatedPicks(picks.Where(p => RoleOf(p.Role) == role).ToList(), request.VisitOrdinal, carry))
            {
                ordered.Add((async () => await BuildPickedItemAsync(pick, role, productJourneys, context, reasons, cancellationToken),
                    pick.ProductId, pick.ProductCode, PlannedVisitContentItemSources.RepPick));
            }

            var taken = 0;
            foreach (var (build, productId, code, source) in ordered)
            {
                if (taken >= max)
                {
                    // Only a candidate that would have been told is overflow (a play line that cannot resolve is dropped).
                    if (source == PlannedVisitContentItemSources.RepPick || await build() is not null)
                    {
                        overflow.Add(new VisitContentOverflow(productId, code, role, source,
                            role == StrategyProductLineRoles.Promo
                                ? VisitContentSequenceReasonCodes.MaxPromo
                                : VisitContentSequenceReasonCodes.MaxNonPromo));
                    }

                    continue;
                }

                var item = await build();
                if (item is null)
                {
                    continue;
                }

                items.Add(item);
                taken++;
            }
        }

        // K-7e — the final order (promo first, then non-promo) is carried on every item.
        items = items.Select((item, index) => item with { Order = index + 1 }).ToList();

        if (items.Count == 0)
        {
            reasons.Add(VisitContentSequenceReasonCodes.NoProducts);
            return VisitContentSequenceResult.NotResolved(
                    VisitContentSequenceStatus.NoJourney, reasons.Distinct(StringComparer.Ordinal).ToList(), at,
                    strategyTemplateId: bindings?.TemplateId)
                with { VisitDurationMinutes = ReportOnlyDuration(capacity, reasons), OverflowProducts = overflow };
        }

        // 4 ─ Duration = FU06B calculator over the PRODUCT counts of the list (K-7). Step minutes never count (E7-B2).
        var promoCount = items.Count(i => i.Role == StrategyProductLineRoles.Promo);
        var nonPromoCount = items.Count - promoCount;
        var duration = ComputeDuration(capacity, promoCount, nonPromoCount, reasons);

        // Backward compatibility: the top-level journey / stage is the first promo item WITH content (else any with it).
        var lead = items.FirstOrDefault(i => i.HasContent && i.Role == StrategyProductLineRoles.Promo)
                   ?? items.FirstOrDefault(i => i.HasContent);
        return new VisitContentSequenceResult(
            VisitContentSequenceStatus.Resolved,
            lead?.JourneyId,
            lead?.StageId,
            lead?.StageIndex,
            lead?.StageCode,
            lead?.StageName,
            items.Any(i => i.Source == PlannedVisitContentItemSources.Play)
                ? PlannedVisitContentSource.Strategy
                : PlannedVisitContentSource.Manual,
            bindings?.TemplateId,
            promoCount,
            nonPromoCount,
            duration,
            reasons.Distinct(StringComparer.Ordinal).ToList(),
            at,
            items,
            overflow);
    }

    /// <summary>
    /// WP-VP-3C (K-7e) — the rep's picks of one role for the doctor's <paramref name="visitOrdinal"/>-th visit in the
    /// period: the SAME set, shifted by (n − 1) (A, B, C → B, C, A → C, A, B); the previous visit's overflow then moves to
    /// the front, in its own order (S-2). Pure.
    /// </summary>
    public static IReadOnlyList<VisitContentProductPick> RotatedPicks(
        IReadOnlyList<VisitContentProductPick> picks, int visitOrdinal, IReadOnlyCollection<Guid> carryOver)
    {
        if (picks.Count == 0)
        {
            return picks;
        }

        var shift = ((Math.Max(1, visitOrdinal) - 1) % picks.Count + picks.Count) % picks.Count;
        var rotated = picks.Skip(shift).Concat(picks.Take(shift)).ToList();
        if (carryOver.Count == 0)
        {
            return rotated;
        }

        var carried = carryOver.Distinct().Select(id => rotated.FirstOrDefault(p => p.ProductId == id)).OfType<VisitContentProductPick>().ToList();
        return carried.Concat(rotated.Where(p => !carried.Contains(p))).ToList();
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

    /// <summary>One play line → its item, or null (dropped, the reason coded). Never throws on missing data.</summary>
    private async Task<VisitContentItem?> BuildPlayItemAsync(
        StrategyTemplateProductMixLine line, string role, ResolutionContext context, List<string> reasons,
        CancellationToken cancellationToken)
    {
        // A line written before SB-3a has no journey: nothing to tell for that product.
        if (line.JourneyId is not { } journeyId || journeyId == Guid.Empty)
        {
            reasons.Add(VisitContentSequenceReasonCodes.ProductHasNoJourney);
            return null;
        }

        return await BuildContentAsync(
            line.GlobalProductId, line.GlobalProductCodeDisplay, role, journeyId, PlannedVisitContentItemSources.Play,
            context, reasons, cancellationToken);
    }

    /// <summary>
    /// WP-VP-3C (K-7f) — one picked product → its item. The product's journey is the ONE published journey telling it
    /// (<see cref="IContentEngagementJourneyReader.ResolvePublishedJourneysForProductsAsync"/>, narrowed to the journeys
    /// whose audience does not exclude the doctor): none → planned without content (<c>no_approved_content</c>), several
    /// → planned without content (<c>ambiguous_journey</c>), one → the same stage / path rule as a play item (a stage that
    /// does not resolve also leaves the product without content). A picked product is never dropped.
    /// </summary>
    private async Task<VisitContentItem> BuildPickedItemAsync(
        VisitContentProductPick pick, string role,
        IReadOnlyDictionary<Guid, IReadOnlyList<ContentEngagementJourneyDto>> productJourneys,
        ResolutionContext context, List<string> reasons, CancellationToken cancellationToken)
    {
        var journeys = new List<ContentEngagementJourneyDto>();
        foreach (var journey in productJourneys.TryGetValue(pick.ProductId, out var rows) ? rows : Array.Empty<ContentEngagementJourneyDto>())
        {
            if (journey.AudienceProfileId is { } profileId && profileId != Guid.Empty
                && VisitContentAudiencePolicy.Covers(
                    await context.AudienceAsync(profileId, _sources, cancellationToken),
                    await context.SpecialtyAsync(_sources, cancellationToken)) == false)
            {
                continue;
            }

            journeys.Add(journey);
        }

        if (journeys.Count != 1)
        {
            var warning = journeys.Count == 0
                ? VisitContentSequenceReasonCodes.NoApprovedContent
                : VisitContentSequenceReasonCodes.AmbiguousJourney;
            reasons.Add(warning);
            return VisitContentItem.WithoutContent(pick.ProductId, pick.ProductCode, role, PlannedVisitContentItemSources.RepPick, warning);
        }

        var item = await BuildContentAsync(
            pick.ProductId, pick.ProductCode, role, journeys[0].JourneyId, PlannedVisitContentItemSources.RepPick,
            context, reasons, cancellationToken);
        if (item is not null)
        {
            return item;
        }

        reasons.Add(VisitContentSequenceReasonCodes.NoApprovedContent);
        return VisitContentItem.WithoutContent(
            pick.ProductId, pick.ProductCode, role, PlannedVisitContentItemSources.RepPick, VisitContentSequenceReasonCodes.NoApprovedContent);
    }

    /// <summary>A product on a journey → its item (stage from progress + pending, the stage's released path, the path's
    /// MAIN branch steps), or null with the reason coded. The same rule for play and picked products.</summary>
    private async Task<VisitContentItem?> BuildContentAsync(
        Guid productId, string? productCode, string role, Guid journeyId, string source, ResolutionContext context,
        List<string> reasons, CancellationToken cancellationToken)
    {
        var stages = await context.StagesAsync(journeyId, _journeys, cancellationToken);
        if (!context.Journeys.TryGetValue(journeyId, out var journey) || stages.Count == 0)
        {
            reasons.Add(VisitContentSequenceReasonCodes.JourneyUnpublished);
            return null;
        }

        // The doctor's position on this product's journey (+ the visits planned before this one), wrapping at the end.
        var warnings = new List<string>();
        var stageIndex = 0;
        if (context.ProgressOf(productId, journeyId) is { } progress)
        {
            if (progress.IsStageIndexStale(stages.Count))
            {
                warnings.Add(VisitContentSequenceReasonCodes.StageIndexReset);
                reasons.Add(VisitContentSequenceReasonCodes.StageIndexReset);
            }

            stageIndex = progress.EffectiveStageIndex(stages.Count);
        }

        stageIndex = JourneyProgress.StageAfter(stageIndex, context.PendingOf(productId, journeyId), stages.Count);
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

        // E7-B1 — only the path's MAIN branch is told (VisitContentMainBranch).
        var template = path.ChainTemplate is { } chain
            ? await context.ChainTemplateAsync(chain.ConceptChainTemplateId, _sources, cancellationToken)
            : null;

        return new VisitContentItem(
            productId,
            productCode,
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
            VisitContentMainBranch.Steps(path, template)
                .Select(s => new VisitContentStep(
                    s.StepId, s.ContentId, s.ContentCode, s.StepTitle, s.StepType, s.EstimatedDurationMinutes))
                .ToList(),
            path.Claims.Select(c => new VisitContentClaim(c.ClaimId, c.ClaimCode)).ToList(),
            warnings,
            source);
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

    private static string RoleOf(string? role)
        => string.Equals(role?.Trim(), StrategyProductLineRoles.NonPromo, StringComparison.OrdinalIgnoreCase)
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

    /// <summary>WP-VP-3C (K-7a) — a visit without products: the report time only (the calculator over 0 / 0); 0 (and
    /// <c>capacity_not_found</c>) without a capacity.</summary>
    private static int ReportOnlyDuration(CapacityEntity? capacity, List<string> reasons)
        => ComputeDuration(capacity, 0, 0, reasons);

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

        private readonly Dictionary<Guid, ConceptChainTemplate?> _templates = new();

        public async Task<ConceptChainTemplate?> ChainTemplateAsync(
            Guid templateId, IVisitContentSourceReader sources, CancellationToken cancellationToken)
        {
            if (!_templates.TryGetValue(templateId, out var template))
            {
                template = await sources.GetChainTemplateAsync(templateId, cancellationToken);
                _templates[templateId] = template;
            }

            return template;
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
