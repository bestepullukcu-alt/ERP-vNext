using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney;
using Diten.CrmService.Application.Features.Segmentation;
using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Application.Features.VisitContentSequence;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using CapacityEntity = Diten.CrmService.Domain.Entities.CycleCapacity;

namespace Diten.CrmService.Application.Tests.VisitContentSequence;

/// <summary>
/// WP-SB-3b — an in-memory world for the visit content resolver v2 (no Mongo): a play whose product lines each tell a
/// product on its own published journey, every stage bound to its own released knowledge path (country TR, language tr,
/// two active steps + one archived, one claim), the doctor's journey progress and a cycle capacity. Shared by the
/// resolver tests and the planning engine tests so both read the same world.
/// </summary>
internal sealed class VisitContentTestKit
{
    public static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly DateTimeOffset Past = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset Now = new(2026, 8, 29, 0, 0, 0, TimeSpan.Zero);

    public Guid StrategyId { get; } = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public Guid SegmentId { get; } = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public Guid CyclePeriodId { get; } = Guid.Parse("00000000-0000-0000-0000-000000000004");
    public Guid DoctorId { get; } = Guid.Parse("00000000-0000-0000-0000-000000000005");

    public KitStrategyReader Strategies { get; }
    public KitSegmentReader Segments { get; } = new();
    public KitJourneyReader Journeys { get; } = new();
    public KitCapacityRepository Capacities { get; } = new();
    public KitSourceReader Sources { get; } = new();
    public TenantContext TenantContext { get; } = new();

    /// <summary>The play's product lines (author order = insertion order unless a sort order is given).</summary>
    public List<StrategyTemplateProductMixLine> Lines { get; } = new();

    /// <summary>The play's template-level content bindings (retired by S3-2: the resolver must not read them).</summary>
    public List<StrategyTemplateContentReference> TemplateBindings { get; } = new();

    public VisitContentSequenceResolver Resolver { get; }

    public VisitContentTestKit()
    {
        TenantContext.SetTenant(Tenant);
        Strategies = new KitStrategyReader(this);
        Capacities.Rows.Add(new CapacityEntity
        {
            Id = Guid.NewGuid(), TenantId = Tenant, CyclePeriodId = CyclePeriodId,
            PromoProductTime = 5, NonPromoProductTime = 3, ReportDuration = 3, DailyWorkMinutes = 480
        });
        Resolver = CreateResolver(TenantContext, Segments, Capacities);
    }

    public CapacityEntity Capacity => Capacities.Rows[0];

    public VisitContentSequenceResolver CreateResolver(
        ITenantContext tenant, ISegmentMembershipReader segments, ICycleCapacityRepository capacities)
        => new(tenant, Strategies, segments, Journeys, capacities, Sources);

    public StrategyTemplateBindingSet Bindings()
        => new(
            StrategyId, "play-a", "Play A", "contact", 1, StrategyId, Past, null,
            new List<Guid> { SegmentId },
            new StrategyTemplateFrequencyIntentSnapshot("none", null, null, null, null, false),
            Lines.ToList(),
            TemplateBindings.ToList());

    /// <summary>A product line + its published journey of <paramref name="stageCount"/> stages, each stage on its own
    /// released path. <paramref name="withJourney"/> = false is a line written before SB-3a.</summary>
    public Product AddProduct(
        string code,
        string role = StrategyProductLineRoles.Promo,
        decimal? weight = null,
        int? sortOrder = null,
        int stageCount = 3,
        string pin = ContentEngagementJourneyPathPin.LatestPublished,
        bool withJourney = true,
        Guid? audienceProfileId = null,
        bool publishJourney = true)
    {
        var productId = Guid.NewGuid();
        var journeyId = Guid.NewGuid();
        var stages = new List<ContentEngagementJourneyStageDto>();
        var paths = new List<KnowledgePath>();
        for (var i = 0; i < stageCount; i++)
        {
            var path = AddPath($"{code}-P{i}", "1.0");
            paths.Add(path);
            stages.Add(Stage(Guid.NewGuid(), (i + 1) * 10, $"{code}-S{i}", path, pin));
        }

        var journey = Journey(journeyId, $"{code}-J", audienceProfileId, stages);
        if (publishJourney)
        {
            Journeys.Published.Add(journey);
            Journeys.Stages[journeyId] = stages;
        }

        Lines.Add(new StrategyTemplateProductMixLine(
            Guid.NewGuid(), productId, weight, "product-only", new List<StrategyTemplateSkuShare>(), 100m, false,
            role, withJourney ? journeyId : null, sortOrder ?? (Lines.Count + 1) * 10, code));

        return new Product(productId, journeyId, code, stages, paths);
    }

