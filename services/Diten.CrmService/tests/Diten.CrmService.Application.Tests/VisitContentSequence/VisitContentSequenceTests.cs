using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Knowledge;
using Diten.CrmService.Application.Features.Knowledge.Content;
using Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney;
using Diten.CrmService.Application.Features.Segmentation;
using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Application.Features.VisitContentSequence;
using Diten.CrmService.Application.Features.VisitContentSequence.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.VisitContentSequence.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;
using CapacityEntity = Diten.CrmService.Domain.Entities.CycleCapacity;

namespace Diten.CrmService.Application.Tests.VisitContentSequence;

/// <summary>
/// MOD-0155 FU04 — Visit Content Sequence resolver. Pure unit tests over in-memory fakes of the READ seams (no Mongo).
/// Pins down: deterministic next-stage advance incl. the end-of-journey FLAG (D-END-OF-JOURNEY), the promo / non-promo
/// split from StrategyTemplate ProductLines (promo-hit, non-promo, content_split_unresolved fail-closed → ReportDuration
/// only, D-CONTENT-SPLIT), the FU06B duration delegation, capacity_not_found, the membership gate, no-persistence, and
/// the preview handler (200 resolved / 400 malformed / resolver parity).
/// </summary>
public sealed class VisitContentSequenceTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset Past = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 0, 0, 0, TimeSpan.Zero);
    private static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");

    // ── AC-SEQ-1 / AC-SEQ-2 ──────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task First_visit_starts_at_stage_index_zero()
    {
        var env = Env.WithThreeStageJourney();
        var result = await env.Resolver.ResolveAsync(env.Request(priorStageIndex: null), default);

        Assert.Equal(VisitContentSequenceStatus.Resolved, result.Status);
        Assert.Equal(0, result.StageIndex);
        Assert.Equal(env.StageId(0), result.StageId);
        Assert.Equal("strategy", result.ContentSource);
    }

    [Fact]
    public async Task Prior_index_advances_by_one()
    {
        var env = Env.WithThreeStageJourney();
        var result = await env.Resolver.ResolveAsync(env.Request(priorStageIndex: 0), default);

        Assert.Equal(VisitContentSequenceStatus.Resolved, result.Status);
        Assert.Equal(1, result.StageIndex);
        Assert.Equal(env.StageId(1), result.StageId);
    }

    [Fact]
    public async Task Auto_advance_is_deterministic()
    {
        var env = Env.WithThreeStageJourney();
        var a = await env.Resolver.ResolveAsync(env.Request(priorStageIndex: 1), default);
        var b = await env.Resolver.ResolveAsync(env.Request(priorStageIndex: 1), default);

        Assert.Equal(a.StageId, b.StageId);
        Assert.Equal(a.StageIndex, b.StageIndex);
        Assert.Equal(a.VisitDurationMinutes, b.VisitDurationMinutes);
    }

    // ── AC-SEQ-4 / D-END-OF-JOURNEY = flag ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Past_last_stage_flags_end_of_journey_without_wrap()
    {
        var env = Env.WithThreeStageJourney();
        var result = await env.Resolver.ResolveAsync(env.Request(priorStageIndex: 2), default);

        Assert.Equal(VisitContentSequenceStatus.EndOfJourney, result.Status);
        Assert.Null(result.StageId);
        Assert.Contains(VisitContentSequenceReasonCodes.JourneyCompleted, result.ReasonCodes);
    }

    // ── AC-SEQ-3 ─────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task No_strategy_is_coded_not_invented()
    {
        var env = Env.WithThreeStageJourney();
        env.Strategies.Bindings.Clear();
        var result = await env.Resolver.ResolveAsync(
            env.Request(priorStageIndex: null, strategyTemplateId: Id(999)), default);

        Assert.Equal(VisitContentSequenceStatus.NoStrategy, result.Status);
        Assert.Contains(VisitContentSequenceReasonCodes.StrategyNotFound, result.ReasonCodes);
        Assert.Null(result.StageId);
    }

    [Fact]
    public async Task Unpublished_journey_is_coded_no_journey()
    {
        var env = Env.WithThreeStageJourney();
        env.Journeys.Published.Clear(); // strategy binds a journey id that no longer resolves as published
        var result = await env.Resolver.ResolveAsync(env.Request(priorStageIndex: null), default);

        Assert.Equal(VisitContentSequenceStatus.NoJourney, result.Status);
        Assert.Contains(VisitContentSequenceReasonCodes.JourneyNotPublished, result.ReasonCodes);
    }

    // ── AC-SPLIT-1 ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Content_tied_to_promoted_product_is_promo_rest_is_non_promo()
    {
        var env = Env.WithThreeStageJourney();
        // Two content items on the journey product: one tied to the promoted product, one to another product.
        env.Content.Items.Add(env.ContentItem(productId: Env.PromotedProduct));
        env.Content.Items.Add(env.ContentItem(productId: Id(700)));
        env.Content.Items.Add(env.ContentItem(productId: null)); // untied → non-promo

        var result = await env.Resolver.ResolveAsync(env.Request(priorStageIndex: 0), default);

        Assert.Equal(VisitContentSequenceStatus.Resolved, result.Status);
        Assert.Equal(1, result.PromoItemCount);
        Assert.Equal(2, result.NonPromoItemCount);
        Assert.DoesNotContain(VisitContentSequenceReasonCodes.ContentSplitUnresolved, result.ReasonCodes);
    }

    // ── AC-SPLIT-2 / AC-DUR-3 — fail-closed ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task No_promoted_products_fails_closed_to_report_duration_only()
    {
        var env = Env.WithThreeStageJourney();
        env.Strategies.Bindings[Env.StrategyId] = env.BindingsWith(productLines: new List<StrategyTemplateProductMixLine>());
        env.Content.Items.Add(env.ContentItem(productId: Env.PromotedProduct));

        var result = await env.Resolver.ResolveAsync(env.Request(priorStageIndex: 0), default);

        Assert.Equal(0, result.PromoItemCount);
        Assert.Equal(0, result.NonPromoItemCount);
        Assert.Contains(VisitContentSequenceReasonCodes.ContentSplitUnresolved, result.ReasonCodes);
        // capacity Report=3, promo/nonPromo 0 → duration == ReportDuration only.
        Assert.Equal(3, result.VisitDurationMinutes);
    }

    // ── AC-DUR-1 ─────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Duration_comes_from_the_fu06b_calculator()
    {
        var env = Env.WithThreeStageJourney(); // capacity Promo=5, NonPromo=3, Report=3
        env.Content.Items.Add(env.ContentItem(productId: Env.PromotedProduct));
        env.Content.Items.Add(env.ContentItem(productId: Env.PromotedProduct));
        env.Content.Items.Add(env.ContentItem(productId: Id(700))); // non-promo

        var result = await env.Resolver.ResolveAsync(env.Request(priorStageIndex: 0), default);

        Assert.Equal(2, result.PromoItemCount);
        Assert.Equal(1, result.NonPromoItemCount);
        Assert.Equal(2 * 5 + 1 * 3 + 3, result.VisitDurationMinutes); // 16
    }

    // ── V5 capacity_not_found ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Missing_capacity_is_coded_and_duration_is_zero()
    {
        var env = Env.WithThreeStageJourney();
        env.Capacities.Rows.Clear();
        var result = await env.Resolver.ResolveAsync(env.Request(priorStageIndex: 0), default);

        Assert.Contains(VisitContentSequenceReasonCodes.CapacityNotFound, result.ReasonCodes);
        Assert.Equal(0, result.VisitDurationMinutes);
    }

    // ── membership gate ──────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Non_member_of_segment_has_no_play()
    {
        var env = Env.WithThreeStageJourney();
        env.Segments.Member = false;
        var result = await env.Resolver.ResolveAsync(
            env.Request(priorStageIndex: null, useSegment: true), default);

        Assert.Equal(VisitContentSequenceStatus.NoStrategy, result.Status);
        Assert.Contains(VisitContentSequenceReasonCodes.StrategyNotFound, result.ReasonCodes);
    }

    [Fact]
    public async Task Member_of_segment_resolves_the_segments_play()
    {
        var env = Env.WithThreeStageJourney();
        env.Segments.Member = true;
        var result = await env.Resolver.ResolveAsync(
            env.Request(priorStageIndex: 0, useSegment: true), default);

        Assert.Equal(VisitContentSequenceStatus.Resolved, result.Status);
        Assert.Equal(Env.StrategyId, result.StrategyTemplateId);
    }

    // ── AC-BND-1 no persistence ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Resolver_persists_nothing()
    {
        var env = Env.WithThreeStageJourney();
        await env.Resolver.ResolveAsync(env.Request(priorStageIndex: 0), default);

        Assert.Equal(0, env.Capacities.InsertCalls);
        Assert.Equal(0, env.Capacities.ReplaceCalls);
    }

    // ── AC-EP-1 / AC-EP-2 preview handler ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Preview_handler_returns_200_with_resolver_parity()
    {
        var env = Env.WithThreeStageJourney();
        var handler = new PreviewVisitContentHandler(env.Resolver);
        var request = env.Request(priorStageIndex: 0);

        var response = await handler.Handle(new PreviewVisitContentQuery(request), default);
        var direct = await env.Resolver.ResolveAsync(request, default);

        Assert.True(response.IsSuccessful);
        Assert.Equal(200, response.StatusCode);
        Assert.NotNull(response.Data);
        Assert.Equal(direct.StageId, response.Data!.StageId);
        Assert.Equal(direct.VisitDurationMinutes, response.Data.VisitDurationMinutes);
        Assert.Equal(0, env.Capacities.InsertCalls); // the endpoint could not write even by mistake
    }

    [Fact]
    public async Task Preview_handler_rejects_malformed_request_with_400()
    {
        var env = Env.WithThreeStageJourney();
        var handler = new PreviewVisitContentHandler(env.Resolver);
        var bad = new VisitContentSequenceRequest(
            SubjectType: "", SubjectId: Guid.Empty, SegmentId: null, StrategyTemplateId: null,
            CyclePeriodId: null, PriorStageIndex: null, EffectiveAt: Now);

        var response = await handler.Handle(new PreviewVisitContentQuery(bad), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
    // Test environment + in-memory fakes
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    private sealed class Env
    {
        public static readonly Guid StrategyId = Id(1);
        public static readonly Guid SegmentId = Id(2);
        public static readonly Guid JourneyId = Id(3);
        public static readonly Guid CyclePeriodId = Id(4);
        public static readonly Guid DoctorId = Id(5);
        public static readonly Guid SubjectRefId = Id(6);
        public static readonly Guid PromotedProduct = Id(100);

        public FakeStrategyReader Strategies { get; } = new();
        public FakeSegmentReader Segments { get; } = new();
        public FakeJourneyReader Journeys { get; } = new();
        public FakeContentReader Content { get; } = new();
        public FakeCapacityRepository Capacities { get; } = new();
        public VisitContentSequenceResolver Resolver { get; }

        private readonly List<ContentEngagementJourneyStageDto> _stages = new();

        private Env()
        {
            var tenant = new TenantContext();
            tenant.SetTenant(Tenant);
            Resolver = new VisitContentSequenceResolver(
                tenant, Strategies, Segments, Journeys, Content, Capacities);
        }

        public Guid StageId(int index) => _stages[index].StageId;

        public static Env WithThreeStageJourney()
        {
            var env = new Env();
            for (var i = 0; i < 3; i++)
            {
                env._stages.Add(Stage(Id(10 + i), i, $"stage-{i}"));
            }

            env.Strategies.Bindings[StrategyId] = env.BindingsWith(DefaultProductLines());
            env.Strategies.SegmentSummaries[SegmentId] = new List<StrategyTemplateSummary>
            {
                new(StrategyId, "play-a", "Play A", "active", 1, Past, null)
            };
            env.Journeys.Published.Add(Journey(JourneyId));
            env.Journeys.Stages[JourneyId] = env._stages;
            env.Capacities.Rows.Add(Capacity(CyclePeriodId, promo: 5, nonPromo: 3, report: 3));
            return env;
        }

        public VisitContentSequenceRequest Request(
            int? priorStageIndex, Guid? strategyTemplateId = null, bool useSegment = false)
            => new(
                SubjectType: "contact",
                SubjectId: DoctorId,
                SegmentId: useSegment ? SegmentId : null,
                StrategyTemplateId: useSegment ? null : (strategyTemplateId ?? StrategyId),
                CyclePeriodId: CyclePeriodId,
                PriorStageIndex: priorStageIndex,
                EffectiveAt: Now);

        public StrategyTemplateBindingSet BindingsWith(IReadOnlyList<StrategyTemplateProductMixLine> productLines)
            => new(
                StrategyId, "play-a", "Play A", "contact", 1, StrategyId, Past, null,
                new List<Guid> { SegmentId },
                new StrategyTemplateFrequencyIntentSnapshot("none", null, null, null, null, false),
                productLines,
                new List<StrategyTemplateContentReference>
                {
                    new("content-engagement-journey", JourneyId, 0)
                });

        private static IReadOnlyList<StrategyTemplateProductMixLine> DefaultProductLines()
            => new List<StrategyTemplateProductMixLine>
            {
                new(Id(50), PromotedProduct, 100m, "product-only",
                    new List<StrategyTemplateSkuShare>(), 100m, false)
            };

        public KnowledgeContentDto ContentItem(Guid? productId)
            => new(
                Guid.NewGuid(), "c", "Content", "detail", "published",
                SubjectRefId, null, null, null, null, productId, null, null, "en",
                null, null, null, null, null, "1.0", Past, null, "manual",
                Array.Empty<string>(), Array.Empty<KnowledgeExternalReferenceDto>(),
                Past, null, null, null, null, null, false);

        private static ContentEngagementJourneyDto Journey(Guid id)
            => new(
                id, "adoption", "Adoption", null, SubjectRefId, null, null, "Drive adoption", "en",
                "1.0", "published", Past, null, "manual",
                new List<ContentEngagementJourneyStageDto>(), 3, 0, 0,
                false, false, false, false, false, null, Past, null, null,
                1, Past, null, null, null, null, null, false);

        private static ContentEngagementJourneyStageDto Stage(Guid id, int order, string code)
            => new(
                id, order, code, $"Stage {order}", "objective", "detail",
                Id(200 + order), "path", "pinned", true, false, null, null, null, null, null,
                new List<ContentEngagementJourneyBranchConditionDto>(), "active",
                Id(200 + order), "1.0", "Path", 1, "pinned", false, false, 1,
                null, null, Past, null, null, null, false);

        private static CapacityEntity Capacity(Guid cyclePeriodId, int promo, int nonPromo, int report)
            => new()
            {
                Id = Guid.NewGuid(),
                TenantId = Tenant,
                CyclePeriodId = cyclePeriodId,
                PromoProductTime = promo,
                NonPromoProductTime = nonPromo,
                ReportDuration = report,
                DailyWorkMinutes = 480
            };
    }

    private sealed class FakeStrategyReader : IStrategyTemplateReader
    {
        public Dictionary<Guid, StrategyTemplateBindingSet> Bindings { get; } = new();
        public Dictionary<Guid, IReadOnlyList<StrategyTemplateSummary>> SegmentSummaries { get; } = new();

        public Task<StrategyTemplateBindingSet?> GetActiveBindingsAsync(
            Guid templateId, DateTimeOffset effectiveAt, CancellationToken cancellationToken)
            => Task.FromResult(Bindings.TryGetValue(templateId, out var set) ? set : null);

        public Task<IReadOnlyList<StrategyTemplateSummary>> ListBySegmentAsync(
            Guid segmentId, DateTimeOffset effectiveAt, CancellationToken cancellationToken)
            => Task.FromResult(SegmentSummaries.TryGetValue(segmentId, out var rows)
                ? rows
                : (IReadOnlyList<StrategyTemplateSummary>)Array.Empty<StrategyTemplateSummary>());
    }

    private sealed class FakeSegmentReader : ISegmentMembershipReader
    {
        public bool Member { get; set; } = true;

        public Task<SegmentMembershipVerdict> IsMemberAsync(
            Guid segmentId, string subjectType, Guid subjectId, DateTimeOffset effectiveAt,
            CancellationToken cancellationToken)
            => Task.FromResult(new SegmentMembershipVerdict(
                segmentId, 1, subjectType, subjectId,
                Member ? SegmentMembershipVerdicts.Member : SegmentMembershipVerdicts.NotMember,
                Array.Empty<string>(), effectiveAt));

        public Task<SegmentResolutionResult> ResolveAsync(
            Guid segmentId, DateTimeOffset effectiveAt, int limit, int offset, CancellationToken cancellationToken)
            => Task.FromResult(new SegmentResolutionResult(
                segmentId, 1, "contact", false, effectiveAt, 0, 0, Array.Empty<SegmentMemberDto>()));
    }

    private sealed class FakeJourneyReader : IContentEngagementJourneyReader
    {
        public List<ContentEngagementJourneyDto> Published { get; } = new();
        public Dictionary<Guid, IReadOnlyList<ContentEngagementJourneyStageDto>> Stages { get; } = new();

        public Task<IReadOnlyList<ContentEngagementJourneyDto>> ResolvePublishedJourneysAsync(
            ContentEngagementJourneyCriteria criteria, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ContentEngagementJourneyDto>>(Published.ToList());

        public Task<IReadOnlyList<ContentEngagementJourneyStageDto>> GetOrderedStagesAsync(
            Guid journeyId, DateTimeOffset effectiveAt, CancellationToken cancellationToken)
            => Task.FromResult(Stages.TryGetValue(journeyId, out var rows)
                ? rows
                : (IReadOnlyList<ContentEngagementJourneyStageDto>)Array.Empty<ContentEngagementJourneyStageDto>());
    }

    private sealed class FakeContentReader : IKnowledgeContentLinkageReader
    {
        public List<KnowledgeContentDto> Items { get; } = new();

        public Task<IReadOnlyList<KnowledgeContentDto>> ResolvePublishedContentAsync(
            KnowledgeContentLinkageCriteria criteria, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<KnowledgeContentDto>>(Items.ToList());
    }

    private sealed class FakeCapacityRepository : ICycleCapacityRepository
    {
        public List<CapacityEntity> Rows { get; } = new();
        public int InsertCalls { get; private set; }
        public int ReplaceCalls { get; private set; }

        public Task<CapacityEntity?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
            => Task.FromResult(Rows.FirstOrDefault(c => c.TenantId == tenantId && c.Id == id));

        public Task<CapacityEntity?> GetByCyclePeriodAsync(
            Guid tenantId, Guid cyclePeriodId, CancellationToken cancellationToken)
            => Task.FromResult(Rows.FirstOrDefault(c => c.TenantId == tenantId && c.CyclePeriodId == cyclePeriodId));

        public Task<IReadOnlyList<CapacityEntity>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<CapacityEntity>>(Rows.Where(c => c.TenantId == tenantId).ToList());

        public Task InsertAsync(CapacityEntity entity, CancellationToken cancellationToken)
        {
            InsertCalls++;
            return Task.CompletedTask;
        }

        public Task<bool> ReplaceAsync(CapacityEntity entity, int expectedVersion, CancellationToken cancellationToken)
        {
            ReplaceCalls++;
            return Task.FromResult(true);
        }
    }
}
