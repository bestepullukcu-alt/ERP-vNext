using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// SCMM-15 (CAND-CAP-0011, DEC-SCMM-04 C3) — ContentSetRevision freeze + in-domain review. Pins down: submit freezes a
/// byte-for-byte immutable manifest (later draft edits do not leak into the revision); idempotent open-revision reuse;
/// stale ExpectedVersion → 409; shape guard (≥1 component; unresolved eligibility → 400); lineage RevisionNumber = max+1;
/// approve/reject transitions; SoD reviewer≠author → 403; duplicate decision → existing (409 on conflict); tenant isolation.
/// </summary>
public sealed class ContentSetRevisionTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Jan1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static TenantContext Tenant(Guid id)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id);
        return ctx;
    }

    private sealed class Actor : IActorContext
    {
        public Actor(string? name) => ActorName = name;
        public string? ActorName { get; }
    }

    private sealed class FakeSetRepo : IContentSetRepository
    {
        public List<ContentSet> Items { get; } = new();
        public Task<ContentSet?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));
        public Task<IReadOnlyList<ContentSet>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ContentSet>)Items.Where(x => x.TenantId == t && !x.IsDeleted).ToList());
        public Task<ContentSet?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && !x.IsDeleted && x.SetCode == code && !x.IsArchived()));
        public Task InsertAsync(ContentSet e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(ContentSet e, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeRevisionRepo : IContentSetRevisionRepository
    {
        public List<ContentSetRevision> Items { get; } = new();
        public Task<ContentSetRevision?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));
        public Task<IReadOnlyList<ContentSetRevision>> ListByContentSetAsync(Guid t, Guid setId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ContentSetRevision>)Items
                .Where(x => x.TenantId == t && x.ContentSetId == setId && !x.IsDeleted)
                .OrderByDescending(x => x.RevisionNumber).ToList());
        public Task InsertAsync(ContentSetRevision e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(ContentSetRevision e, CancellationToken ct) => Task.CompletedTask; // mutation-by-reference
    }

    private sealed class Fixture
    {
        public FakeSetRepo Sets { get; } = new();
        public FakeRevisionRepo Revisions { get; } = new();

        public SubmitContentSetForReviewHandler Submit(string? actor, Guid tenant)
            => new(Tenant(tenant), new Actor(actor), Sets, Revisions);
        public RecordReviewDecisionHandler Decide(string? actor, Guid tenant)
            => new(Tenant(tenant), new Actor(actor), Revisions);
        public GetContentSetRevisionByIdHandler Get(Guid tenant)
            => new(Tenant(tenant), Revisions);
        public ListContentSetRevisionsHandler List(Guid tenant)
            => new(Tenant(tenant), Revisions);
    }

    private static ContentSet SeedSet(FakeSetRepo repo, Guid tenant, int components = 1, int version = 0,
        ContentSetEligibilitySnapshot? eligibility = null)
    {
        var set = new ContentSet
        {
            TenantId = tenant, SetCode = "SET-1", SetName = "Set", Status = ContentSetStatuses.Draft,
            Template = new ContentSetTemplateRef { ConceptChainTemplateId = Guid.NewGuid(), ChainVersion = "1.0" },
            Version = version, CreatedAt = Jan1, EligibilitySnapshot = eligibility
        };
        for (var i = 0; i < components; i++)
        {
            set.SelectedComponents.Add(new ContentSetComponent
            {
                KnowledgeContentId = Guid.NewGuid(), ContentVersion = "1.0", LanguageCode = "en",
                Arrangement = new ContentArrangement { TemplateStepId = Guid.NewGuid(), Position = i }
            });
        }
        repo.Items.Add(set);
        return set;
    }

    // ── submit / freeze ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_freezes_manifest_and_returns_201_submitted()
    {
        var fx = new Fixture();
        var set = SeedSet(fx.Sets, TenantA);

        var r = await fx.Submit("author", TenantA).Handle(new SubmitContentSetForReviewCommand(set.Id), default);

        Assert.Equal(201, r.StatusCode);
        var dto = (await fx.Get(TenantA).Handle(new GetContentSetRevisionByIdQuery(r.Data), default)).Data!;
        Assert.Equal(ContentSetReviewStatuses.Submitted, dto.ReviewStatus);
        Assert.Equal("SET-1-R1", dto.RevisionCode);
        Assert.Equal(1, dto.RevisionNumber);
        Assert.Equal(set.Version, dto.ContentSetVersion);
        Assert.Single(dto.SelectedComponents);
        Assert.Equal("author", dto.SubmittedBy);
        Assert.Null(dto.Decision);
    }

    [Fact]
    public async Task Submit_snapshot_is_immutable_after_the_draft_changes()
    {
        var fx = new Fixture();
        var set = SeedSet(fx.Sets, TenantA, components: 1);
        var r = await fx.Submit("author", TenantA).Handle(new SubmitContentSetForReviewCommand(set.Id), default);

        // Mutate the DRAFT after the freeze — the revision must not see it.
        set.SelectedComponents.Add(new ContentSetComponent { KnowledgeContentId = Guid.NewGuid(), ContentVersion = "9" });
        set.SelectedComponents[0].ContentVersion = "tampered";

        var dto = (await fx.Get(TenantA).Handle(new GetContentSetRevisionByIdQuery(r.Data), default)).Data!;
        Assert.Single(dto.SelectedComponents);
        Assert.Equal("1.0", dto.SelectedComponents[0].ContentVersion);
    }

    [Fact]
    public async Task Submit_is_idempotent_returns_the_open_revision()
    {
        var fx = new Fixture();
        var set = SeedSet(fx.Sets, TenantA);

        var first = await fx.Submit("author", TenantA).Handle(new SubmitContentSetForReviewCommand(set.Id), default);
        var second = await fx.Submit("author", TenantA).Handle(new SubmitContentSetForReviewCommand(set.Id), default);

        Assert.Equal(201, first.StatusCode);
        Assert.Equal(200, second.StatusCode);
        Assert.Equal(first.Data, second.Data);
        Assert.Single(fx.Revisions.Items);
    }

    [Fact]
    public async Task Submit_stale_expected_version_is_409()
    {
        var fx = new Fixture();
        var set = SeedSet(fx.Sets, TenantA, version: 3);

        var r = await fx.Submit("author", TenantA).Handle(new SubmitContentSetForReviewCommand(set.Id, ExpectedVersion: 2), default);

        Assert.Equal(409, r.StatusCode);
        Assert.Empty(fx.Revisions.Items);
    }

    [Fact]
    public async Task Submit_without_components_is_400()
    {
        var fx = new Fixture();
        var set = SeedSet(fx.Sets, TenantA, components: 0);

        var r = await fx.Submit("author", TenantA).Handle(new SubmitContentSetForReviewCommand(set.Id), default);

        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Submit_with_unresolved_eligibility_is_400()
    {
        var fx = new Fixture();
        var eligibility = new ContentSetEligibilitySnapshot
        {
            EvaluatedAtUtc = Jan1,
            Items = new List<ContentSetEligibilityItem>
            {
                new() { ItemKind = "claim", SelectionId = Guid.NewGuid(), ItemId = Guid.NewGuid(), State = "unresolved" }
            }
        };
        var set = SeedSet(fx.Sets, TenantA, eligibility: eligibility);

        var r = await fx.Submit("author", TenantA).Handle(new SubmitContentSetForReviewCommand(set.Id), default);

        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task RevisionNumber_increments_across_versions()
    {
        var fx = new Fixture();
        var set = SeedSet(fx.Sets, TenantA, version: 0);

        var r1 = await fx.Submit("author", TenantA).Handle(new SubmitContentSetForReviewCommand(set.Id), default);
        await fx.Decide("reviewer", TenantA).Handle(new RecordReviewDecisionCommand(r1.Data, "reject"), default);

        set.Version = 1;   // the draft moved on; a fresh submit opens the next revision in the lineage
        var r2Submit = await fx.Submit("author", TenantA).Handle(new SubmitContentSetForReviewCommand(set.Id), default);

        var dto = (await fx.Get(TenantA).Handle(new GetContentSetRevisionByIdQuery(r2Submit.Data), default)).Data!;
        Assert.Equal(2, dto.RevisionNumber);
        Assert.Equal("SET-1-R2", dto.RevisionCode);
        var list = (await fx.List(TenantA).Handle(new ListContentSetRevisionsQuery(set.Id), default)).Data!;
        Assert.Equal(2, list.Total);
        Assert.Equal(2, list.Items[0].RevisionNumber); // newest-first
    }

    // ── review decision / SoD ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Decision_by_another_actor_approves()
    {
        var fx = new Fixture();
        var set = SeedSet(fx.Sets, TenantA);
        var r = await fx.Submit("author", TenantA).Handle(new SubmitContentSetForReviewCommand(set.Id), default);

        var d = await fx.Decide("reviewer", TenantA).Handle(new RecordReviewDecisionCommand(r.Data, "approve", "looks good"), default);

        Assert.True(d.IsSuccessful);
        var dto = (await fx.Get(TenantA).Handle(new GetContentSetRevisionByIdQuery(r.Data), default)).Data!;
        Assert.Equal(ContentSetReviewStatuses.Approved, dto.ReviewStatus);
        Assert.Equal("reviewer", dto.Decision!.ReviewerId);
        Assert.Equal("looks good", dto.Decision!.Reason);
    }

    [Fact]
    public async Task Decision_by_the_author_is_403_separation_of_duties()
    {
        var fx = new Fixture();
        var set = SeedSet(fx.Sets, TenantA);
        var r = await fx.Submit("author", TenantA).Handle(new SubmitContentSetForReviewCommand(set.Id), default);

        var d = await fx.Decide("author", TenantA).Handle(new RecordReviewDecisionCommand(r.Data, "approve"), default);

        Assert.Equal(403, d.StatusCode);
        var dto = (await fx.Get(TenantA).Handle(new GetContentSetRevisionByIdQuery(r.Data), default)).Data!;
        Assert.Equal(ContentSetReviewStatuses.Submitted, dto.ReviewStatus);
    }

    [Fact]
    public async Task Duplicate_identical_decision_returns_existing_and_conflicting_is_409()
    {
        var fx = new Fixture();
        var set = SeedSet(fx.Sets, TenantA);
        var r = await fx.Submit("author", TenantA).Handle(new SubmitContentSetForReviewCommand(set.Id), default);

        await fx.Decide("reviewer", TenantA).Handle(new RecordReviewDecisionCommand(r.Data, "approve"), default);
        var again = await fx.Decide("reviewer", TenantA).Handle(new RecordReviewDecisionCommand(r.Data, "approve"), default);
        var conflict = await fx.Decide("reviewer", TenantA).Handle(new RecordReviewDecisionCommand(r.Data, "reject"), default);

        Assert.True(again.IsSuccessful);           // duplicate → existing outcome
        Assert.Equal(409, conflict.StatusCode); // different decision on a decided revision → conflict
    }

    [Fact]
    public async Task Decision_unknown_verb_is_400()
    {
        var fx = new Fixture();
        var set = SeedSet(fx.Sets, TenantA);
        var r = await fx.Submit("author", TenantA).Handle(new SubmitContentSetForReviewCommand(set.Id), default);

        var d = await fx.Decide("reviewer", TenantA).Handle(new RecordReviewDecisionCommand(r.Data, "maybe"), default);

        Assert.Equal(400, d.StatusCode);
    }

    [Fact]
    public async Task Revision_is_tenant_isolated()
    {
        var fx = new Fixture();
        var set = SeedSet(fx.Sets, TenantA);
        var r = await fx.Submit("author", TenantA).Handle(new SubmitContentSetForReviewCommand(set.Id), default);

        var cross = await fx.Get(TenantB).Handle(new GetContentSetRevisionByIdQuery(r.Data), default);

        Assert.Equal(404, cross.StatusCode);
    }
}