    public KnowledgePath AddPath(
        string pathCode, string version, string country = "TR", string language = "tr",
        string status = KnowledgePathStatuses.Published, DateTimeOffset? publishedAt = null)
    {
        var path = new KnowledgePath
        {
            Id = Guid.NewGuid(), TenantId = Tenant, PathCode = pathCode, PathName = pathCode, SubjectId = Guid.NewGuid(),
            Objective = "tell", LanguageCode = language, CountryCode = country, PathVersion = version, PathStatus = status,
            EffectiveFrom = Past, PublishedAt = publishedAt ?? Past,
            Steps =
            {
                Step(20, "second", 4),
                Step(10, "first", 3),
                new KnowledgePathStep
                {
                    StepId = Guid.NewGuid(), StepOrder = 5, StepCode = "gone", StepTitle = "Archived", StepType = "detail",
                    ContentId = Guid.NewGuid(), ContentCode = "C-GONE", ArchivedAt = Past
                }
            },
            Claims = { new KnowledgePathClaim { ClaimId = Guid.NewGuid(), ClaimCode = $"CL-{pathCode}" } }
        };
        Sources.Paths.Add(path);
        return path;
    }

    public JourneyProgress SetProgress(Product product, int currentStageIndex, int exposure, int cycle = 0)
    {
        var row = new JourneyProgress
        {
            TenantId = Tenant, ContactId = DoctorId, ProductId = product.ProductId, JourneyId = product.JourneyId,
            CurrentStageIndex = currentStageIndex, ExposureCount = exposure, Cycle = cycle
        };
        Sources.Progress.Add(row);
        return row;
    }

    public VisitContentSequenceRequest Request(
        IReadOnlyList<VisitContentPendingExposure>? pending = null, bool useSegment = false, Guid? strategyTemplateId = null)
        => new(
            SubjectType: "contact",
            SubjectId: DoctorId,
            SegmentId: useSegment ? SegmentId : null,
            StrategyTemplateId: useSegment ? null : (strategyTemplateId ?? StrategyId),
            CyclePeriodId: CyclePeriodId,
            PriorStageIndex: null,
            EffectiveAt: Now,
            PendingExposures: pending);

    public Task<VisitContentSequenceResult> ResolveAsync(IReadOnlyList<VisitContentPendingExposure>? pending = null)
        => Resolver.ResolveAsync(Request(pending), default);

    public sealed record Product(
        Guid ProductId, Guid JourneyId, string Code, IReadOnlyList<ContentEngagementJourneyStageDto> Stages,
        IReadOnlyList<KnowledgePath> Paths);

    private static KnowledgePathStep Step(int order, string code, int minutes) => new()
    {
        StepId = Guid.NewGuid(), StepOrder = order, StepCode = code, StepTitle = $"Step {code}", StepType = "detail",
        ContentId = Guid.NewGuid(), ContentCode = $"C-{code}", EstimatedDurationMinutes = minutes
    };

    public static ContentEngagementJourneyDto Journey(
        Guid id, string code, Guid? audienceProfileId, IReadOnlyList<ContentEngagementJourneyStageDto> stages)
        => new(
            id, code, code, null, Guid.NewGuid(), null, audienceProfileId, "Tell the product", "tr",
            "1.0", "published", Past, null, "manual",
            stages, stages.Count, 0, 0,
            false, false, false, false, false, null, Past, null, null,
            1, Past, null, null, null, null, null, false);

    public static ContentEngagementJourneyStageDto Stage(
        Guid id, int order, string code, KnowledgePath path, string pin)
        => new(
            id, order, code, $"Stage {code}", "objective", "detail",
            path.Id, path.PathCode, pin, true, false, null, null, null, null, null,
            new List<ContentEngagementJourneyBranchConditionDto>(), "active",
            path.Id, path.PathVersion, path.PathName, 2, pin, false, false, 1,
            null, null, Past, null, null, null, false);
}

internal sealed class KitStrategyReader : IStrategyTemplateReader
{
    private readonly VisitContentTestKit _kit;

    public KitStrategyReader(VisitContentTestKit kit) => _kit = kit;

    public bool Missing { get; set; }

