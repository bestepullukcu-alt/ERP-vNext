using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Segmentation.Catalog;
using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Analysis;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Handlers;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Queries;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Resolve;
using Diten.CrmService.Application.Tests.Segmentation;
using Diten.CrmService.Application.Tests.Territory;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;
using Vfp = Diten.CrmService.Domain.Entities.VisitFrequencyPolicy;
using CyclePeriodEntity = Diten.CrmService.Domain.Entities.CyclePeriod;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// WP-FREQ-DET-A — the read-only DETAY ANALİZ. Pins down: the quarter normalisation matrix (month ×3 / week ×13 /
/// quarter ×1 / day ×91), the non-normalisable cycle/campaign-period raw projection, the "uncomputable ⇒ null, never a
/// fabricated 0" rule, per-target-type counting through the REUSED readers (segment membership resolver, campaign-target
/// repo, territory coverage resolver, audience/concept ⇒ uncomputable), the conflict block produced by REUSING the FU03
/// resolve engine, and tenant isolation. It asserts nothing about resolve/CRUD behaviour, which this pack does not touch.
/// </summary>
public sealed class VisitFrequencyPolicyAnalysisTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Jan1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static TenantContext Ctx(Guid id)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id);
        return ctx;
    }

    private static Vfp Policy(
        Guid tenant,
        string targetType,
        Guid targetId,
        string periodType = FrequencyPeriodType.Week,
        int requiredVisitCount = 2,
        int priority = 300,
        string code = "VFP-1",
        string status = FrequencyPolicyStatus.Active,
        Guid? segmentId = null,
        Guid? campaignId = null,
        Guid? territoryNodeId = null,
        DateTimeOffset? from = null)
        => new()
        {
            TenantId = tenant,
            PolicyCode = code,
            PolicyName = "Policy " + code,
            TargetType = targetType,
            TargetId = targetId,
            FrequencyType = FrequencyType.Custom,
            RequiredVisitCount = requiredVisitCount,
            PeriodType = periodType,
            EffectiveFrom = from ?? Jan1,
            Priority = priority,
            Source = FrequencySource.Manual,
            Status = status,
            SegmentId = segmentId,
            CampaignId = campaignId,
            TerritoryNodeId = territoryNodeId
        };

    private static GetVisitFrequencyPolicyAnalysisHandler Handler(
        Guid tenant, FakeVfpRepo repo, IVisitFrequencyTargetImpactCounter counter, FakeCyclePeriodRepo? cyclePeriods = null)
        => new(Ctx(tenant), repo, new VisitFrequencyPolicyResolver(Ctx(tenant), repo), counter,
            cyclePeriods ?? new FakeCyclePeriodRepo());

    // ---------------- Guards ----------------

    [Fact]
    public async Task Analysis_Without_Tenant_Returns_400()
    {
        var handler = new GetVisitFrequencyPolicyAnalysisHandler(
            new TenantContext(), new FakeVfpRepo(),
            new VisitFrequencyPolicyResolver(new TenantContext(), new FakeVfpRepo()),
            new StubCounter(VisitFrequencyTargetImpact.Countable(1)), new FakeCyclePeriodRepo());
        var r = await handler.Handle(new GetVisitFrequencyPolicyAnalysisQuery(Guid.NewGuid()), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Analysis_Unknown_Policy_Returns_404()
    {
        var repo = new FakeVfpRepo();
        var r = await Handler(TenantA, repo, new StubCounter(VisitFrequencyTargetImpact.Countable(1)))
            .Handle(new GetVisitFrequencyPolicyAnalysisQuery(Guid.NewGuid()), default);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task Analysis_CrossTenant_Policy_Returns_404()
    {
        var repo = new FakeVfpRepo();
        var policy = Policy(TenantA, FrequencyTargetType.Account, Guid.NewGuid());
        repo.Items.Add(policy);

        var r = await Handler(TenantB, repo, new StubCounter(VisitFrequencyTargetImpact.Countable(1)))
            .Handle(new GetVisitFrequencyPolicyAnalysisQuery(policy.Id), default);
        Assert.Equal(404, r.StatusCode);
    }

    // ---------------- Projection normalisation ----------------

    [Theory]
    [InlineData(FrequencyPeriodType.Quarter, 1)]
    [InlineData(FrequencyPeriodType.Month, 3)]
    [InlineData(FrequencyPeriodType.Week, 13)]
    [InlineData(FrequencyPeriodType.Day, 91)]
    public async Task Projection_Normalises_Period_To_Quarter(string periodType, int multiplier)
    {
        var repo = new FakeVfpRepo();
        // count 10 × requiredVisitCount 2 × multiplier
        var policy = Policy(TenantA, FrequencyTargetType.Segment, Guid.NewGuid(),
            periodType: periodType, requiredVisitCount: 2, segmentId: Guid.NewGuid());
        repo.Items.Add(policy);

        var r = await Handler(TenantA, repo, new StubCounter(VisitFrequencyTargetImpact.Countable(10)))
            .Handle(new GetVisitFrequencyPolicyAnalysisQuery(policy.Id), default);

        Assert.Equal(200, r.StatusCode);
        Assert.Equal(10, r.Data!.Impact.TargetCount);
        Assert.True(r.Data.Impact.TargetCountComputable);
        Assert.Equal(10 * 2 * multiplier, r.Data.Impact.PlannedVisitsPerQuarter);
        Assert.Null(r.Data.Impact.ProjectionNote);
    }

    [Theory]
    [InlineData(FrequencyPeriodType.Cycle)]
    [InlineData(FrequencyPeriodType.CampaignPeriod)]
    [InlineData(FrequencyPeriodType.Custom)]
    public async Task Projection_NonNormalisable_Period_Is_Raw_Product_With_Note(string periodType)
    {
        var repo = new FakeVfpRepo();
        var policy = Policy(TenantA, FrequencyTargetType.Account, Guid.NewGuid(),
            periodType: periodType, requiredVisitCount: 4);
        repo.Items.Add(policy);

        var r = await Handler(TenantA, repo, new StubCounter(VisitFrequencyTargetImpact.Countable(5)))
            .Handle(new GetVisitFrequencyPolicyAnalysisQuery(policy.Id), default);

        Assert.Equal(5 * 4, r.Data!.Impact.PlannedVisitsPerQuarter); // raw, not scaled
        Assert.NotNull(r.Data.Impact.ProjectionNote);
        Assert.Contains("normalize", r.Data.Impact.ProjectionNote!);
    }

    [Fact]
    public async Task Uncomputable_Count_Yields_Null_Count_And_Null_Projection_With_Note()
    {
        var repo = new FakeVfpRepo();
        var policy = Policy(TenantA, FrequencyTargetType.AudienceProfile, Guid.NewGuid(),
            periodType: FrequencyPeriodType.Week);
        repo.Items.Add(policy);

        var r = await Handler(TenantA, repo,
                new StubCounter(VisitFrequencyTargetImpact.NotCountable("bu hedef tipi için üye sayımı henüz yok")))
            .Handle(new GetVisitFrequencyPolicyAnalysisQuery(policy.Id), default);

        Assert.Null(r.Data!.Impact.TargetCount);
        Assert.False(r.Data.Impact.TargetCountComputable);
        Assert.Null(r.Data.Impact.PlannedVisitsPerQuarter);
        Assert.Equal("bu hedef tipi için üye sayımı henüz yok", r.Data.Impact.ProjectionNote);
    }

    // ---------------- Conflicts (FU03 resolve engine REUSED) ----------------

    [Fact]
    public async Task Conflicts_Lower_Priority_Wins_And_Both_Candidates_Are_Visible()
    {
        var repo = new FakeVfpRepo();
        var target = Guid.NewGuid();
        var hi = Policy(TenantA, FrequencyTargetType.AccountContactLink, target, code: "HI", priority: 500);
        var lo = Policy(TenantA, FrequencyTargetType.AccountContactLink, target, code: "LO", priority: 100);
        repo.Items.Add(hi);
        repo.Items.Add(lo);

        // Analyse the HI policy — the conflict block resolves for its own target/context.
        var r = await Handler(TenantA, repo, new StubCounter(VisitFrequencyTargetImpact.Countable(1)))
            .Handle(new GetVisitFrequencyPolicyAnalysisQuery(hi.Id), default);

        Assert.Equal(FrequencyStatus.Resolved, r.Data!.Conflicts.Verdict);
        Assert.Equal(lo.Id, r.Data.Conflicts.SelectedPolicyId);
        Assert.Equal(2, r.Data.Conflicts.Candidates.Count);
        Assert.Contains(r.Data.Conflicts.Candidates, c => c.Selected && c.PolicyCode == "LO");
        Assert.Contains(r.Data.Conflicts.Candidates,
            c => !c.Selected && c.PolicyCode == "HI" && c.Reason == FrequencyReasonCodes.PolicySelectedByPriority);
        Assert.All(r.Data.Conflicts.Candidates, c => Assert.Contains("×/", c.FrequencySummary));
    }

    [Fact]
    public async Task Conflicts_Same_Band_Tie_Is_Conflict_But_Deterministic()
    {
        var repo = new FakeVfpRepo();
        var target = Guid.NewGuid();
        var a = Policy(TenantA, FrequencyTargetType.AccountContactLink, target, code: "A", priority: 300, from: Jan1);
        var b = Policy(TenantA, FrequencyTargetType.AccountContactLink, target, code: "B", priority: 300, from: Jan1);
        repo.Items.Add(a);
        repo.Items.Add(b);

        var r = await Handler(TenantA, repo, new StubCounter(VisitFrequencyTargetImpact.Countable(1)))
            .Handle(new GetVisitFrequencyPolicyAnalysisQuery(a.Id), default);

        Assert.Equal(FrequencyStatus.Conflict, r.Data!.Conflicts.Verdict);
        Assert.NotNull(r.Data.Conflicts.SelectedPolicyId);
    }

    // ---------------- Timeline (WP-FREQ-DET-C) ----------------

    [Fact]
    public async Task Timeline_Projects_Real_Events_Chronologically()
    {
        var repo = new FakeVfpRepo();
        var policy = Policy(TenantA, FrequencyTargetType.Account, Guid.NewGuid());
        policy.Events.Add(new VisitFrequencyPolicyEvent
        {
            Type = FrequencyPolicyEventType.Created, At = Jan1, By = "Ali"
        });
        policy.Events.Add(new VisitFrequencyPolicyEvent
        {
            Type = FrequencyPolicyEventType.WeightChanged, At = Jan1.AddDays(2), By = "Ali",
            FromValue = "baseline", ToValue = "standard"
        });
        policy.Events.Add(new VisitFrequencyPolicyEvent
        {
            Type = FrequencyPolicyEventType.Published, At = Jan1.AddDays(1), By = "Ali"
        });
        repo.Items.Add(policy);

        var r = await Handler(TenantA, repo, new StubCounter(VisitFrequencyTargetImpact.Countable(1)))
            .Handle(new GetVisitFrequencyPolicyAnalysisQuery(policy.Id), default);

        var tl = r.Data!.Timeline.Where(e => !e.IsFuture).ToList();
        Assert.Equal(3, tl.Count);
        Assert.Equal(FrequencyPolicyEventType.Created, tl[0].Type);
        Assert.Equal(FrequencyPolicyEventType.Published, tl[1].Type); // chronological, not insertion order
        Assert.Equal(FrequencyPolicyEventType.WeightChanged, tl[2].Type);
        Assert.Equal("baseline", tl[2].FromValue);
        Assert.Equal("standard", tl[2].ToValue);
    }

    [Fact]
    public async Task Timeline_Backfills_Created_And_Archived_When_Events_Empty()
    {
        var repo = new FakeVfpRepo();
        var policy = Policy(TenantA, FrequencyTargetType.Account, Guid.NewGuid(),
            status: FrequencyPolicyStatus.Archived);
        policy.CreatedAt = Jan1;
        policy.CreatedBy = "Ali";
        policy.ArchivedAt = Jan1.AddMonths(2);
        policy.ArchivedBy = "Veli";
        // Events left empty → pre-trail policy.
        repo.Items.Add(policy);

        var r = await Handler(TenantA, repo, new StubCounter(VisitFrequencyTargetImpact.Countable(1)))
            .Handle(new GetVisitFrequencyPolicyAnalysisQuery(policy.Id), default);

        var tl = r.Data!.Timeline.Where(e => !e.IsFuture).ToList();
        Assert.Equal(2, tl.Count);
        Assert.Equal(FrequencyPolicyEventType.Created, tl[0].Type);
        Assert.Equal("Ali", tl[0].By);
        Assert.Equal(FrequencyPolicyEventType.Archived, tl[1].Type);
        Assert.Equal("Veli", tl[1].By);
        // A pre-trail policy never invents a weight/status change.
        Assert.DoesNotContain(tl, e => e.Type == FrequencyPolicyEventType.WeightChanged);
    }

    [Fact]
    public async Task NextEval_Uses_CyclePeriod_End_When_Cycle_Scoped()
    {
        var repo = new FakeVfpRepo();
        var cyclePeriods = new FakeCyclePeriodRepo();
        var periodId = Guid.NewGuid();
        var periodEnd = Jan1.AddMonths(3);
        cyclePeriods.Rows.Add(new CyclePeriodEntity
        {
            Id = periodId, TenantId = TenantA, StartDate = Jan1, EndDate = periodEnd
        });
        var policy = Policy(TenantA, FrequencyTargetType.Account, Guid.NewGuid());
        policy.CyclePeriodId = periodId;
        policy.EffectiveTo = Jan1.AddYears(5); // must be ignored in favour of the cycle-period end
        repo.Items.Add(policy);

        var r = await Handler(TenantA, repo, new StubCounter(VisitFrequencyTargetImpact.Countable(1)), cyclePeriods)
            .Handle(new GetVisitFrequencyPolicyAnalysisQuery(policy.Id), default);

        var future = Assert.Single(r.Data!.Timeline, e => e.IsFuture);
        Assert.Equal(FrequencyPolicyEventType.NextEval, future.Type);
        Assert.Equal(periodEnd, future.At);
    }

    [Fact]
    public async Task NextEval_Falls_Back_To_EffectiveTo_Then_Omits_When_Neither_Present()
    {
        var repo = new FakeVfpRepo();
        var withTo = Policy(TenantA, FrequencyTargetType.Account, Guid.NewGuid(), code: "TO");
        withTo.EffectiveTo = Jan1.AddMonths(6);
        var openEnded = Policy(TenantA, FrequencyTargetType.Account, Guid.NewGuid(), code: "OPEN");
        repo.Items.Add(withTo);
        repo.Items.Add(openEnded);

        var rTo = await Handler(TenantA, repo, new StubCounter(VisitFrequencyTargetImpact.Countable(1)))
            .Handle(new GetVisitFrequencyPolicyAnalysisQuery(withTo.Id), default);
        var future = Assert.Single(rTo.Data!.Timeline, e => e.IsFuture);
        Assert.Equal(Jan1.AddMonths(6), future.At);

        var rOpen = await Handler(TenantA, repo, new StubCounter(VisitFrequencyTargetImpact.Countable(1)))
            .Handle(new GetVisitFrequencyPolicyAnalysisQuery(openEnded.Id), default);
        Assert.DoesNotContain(rOpen.Data!.Timeline, e => e.IsFuture);
    }

    // ---------------- Real impact counter (per-type counting via REUSED readers) ----------------

    [Theory]
    [InlineData(FrequencyTargetType.Account)]
    [InlineData(FrequencyTargetType.Contact)]
    [InlineData(FrequencyTargetType.AccountContactLink)]
    public async Task Counter_Single_Record_Targets_Count_One(string targetType)
    {
        var counter = RealCounter(out _, out _, out _, out _);
        var impact = await counter.CountAsync(
            TenantA, Policy(TenantA, targetType, Guid.NewGuid()), DateTimeOffset.UtcNow, default);
        Assert.True(impact.Computable);
        Assert.Equal(1, impact.Count);
    }

    [Theory]
    [InlineData(FrequencyTargetType.AudienceProfile)]
    [InlineData(FrequencyTargetType.ConceptNode)]
    public async Task Counter_Group_Targets_Without_A_Reader_Are_Uncomputable(string targetType)
    {
        var counter = RealCounter(out _, out _, out _, out _);
        var impact = await counter.CountAsync(
            TenantA, Policy(TenantA, targetType, Guid.NewGuid()), DateTimeOffset.UtcNow, default);
        Assert.False(impact.Computable);
        Assert.Null(impact.Count);
        Assert.NotNull(impact.Note);
    }

    [Fact]
    public async Task Counter_Segment_Uses_Membership_Resolver_Total()
    {
        var segments = new FakeSegmentRepository();
        var candidates = new FakeCandidateSource();
        var counter = RealCounter(out _, out _, segments, candidates);

        var segment = SegmentTestBuilders.Segment(
            TenantA, type: SegmentTypes.Dynamic,
            criteria: SegmentTestBuilders.Criteria(SegmentTestBuilders.Predicate(
                SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.Eq, SegmentValueTypes.String,
                new[] { "cardiology" })));
        segments.Rows.Add(segment);
        for (var i = 0; i < 7; i++)
        {
            candidates.Candidates.Add(SegmentTestBuilders.Contact(Guid.NewGuid(), specialty: "cardiology"));
        }

        var impact = await counter.CountAsync(
            TenantA,
            Policy(TenantA, FrequencyTargetType.Segment, segment.Id, segmentId: segment.Id),
            SegmentTestDoubles.Now, default);

        Assert.True(impact.Computable);
        Assert.Equal(7, impact.Count);
    }

    [Fact]
    public async Task Counter_Segment_Cap_Exceeded_Is_Uncomputable()
    {
        var segments = new FakeSegmentRepository();
        var candidates = new FakeCandidateSource { ForceCapExceeded = true };
        var counter = RealCounter(out _, out _, segments, candidates);

        var segment = SegmentTestBuilders.Segment(TenantA, type: SegmentTypes.Dynamic);
        segments.Rows.Add(segment);

        var impact = await counter.CountAsync(
            TenantA,
            Policy(TenantA, FrequencyTargetType.Segment, segment.Id, segmentId: segment.Id),
            SegmentTestDoubles.Now, default);

        Assert.False(impact.Computable);
        Assert.Null(impact.Count);
    }

    [Fact]
    public async Task Counter_Segment_Missing_Is_Uncomputable()
    {
        var counter = RealCounter(out _, out _, out _, out _);
        var impact = await counter.CountAsync(
            TenantA,
            Policy(TenantA, FrequencyTargetType.Segment, Guid.NewGuid(), segmentId: Guid.NewGuid()),
            SegmentTestDoubles.Now, default);

        Assert.False(impact.Computable);
        Assert.Equal("segment bulunamadı", impact.Note);
    }

    [Fact]
    public async Task Counter_CampaignTarget_Counts_Live_Members_Only()
    {
        var campaignTargets = new FakeCampaignTargetRepo();
        var campaignId = Guid.NewGuid();
        campaignTargets.Rows.Add(CampaignTargetRow(campaignId, CampaignTargetStatuses.Active));
        campaignTargets.Rows.Add(CampaignTargetRow(campaignId, CampaignTargetStatuses.Draft));
        campaignTargets.Rows.Add(CampaignTargetRow(campaignId, CampaignTargetStatuses.Excluded)); // not a member
        var archived = CampaignTargetRow(campaignId, CampaignTargetStatuses.Active);
        archived.ArchivedAt = DateTimeOffset.UtcNow; // not a member
        campaignTargets.Rows.Add(archived);

        var counter = new VisitFrequencyTargetImpactCounter(
            new FakeSegmentRepository(), Membership(new FakeCandidateSource()),
            campaignTargets, new FakeAccountTerritoryAssignmentRepo(), new FakeTerritoryModelRepo());

        var impact = await counter.CountAsync(
            TenantA, Policy(TenantA, FrequencyTargetType.CampaignTarget, campaignId), DateTimeOffset.UtcNow, default);

        Assert.True(impact.Computable);
        Assert.Equal(2, impact.Count);
    }

    [Fact]
    public async Task Counter_TerritoryNode_Counts_Current_Coverage()
    {
        var models = new FakeTerritoryModelRepo();
        var assignments = new FakeAccountTerritoryAssignmentRepo();
        var model = new TerritoryModel
        {
            TenantId = TenantA, Status = "active",
            EffectiveFrom = DateTimeOffset.UtcNow.AddYears(-1), EffectiveTo = DateTimeOffset.UtcNow.AddYears(1)
        };
        models.Items.Add(model);
        var node = Guid.NewGuid();
        assignments.Items.Add(Assignment(model.Id, node, Guid.NewGuid()));
        assignments.Items.Add(Assignment(model.Id, node, Guid.NewGuid()));
        assignments.Items.Add(Assignment(model.Id, Guid.NewGuid(), Guid.NewGuid())); // different node

        var counter = new VisitFrequencyTargetImpactCounter(
            new FakeSegmentRepository(), Membership(new FakeCandidateSource()),
            new FakeCampaignTargetRepo(), assignments, models);

        var impact = await counter.CountAsync(
            TenantA, Policy(TenantA, FrequencyTargetType.TerritoryNode, node, territoryNodeId: node),
            DateTimeOffset.UtcNow, default);

        Assert.True(impact.Computable);
        Assert.Equal(2, impact.Count);
    }

    // ---------------- helpers ----------------

    private static SegmentMembershipResolver Membership(FakeCandidateSource candidates)
        => new(candidates,
            new SegmentAttributeSourceReader(
                candidates, new FakeConsentBulkReader(), new FakeTerritoryCoverageReader(), new FakeConceptAffinityReader()),
            new FakeTargetCustomerRepository());

    private static VisitFrequencyTargetImpactCounter RealCounter(
        out FakeCampaignTargetRepo campaignTargets,
        out FakeAccountTerritoryAssignmentRepo assignments,
        FakeSegmentRepository segments,
        FakeCandidateSource candidates)
    {
        campaignTargets = new FakeCampaignTargetRepo();
        assignments = new FakeAccountTerritoryAssignmentRepo();
        return new VisitFrequencyTargetImpactCounter(
            segments, Membership(candidates), campaignTargets, assignments, new FakeTerritoryModelRepo());
    }

    // Overload used where the segment/campaign/territory data is irrelevant.
    private static VisitFrequencyTargetImpactCounter RealCounter(
        out FakeCampaignTargetRepo campaignTargets,
        out FakeAccountTerritoryAssignmentRepo assignments,
        out FakeSegmentRepository segments,
        out FakeCandidateSource candidates)
    {
        segments = new FakeSegmentRepository();
        candidates = new FakeCandidateSource();
        return RealCounter(out campaignTargets, out assignments, segments, candidates);
    }

    private static CampaignTarget CampaignTargetRow(Guid campaignId, string status) => new()
    {
        TenantId = TenantA, CampaignId = campaignId, TargetType = CampaignTargetTypes.Account,
        TargetId = Guid.NewGuid(), TargetStatus = status, TargetSource = CampaignTargetSources.Manual,
        SelectionReason = "test", ReasonCodes = { CampaignReasonCodes.ManualTargetSelected },
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1)
    };

    private static AccountTerritoryAssignment Assignment(Guid modelId, Guid nodeId, Guid accountId) => new()
    {
        TenantId = TenantA, TerritoryModelId = modelId, AccountId = accountId,
        AccountCode = "ACC", AccountDisplayName = "Account", TerritoryNodeId = nodeId,
        TerritoryNodeCode = "ZONE", TerritoryNodeName = "Zone",
        AssignmentSource = "rule", AssignmentStatus = "active", ConflictPolicy = "block",
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-10)
    };

    // ---------------- fakes ----------------

    private sealed class StubCounter : IVisitFrequencyTargetImpactCounter
    {
        private readonly VisitFrequencyTargetImpact _impact;
        public StubCounter(VisitFrequencyTargetImpact impact) => _impact = impact;

        public Task<VisitFrequencyTargetImpact> CountAsync(
            Guid tenantId, Vfp policy, DateTimeOffset at, CancellationToken cancellationToken)
            => Task.FromResult(_impact);
    }

    private sealed class FakeVfpRepo : IVisitFrequencyPolicyRepository
    {
        public List<Vfp> Items { get; } = new();

        public Task<Vfp?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(p => p.TenantId == t && p.Id == id && !p.IsDeleted));

        public Task<IReadOnlyList<Vfp>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Vfp>)Items.Where(p => p.TenantId == t && !p.IsDeleted).ToList());

        public Task<Vfp?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(p =>
                p.TenantId == t && !p.IsDeleted && p.PolicyCode == code && p.Status != FrequencyPolicyStatus.Archived));

        public Task<IReadOnlyList<Vfp>> ListActiveByTargetsAsync(
            Guid t, IReadOnlyCollection<Guid> targetIds, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Vfp>)Items.Where(p =>
                p.TenantId == t && !p.IsDeleted && p.Status == FrequencyPolicyStatus.Active
                && targetIds.Contains(p.TargetId)).ToList());

        public Task InsertAsync(Vfp policy, CancellationToken ct) { Items.Add(policy); return Task.CompletedTask; }
        public Task UpdateAsync(Vfp policy, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeCyclePeriodRepo : ICyclePeriodRepository
    {
        public List<CyclePeriodEntity> Rows { get; } = new();

        public Task<CyclePeriodEntity?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Rows.FirstOrDefault(r => r.TenantId == t && r.Id == id && !r.IsDeleted));

        public Task<IReadOnlyList<CyclePeriodEntity>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<CyclePeriodEntity>)Rows.Where(r => r.TenantId == t && !r.IsDeleted).ToList());

        public Task<IReadOnlyList<CyclePeriodEntity>> ListByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<CyclePeriodEntity>)Rows
                .Where(r => r.TenantId == t && !r.IsDeleted && r.CycleCode == code).ToList());

        public Task<IReadOnlyList<CyclePeriodEntity>> ListByYearAsync(Guid t, int year, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<CyclePeriodEntity>)Rows
                .Where(r => r.TenantId == t && !r.IsDeleted && r.Year == year).ToList());

        public Task<IReadOnlyList<CyclePeriodEntity>> ListActiveAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<CyclePeriodEntity>)Rows
                .Where(r => r.TenantId == t && !r.IsDeleted).ToList());

        public Task InsertAsync(CyclePeriodEntity entity, CancellationToken ct) { Rows.Add(entity); return Task.CompletedTask; }

        public Task<bool> ReplaceAsync(CyclePeriodEntity entity, int expectedVersion, CancellationToken ct)
            => Task.FromResult(true);
    }

    private sealed class FakeCampaignTargetRepo : ICampaignTargetRepository
    {
        public List<CampaignTarget> Rows { get; } = new();

        public Task<CampaignTarget?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Rows.FirstOrDefault(r => r.TenantId == t && r.Id == id && !r.IsDeleted));

        public Task<IReadOnlyList<CampaignTarget>> ListByCampaignAsync(Guid t, Guid campaignId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<CampaignTarget>)Rows
                .Where(r => r.TenantId == t && r.CampaignId == campaignId && !r.IsDeleted).ToList());

        public Task<CampaignTarget?> FindActiveByTargetAsync(
            Guid t, Guid campaignId, string targetType, Guid targetId, CancellationToken ct)
            => Task.FromResult<CampaignTarget?>(null);

        public Task InsertAsync(CampaignTarget target, CancellationToken ct) { Rows.Add(target); return Task.CompletedTask; }
        public Task UpdateAsync(CampaignTarget target, CancellationToken ct) => Task.CompletedTask;
    }
}
