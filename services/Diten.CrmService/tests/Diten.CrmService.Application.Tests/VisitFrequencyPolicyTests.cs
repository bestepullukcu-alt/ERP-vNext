using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Commands;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Contract;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Handlers;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Queries;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Resolve;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;
using Vfp = Diten.CrmService.Domain.Entities.VisitFrequencyPolicy;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// MOD-0165 FU03 — Visit Frequency / Call-Cycle Policy. Pins down: policy is its own aggregate (never a flat field),
/// TenantId is claim-only, RequiredVisitCount &gt; 0, EffectiveTo ≥ EffectiveFrom, the freq×period matrix, the
/// cycle/campaign/segment context guards, deterministic priority/specificity/effective-from resolution with visible
/// candidate diagnostics, no-policy ⇒ unknown (never a default), a same-band tie ⇒ conflict, archive removes a policy
/// from resolve but keeps it readable, no DELETE, and a write-free resolve endpoint.
/// </summary>
public sealed class VisitFrequencyPolicyTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Jan1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Jun1 = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private static TenantContext Tenant(Guid id)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id);
        return ctx;
    }

    private sealed class Fixture
    {
        public FakeRepo Repo { get; } = new();
        public Guid TenantId { get; }

        public Fixture(Guid tenant) => TenantId = tenant;

        public CreateVisitFrequencyPolicyHandler Create(Guid? tenant = null)
            => new(Tenant(tenant ?? TenantId), new NullActorContext(), Repo);

        public UpdateVisitFrequencyPolicyHandler Update()
            => new(Tenant(TenantId), new NullActorContext(), Repo);

        public ArchiveVisitFrequencyPolicyHandler Archive()
            => new(Tenant(TenantId), new NullActorContext(), Repo);

        public ResolveVisitFrequencyPolicyHandler Resolver()
            => new(Tenant(TenantId), new VisitFrequencyPolicyResolver(Tenant(TenantId), Repo));

        public GetVisitFrequencyPolicyHandler Get()
            => new(Tenant(TenantId), Repo);

        public ListVisitFrequencyPoliciesHandler List()
            => new(Tenant(TenantId), Repo);
    }

    private static CreateVisitFrequencyPolicyCommand Cmd(
        Guid targetId,
        string code = "VFP-1",
        string targetType = FrequencyTargetType.AccountContactLink,
        string frequencyType = FrequencyType.Weekly,
        int requiredVisitCount = 2,
        string periodType = FrequencyPeriodType.Week,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int priority = 300,
        string source = FrequencySource.Manual,
        string? status = FrequencyPolicyStatus.Active,
        Guid? campaignId = null,
        Guid? segmentId = null,
        Guid? cycleId = null,
        string? notes = null)
        => new(code, "Policy " + code, targetType, targetId, frequencyType, requiredVisitCount, periodType,
            from ?? Jan1, priority, source, status, null, null, null, campaignId, segmentId, null, null, cycleId, null, to, notes);

    // ---------------- Create validation ----------------

    [Fact]
    public async Task Create_Valid_Persists_And_Returns_201()
    {
        var f = new Fixture(TenantA);
        var r = await f.Create().Handle(Cmd(Guid.NewGuid()), default);
        Assert.Equal(201, r.StatusCode);
        var row = Assert.Single(f.Repo.Items);
        Assert.Equal(TenantA, row.TenantId);
        Assert.Equal("account-contact-link", row.TargetType);
        Assert.Equal("active", row.Status);
    }

    [Fact]
    public async Task Create_Without_Tenant_Returns_400()
    {
        var handler = new CreateVisitFrequencyPolicyHandler(new TenantContext(), new NullActorContext(), new FakeRepo());
        var r = await handler.Handle(Cmd(Guid.NewGuid()), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Create_RequiredVisitCount_Zero_Returns_400()
    {
        var f = new Fixture(TenantA);
        var r = await f.Create().Handle(Cmd(Guid.NewGuid(), requiredVisitCount: 0), default);
        Assert.Equal(400, r.StatusCode);
        Assert.Empty(f.Repo.Items);
    }

    [Fact]
    public async Task Create_EffectiveTo_Before_EffectiveFrom_Returns_400()
    {
        var f = new Fixture(TenantA);
        var r = await f.Create().Handle(Cmd(Guid.NewGuid(), from: Jun1, to: Jan1), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Create_CycleBased_Without_Cycle_Returns_400()
    {
        var f = new Fixture(TenantA);
        var r = await f.Create().Handle(
            Cmd(Guid.NewGuid(), frequencyType: FrequencyType.CycleBased, periodType: FrequencyPeriodType.Cycle), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Create_CycleBased_With_Cycle_Succeeds()
    {
        var f = new Fixture(TenantA);
        var r = await f.Create().Handle(
            Cmd(Guid.NewGuid(), frequencyType: FrequencyType.CycleBased, periodType: FrequencyPeriodType.Cycle, cycleId: Guid.NewGuid()),
            default);
        Assert.Equal(201, r.StatusCode);
    }

    [Fact]
    public async Task Create_CampaignPeriod_Without_Campaign_Returns_400()
    {
        var f = new Fixture(TenantA);
        var r = await f.Create().Handle(
            Cmd(Guid.NewGuid(), frequencyType: FrequencyType.Custom, periodType: FrequencyPeriodType.CampaignPeriod, notes: "x"), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Create_Unknown_TargetType_Returns_400()
    {
        var f = new Fixture(TenantA);
        var r = await f.Create().Handle(Cmd(Guid.NewGuid(), targetType: "planet"), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Create_Empty_TargetId_Returns_400()
    {
        var f = new Fixture(TenantA);
        var r = await f.Create().Handle(Cmd(Guid.Empty), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Create_Incompatible_Frequency_Period_Returns_400()
    {
        var f = new Fixture(TenantA);
        // weekly requires PeriodType=week; month is invalid.
        var r = await f.Create().Handle(Cmd(Guid.NewGuid(), frequencyType: FrequencyType.Weekly, periodType: FrequencyPeriodType.Month), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Create_Custom_Without_Notes_Returns_400()
    {
        var f = new Fixture(TenantA);
        var r = await f.Create().Handle(Cmd(Guid.NewGuid(), frequencyType: FrequencyType.Custom, periodType: FrequencyPeriodType.Custom), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Create_Segmentation_Source_Requires_SegmentId()
    {
        var f = new Fixture(TenantA);
        var r = await f.Create().Handle(Cmd(Guid.NewGuid(), source: FrequencySource.Segmentation), default);
        Assert.Equal(400, r.StatusCode);

        var ok = await f.Create().Handle(Cmd(Guid.NewGuid(), code: "VFP-SEG", source: FrequencySource.Segmentation, segmentId: Guid.NewGuid()), default);
        Assert.Equal(201, ok.StatusCode);
    }

    [Fact]
    public async Task Create_Duplicate_Active_Code_Returns_409()
    {
        var f = new Fixture(TenantA);
        await f.Create().Handle(Cmd(Guid.NewGuid(), code: "DUP"), default);
        var r = await f.Create().Handle(Cmd(Guid.NewGuid(), code: "DUP"), default);
        Assert.Equal(409, r.StatusCode);
    }

    // ---------------- Resolve ----------------

    [Fact]
    public async Task Resolve_Active_Effective_Policy_Is_Resolved()
    {
        var f = new Fixture(TenantA);
        var target = Guid.NewGuid();
        await f.Create().Handle(Cmd(target), default);

        var r = await f.Resolver().Handle(
            new ResolveVisitFrequencyPolicyQuery(FrequencyTargetType.AccountContactLink, target, Jun1), default);

        Assert.Equal(200, r.StatusCode);
        Assert.Equal(FrequencyStatus.Resolved, r.Data!.FrequencyStatus);
        Assert.Equal(2, r.Data.RequiredVisitCount);
        Assert.Contains(FrequencyReasonCodes.FrequencyPolicyResolved, r.Data.ReasonCodes);
    }

    [Fact]
    public async Task Resolve_No_Policy_Is_Unknown_Not_Default()
    {
        var f = new Fixture(TenantA);
        var r = await f.Resolver().Handle(
            new ResolveVisitFrequencyPolicyQuery(FrequencyTargetType.Account, Guid.NewGuid(), Jun1), default);

        Assert.Equal(FrequencyStatus.Unknown, r.Data!.FrequencyStatus);
        Assert.Null(r.Data.SelectedFrequencyPolicyId);
        Assert.Null(r.Data.RequiredVisitCount);
        Assert.Contains(FrequencyReasonCodes.NoMatchingPolicy, r.Data.ReasonCodes);
    }

    [Fact]
    public async Task Resolve_Draft_And_Archived_Not_Selected()
    {
        var f = new Fixture(TenantA);
        var target = Guid.NewGuid();
        await f.Create().Handle(Cmd(target, code: "DRAFT", status: FrequencyPolicyStatus.Draft), default);

        var r = await f.Resolver().Handle(
            new ResolveVisitFrequencyPolicyQuery(FrequencyTargetType.AccountContactLink, target, Jun1), default);
        Assert.Equal(FrequencyStatus.Unknown, r.Data!.FrequencyStatus);
    }

    [Fact]
    public async Task Resolve_OutOfWindow_Policy_Not_Selected()
    {
        var f = new Fixture(TenantA);
        var target = Guid.NewGuid();
        await f.Create().Handle(Cmd(target, from: Jan1, to: new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)), default);

        var r = await f.Resolver().Handle(
            new ResolveVisitFrequencyPolicyQuery(FrequencyTargetType.AccountContactLink, target, Jun1), default);
        Assert.Equal(FrequencyStatus.Unknown, r.Data!.FrequencyStatus);
    }

    [Fact]
    public async Task Resolve_Lower_Priority_Wins()
    {
        var f = new Fixture(TenantA);
        var target = Guid.NewGuid();
        await f.Create().Handle(Cmd(target, code: "HI", priority: 500), default);
        await f.Create().Handle(Cmd(target, code: "LO", priority: 100), default);

        var r = await f.Resolver().Handle(
            new ResolveVisitFrequencyPolicyQuery(FrequencyTargetType.AccountContactLink, target, Jun1), default);

        Assert.Equal(FrequencyStatus.Resolved, r.Data!.FrequencyStatus);
        Assert.Equal("LO", r.Data.SelectedPolicyCode);
        Assert.Contains(FrequencyReasonCodes.PolicySelectedByPriority, r.Data.ReasonCodes);
        Assert.Equal(2, r.Data.CandidatePolicies.Count); // winner + loser visible
        Assert.Contains(r.Data.CandidatePolicies, c => !c.Selected && c.PolicyCode == "HI");
    }

    [Fact]
    public async Task Resolve_More_Specific_Target_Wins_Tie()
    {
        var f = new Fixture(TenantA);
        var link = Guid.NewGuid();
        var segment = Guid.NewGuid();
        // Same priority; account-contact-link is more specific than segment.
        await f.Create().Handle(Cmd(link, code: "LINK", targetType: FrequencyTargetType.AccountContactLink, priority: 300), default);
        await f.Create().Handle(Cmd(segment, code: "SEG", targetType: FrequencyTargetType.Segment, priority: 300, source: FrequencySource.Segmentation, segmentId: segment), default);

        var r = await f.Resolver().Handle(
            new ResolveVisitFrequencyPolicyQuery(FrequencyTargetType.AccountContactLink, link, Jun1, SegmentId: segment), default);

        Assert.Equal("LINK", r.Data!.SelectedPolicyCode);
        Assert.Contains(FrequencyReasonCodes.PolicySelectedBySpecificity, r.Data.ReasonCodes);
    }

    [Fact]
    public async Task Resolve_Latest_EffectiveFrom_Wins_Tie()
    {
        var f = new Fixture(TenantA);
        var target = Guid.NewGuid();
        await f.Create().Handle(Cmd(target, code: "OLD", priority: 300, from: Jan1), default);
        await f.Create().Handle(Cmd(target, code: "NEW", priority: 300, from: new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero)), default);

        var r = await f.Resolver().Handle(
            new ResolveVisitFrequencyPolicyQuery(FrequencyTargetType.AccountContactLink, target, Jun1), default);

        Assert.Equal("NEW", r.Data!.SelectedPolicyCode);
        Assert.Contains(FrequencyReasonCodes.PolicySelectedByLatestEffectiveFrom, r.Data.ReasonCodes);
    }

    [Fact]
    public async Task Resolve_Same_Band_Tie_Is_Conflict_But_Deterministic()
    {
        var f = new Fixture(TenantA);
        var target = Guid.NewGuid();
        // identical priority, same target type (specificity), same effective-from → same band tie
        await f.Create().Handle(Cmd(target, code: "A", priority: 300, from: Jan1), default);
        await f.Create().Handle(Cmd(target, code: "B", priority: 300, from: Jan1), default);

        var r = await f.Resolver().Handle(
            new ResolveVisitFrequencyPolicyQuery(FrequencyTargetType.AccountContactLink, target, Jun1), default);

        Assert.Equal(FrequencyStatus.Conflict, r.Data!.FrequencyStatus);
        Assert.NotNull(r.Data.SelectedFrequencyPolicyId); // still deterministically selected
        Assert.Contains(FrequencyReasonCodes.PolicyConflict, r.Data.ReasonCodes);
    }

    [Fact]
    public async Task Resolve_IncludeDiagnostics_False_Hides_Candidates()
    {
        var f = new Fixture(TenantA);
        var target = Guid.NewGuid();
        await f.Create().Handle(Cmd(target, code: "HI", priority: 500), default);
        await f.Create().Handle(Cmd(target, code: "LO", priority: 100), default);

        var r = await f.Resolver().Handle(
            new ResolveVisitFrequencyPolicyQuery(FrequencyTargetType.AccountContactLink, target, Jun1, IncludeDiagnostics: false), default);
        Assert.Empty(r.Data!.CandidatePolicies);
        Assert.Equal("LO", r.Data.SelectedPolicyCode);
    }

    [Fact]
    public async Task Resolve_Does_Not_Mutate_State()
    {
        var f = new Fixture(TenantA);
        var target = Guid.NewGuid();
        await f.Create().Handle(Cmd(target), default);
        var before = f.Repo.WriteCount;

        await f.Resolver().Handle(new ResolveVisitFrequencyPolicyQuery(FrequencyTargetType.AccountContactLink, target, Jun1), default);
        await f.Resolver().Handle(new ResolveVisitFrequencyPolicyQuery(FrequencyTargetType.AccountContactLink, target, Jun1), default);

        Assert.Equal(before, f.Repo.WriteCount); // resolve performed zero writes
    }

    [Fact]
    public async Task Resolve_Unknown_TargetType_Returns_400()
    {
        var f = new Fixture(TenantA);
        var r = await f.Resolver().Handle(new ResolveVisitFrequencyPolicyQuery("planet", Guid.NewGuid(), Jun1), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Resolve_Contact_Without_Location_Surfaces_Reason()
    {
        var f = new Fixture(TenantA);
        var target = Guid.NewGuid();
        await f.Create().Handle(Cmd(target, code: "CT", targetType: FrequencyTargetType.Contact, priority: 400), default);

        var r = await f.Resolver().Handle(new ResolveVisitFrequencyPolicyQuery(FrequencyTargetType.Contact, target, Jun1), default);
        Assert.Contains(FrequencyReasonCodes.ContactLocationContextAbsent, r.Data!.ReasonCodes);
    }

    // ---------------- Lifecycle ----------------

    [Fact]
    public async Task Archive_Removes_From_Resolve_But_Keeps_Read()
    {
        var f = new Fixture(TenantA);
        var target = Guid.NewGuid();
        var created = await f.Create().Handle(Cmd(target), default);
        var policyId = created.Data;

        var archive = await f.Archive().Handle(new ArchiveVisitFrequencyPolicyCommand(policyId), default);
        Assert.Equal(200, archive.StatusCode);

        var resolve = await f.Resolver().Handle(
            new ResolveVisitFrequencyPolicyQuery(FrequencyTargetType.AccountContactLink, target, Jun1), default);
        Assert.Equal(FrequencyStatus.Unknown, resolve.Data!.FrequencyStatus);

        var read = await f.Get().Handle(new GetVisitFrequencyPolicyQuery(policyId), default);
        Assert.Equal(200, read.StatusCode);
        Assert.Equal("archived", read.Data!.Status);
        Assert.NotNull(read.Data.ArchivedAt);
    }

    [Fact]
    public async Task Update_Archived_Policy_Returns_409()
    {
        var f = new Fixture(TenantA);
        var created = await f.Create().Handle(Cmd(Guid.NewGuid()), default);
        await f.Archive().Handle(new ArchiveVisitFrequencyPolicyCommand(created.Data), default);

        var update = await f.Update().Handle(new UpdateVisitFrequencyPolicyCommand(
            created.Data, "Renamed", FrequencyType.Weekly, 3, FrequencyPeriodType.Week, Jan1, 300, FrequencySource.Manual), default);
        Assert.Equal(409, update.StatusCode);
    }

    [Fact]
    public async Task CrossTenant_Get_Returns_404()
    {
        var f = new Fixture(TenantA);
        var created = await f.Create().Handle(Cmd(Guid.NewGuid()), default);

        var otherTenant = new GetVisitFrequencyPolicyHandler(Tenant(TenantB), f.Repo);
        var r = await otherTenant.Handle(new GetVisitFrequencyPolicyQuery(created.Data), default);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task Brand_Product_Optional_NonPharma_Policy_Works()
    {
        var f = new Fixture(TenantA);
        var target = Guid.NewGuid();
        var r = await f.Create().Handle(Cmd(target, code: "NP"), default); // no brand/product
        Assert.Equal(201, r.StatusCode);

        var resolve = await f.Resolver().Handle(
            new ResolveVisitFrequencyPolicyQuery(FrequencyTargetType.AccountContactLink, target, Jun1), default);
        Assert.Equal(FrequencyStatus.Resolved, resolve.Data!.FrequencyStatus);
    }

    [Fact]
    public async Task Campaign_Segment_Source_Provenance_Is_Visible()
    {
        var f = new Fixture(TenantA);
        var target = Guid.NewGuid();
        var campaign = Guid.NewGuid();
        await f.Create().Handle(Cmd(target, code: "CMP", source: FrequencySource.Campaign, campaignId: campaign), default);

        var read = await f.List().Handle(new ListVisitFrequencyPoliciesQuery(Source: FrequencySource.Campaign), default);
        var dto = Assert.Single(read.Data!.Items);
        Assert.Equal("campaign", dto.Source);
        Assert.Equal(campaign, dto.CampaignId);
    }

    [Fact]
    public void Contract_Flags_Are_Correct_And_Forbidden_Absent()
    {
        var handler = new GetVisitFrequencyContractHandler(Tenant(TenantA));
        var r = handler.Handle(new GetVisitFrequencyContractQuery(), default).GetAwaiter().GetResult();

        Assert.True(r.Data!.Features.SupportsVisitFrequencyPolicy);
        Assert.True(r.Data.Features.SupportsCallCyclePolicy);
        Assert.True(r.Data.Features.SupportsFrequencyPolicyPriority);
        Assert.True(r.Data.Features.SupportsFrequencyPolicyEffectiveWindow);
        Assert.True(r.Data.Features.SupportsFrequencyPolicyProvider);

        // Forbidden planning/detailing/recommendation/consent/workflow flags must not exist on the flags record.
        var props = r.Data.Features.GetType().GetProperties().Select(p => p.Name).ToList();
        foreach (var forbidden in new[]
                 {
                     "SupportsVisitPlanning", "SupportsRoutePlanning", "SupportsDueOverdueEngine",
                     "SupportsDigitalDetailing", "SupportsRecommendationEngine", "SupportsConsentEvaluationEngine",
                     "SupportsWorkflowApproval"
                 })
        {
            Assert.DoesNotContain(forbidden, props);
        }
    }

    [Fact]
    public void Resolve_Result_Has_No_Route_Visit_Due_LastVisit_Consent_Fields()
    {
        // Response shape guard: the resolve result type must never leak a consumer-domain field.
        var props = typeof(VisitFrequencyResolveResult).GetProperties().Select(p => p.Name.ToLowerInvariant()).ToList();
        foreach (var banned in new[] { "duestatus", "lastvisitdate", "routeorder", "distance", "traveltime", "visitplanid", "consentallowed" })
        {
            Assert.DoesNotContain(banned, props);
        }
    }

    // ---------------- Fake repository ----------------

    private sealed class FakeRepo : IVisitFrequencyPolicyRepository
    {
        public List<Vfp> Items { get; } = new();
        public int WriteCount { get; private set; }

        public Task<Vfp?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(p => p.TenantId == t && p.Id == id && !p.IsDeleted));

        public Task<IReadOnlyList<Vfp>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Vfp>)Items.Where(p => p.TenantId == t && !p.IsDeleted).OrderByDescending(p => p.CreatedAt).ToList());

        public Task<Vfp?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(p =>
                p.TenantId == t && !p.IsDeleted && p.PolicyCode == code && p.Status != FrequencyPolicyStatus.Archived));

        public Task<IReadOnlyList<Vfp>> ListActiveByTargetsAsync(Guid t, IReadOnlyCollection<Guid> targetIds, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Vfp>)Items.Where(p =>
                p.TenantId == t && !p.IsDeleted && p.Status == FrequencyPolicyStatus.Active && targetIds.Contains(p.TargetId)).ToList());

        public Task InsertAsync(Vfp policy, CancellationToken ct)
        {
            WriteCount++;
            Items.Add(policy);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Vfp policy, CancellationToken ct)
        {
            WriteCount++;
            return Task.CompletedTask;
        }
    }
}