    public Task<StrategyTemplateBindingSet?> GetActiveBindingsAsync(
        Guid templateId, DateTimeOffset effectiveAt, CancellationToken cancellationToken)
        => Task.FromResult(!Missing && templateId == _kit.StrategyId ? _kit.Bindings() : null);

    public Task<IReadOnlyList<StrategyTemplateSummary>> ListBySegmentAsync(
        Guid segmentId, DateTimeOffset effectiveAt, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<StrategyTemplateSummary>>(segmentId == _kit.SegmentId
            ? new List<StrategyTemplateSummary>
            {
                new(_kit.StrategyId, "play-a", "Play A", "active", 1, VisitContentTestKit.Past, null)
            }
            : Array.Empty<StrategyTemplateSummary>());
}

internal sealed class KitSegmentReader : ISegmentMembershipReader
{
    public bool Member { get; set; } = true;

    public Task<SegmentMembershipVerdict> IsMemberAsync(
        Guid segmentId, string subjectType, Guid subjectId, DateTimeOffset effectiveAt, CancellationToken cancellationToken)
        => Task.FromResult(new SegmentMembershipVerdict(
            segmentId, 1, subjectType, subjectId,
            Member ? SegmentMembershipVerdicts.Member : SegmentMembershipVerdicts.NotMember,
            Array.Empty<string>(), effectiveAt));

    public Task<SegmentResolutionResult> ResolveAsync(
        Guid segmentId, DateTimeOffset effectiveAt, int limit, int offset, CancellationToken cancellationToken)
        => Task.FromResult(new SegmentResolutionResult(
            segmentId, 1, "contact", false, effectiveAt, 0, 0, Array.Empty<SegmentMemberDto>()));
}

internal sealed class KitJourneyReader : IContentEngagementJourneyReader
{
    public List<ContentEngagementJourneyDto> Published { get; } = new();
    public Dictionary<Guid, IReadOnlyList<ContentEngagementJourneyStageDto>> Stages { get; } = new();

    public Task<IReadOnlyList<ContentEngagementJourneyDto>> ResolvePublishedJourneysAsync(
        ContentEngagementJourneyCriteria criteria, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ContentEngagementJourneyDto>>(Published.ToList());

    public Task<IReadOnlyList<ContentEngagementJourneyStageDto>> GetOrderedStagesAsync(
        Guid journeyId, DateTimeOffset effectiveAt, CancellationToken cancellationToken)
        => Task.FromResult(Published.Any(j => j.JourneyId == journeyId) && Stages.TryGetValue(journeyId, out var rows)
            ? rows
            : (IReadOnlyList<ContentEngagementJourneyStageDto>)Array.Empty<ContentEngagementJourneyStageDto>());
}

internal sealed class KitCapacityRepository : ICycleCapacityRepository
{
    public List<CapacityEntity> Rows { get; } = new();
    public int InsertCalls { get; private set; }
    public int ReplaceCalls { get; private set; }

    public Task<CapacityEntity?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(Rows.FirstOrDefault(c => c.TenantId == tenantId && c.Id == id));

    public Task<CapacityEntity?> GetByCyclePeriodAsync(Guid tenantId, Guid cyclePeriodId, CancellationToken cancellationToken)
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

/// <summary>The resolver's extra read seam — READ-only by construction (it has no write member at all).</summary>
internal sealed class KitSourceReader : IVisitContentSourceReader
{
    public List<JourneyProgress> Progress { get; } = new();
    public List<KnowledgePath> Paths { get; } = new();
    public Dictionary<Guid, string?> Specialties { get; } = new();
    public Dictionary<Guid, AudienceProfile> Audiences { get; } = new();

    public Task<IReadOnlyList<JourneyProgress>> ListProgressAsync(Guid contactId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<JourneyProgress>>(Progress.Where(p => p.ContactId == contactId).ToList());

    public Task<IReadOnlyList<KnowledgePath>> ListPathsAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<KnowledgePath>>(Paths.ToList());

    public Task<string?> GetContactSpecialtyAsync(Guid contactId, CancellationToken cancellationToken)
        => Task.FromResult(Specialties.TryGetValue(contactId, out var s) ? s : null);

    public Task<AudienceProfile?> GetAudienceProfileAsync(Guid audienceProfileId, CancellationToken cancellationToken)
        => Task.FromResult(Audiences.TryGetValue(audienceProfileId, out var p) ? p : null);
}
