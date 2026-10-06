using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ContentComposition;
using Diten.CrmService.Application.Features.ContentComposition.ContentScopes;
using Diten.CrmService.Application.Features.ContentComposition.ContentSets;
using Diten.CrmService.Application.Features.ContentComposition.Eligibility;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// SCMM-14 (CAND-CAP-0011) — ContentScope + ContentSet (assembly draft). Pins down: scope CRUD + duplicate-code + status
/// guard + audit; content-set create-from-template with version PIN; clone-to-draft (new id, draft, no inherited
/// eligibility snapshot, remapped selection ids); component/claim arrange against the pinned template
/// (branch/cardinality → 400/409); version-pin holds when the source changes later; apply-eligibility writes a per-claim
/// snapshot, is non-blocking on Blocked/Unresolved, and PROPAGATES an infrastructure failure of the port (fail-closed);
/// duplicate set code 409; audit.
/// </summary>
public sealed class ContentSetAssemblyTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset Jan1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static TenantContext Tenant(Guid id)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id);
        return ctx;
    }

    private sealed class Fixture
    {
        public FakeScopeRepo Scopes { get; } = new();
        public FakeSetRepo Sets { get; } = new();
        public FakeTemplateRepo Templates { get; } = new();
        public FakeContentRepo Contents { get; } = new();
        public FakeClaimRepo Claims { get; } = new();
        public FakePort Port { get; } = new();
        public CapturingAudit Audit { get; } = new();

        // ContentScope
        public CreateContentScopeHandler CreateScope() => new(Tenant(TenantA), new NullActorContext(), Scopes, Audit);
        public UpdateContentScopeHandler UpdateScope() => new(Tenant(TenantA), new NullActorContext(), Scopes, Audit);
        public ArchiveContentScopeHandler ArchiveScope() => new(Tenant(TenantA), new NullActorContext(), Scopes, Audit);
        public GetContentScopeHandler GetScope() => new(Tenant(TenantA), Scopes);

        // ContentSet
        public CreateContentSetDraftHandler CreateSet() => new(Tenant(TenantA), new NullActorContext(), Sets, Templates, Scopes, Audit);
        public CloneContentSetToDraftHandler CloneSet() => new(Tenant(TenantA), new NullActorContext(), Sets, Audit);
        public UpdateContentSetHandler UpdateSet() => new(Tenant(TenantA), new NullActorContext(), Sets, Audit);
        public ArchiveContentSetHandler ArchiveSet() => new(Tenant(TenantA), new NullActorContext(), Sets, Audit);
        public GetContentSetHandler GetSet() => new(Tenant(TenantA), Sets);
        public AddContentSetComponentHandler AddComponent() => new(Tenant(TenantA), new NullActorContext(), Sets, Templates, Contents, Audit);
        public RemoveContentSetComponentHandler RemoveComponent() => new(Tenant(TenantA), new NullActorContext(), Sets, Audit);
        public ArrangeContentSetComponentHandler ArrangeComponent() => new(Tenant(TenantA), new NullActorContext(), Sets, Templates, Audit);
        public AddContentSetClaimHandler AddClaim() => new(Tenant(TenantA), new NullActorContext(), Sets, Templates, Claims, Audit);
        public ApplyContentSetEligibilityHandler ApplyEligibility() => new(Tenant(TenantA), new NullActorContext(), Sets, Scopes, Claims, Port, Audit);
    }

    // Seeds a branch-mode template: branch "B1" with one step (ConceptType T1) capped at MaxSelection=1.
    private static ConceptChainTemplate SeedBranchTemplate(FakeTemplateRepo repo, Guid stepType, string version = "1.0")
    {
        var t = new ConceptChainTemplate
        {
            TenantId = TenantA, ChainCode = "CH-1", ChainName = "Chain", SubjectId = Guid.NewGuid(),
            OrderedConceptTypes = new List<Guid> { stepType, Guid.NewGuid() },
            Branches = new List<ConceptChainBranch>
            {
                new()
                {
                    BranchCode = "B1", BranchName = "Branch 1", SortOrder = 0,
                    Steps = new List<ConceptChainStep> { new() { ConceptTypeId = stepType, MinSelection = 1, MaxSelection = 1 } }
                }
            },
            Status = ConceptChainStatuses.Published, ChainVersion = version, EffectiveFrom = Jan1, CreatedAt = Jan1
        };
        repo.Items.Add(t);
        return t;
    }

    private static KnowledgeContent SeedContent(FakeContentRepo repo, string code = "KC-1", string version = "1.0", string lang = "en")
    {
        var c = new KnowledgeContent
        {
            TenantId = TenantA, ContentCode = code, ContentTitle = "Content", ContentType = KnowledgeContentTypes.Presentation,
            ContentStatus = KnowledgeContentStatuses.Published, SubjectId = Guid.NewGuid(), LanguageCode = lang,
            ContentVersion = version, EffectiveFrom = Jan1, Url = "https://x", CreatedAt = Jan1
        };
        c.ContentSetId = c.Id; c.IsSourceLanguage = true;
        repo.Items.Add(c);
        return c;
    }

    private static Claim SeedClaim(FakeClaimRepo repo, string code = "CLM-1", string version = "1.0", Guid? policyId = null)
    {
        var c = new Claim
        {
            TenantId = TenantA, ClaimCode = code, ClaimName = "Claim", ClaimText = "text",
            ClaimVersion = version, Status = ClaimStatuses.Draft, EffectiveFrom = Jan1, CreatedAt = Jan1,
            Applicability = new ClaimApplicability { EligibilityPolicyId = policyId }
        };
        repo.Items.Add(c);
        return c;
    }

    // ---------------- ContentScope ----------------

    [Fact]
    public async Task Create_scope_returns_201_and_round_trips()
    {
        var fx = new Fixture();
        var r = await fx.CreateScope().Handle(new CreateContentScopeCommand(
            "SC-1", "Cardiology EU", ProductRefs: new[] { "p1" }, MarketRefs: new[] { "eu" }, Status: ContentScopeStatuses.Active), default);
        Assert.Equal(201, r.StatusCode);
        var dto = (await fx.GetScope().Handle(new GetContentScopeQuery(r.Data), default)).Data!;
        Assert.Equal("SC-1", dto.ScopeCode);
        Assert.Equal(new[] { "p1" }, dto.ProductRefs.ToArray());
        Assert.Equal("1.0", dto.ScopeVersion);
    }

    [Fact]
    public async Task Duplicate_scope_code_returns_409()
    {
        var fx = new Fixture();
        Assert.Equal(201, (await fx.CreateScope().Handle(new CreateContentScopeCommand("SC-DUP", "A"), default)).StatusCode);
        Assert.Equal(409, (await fx.CreateScope().Handle(new CreateContentScopeCommand("SC-DUP", "B"), default)).StatusCode);
    }

    [Fact]
    public async Task Create_scope_with_archived_status_returns_400()
    {
        var fx = new Fixture();
        var r = await fx.CreateScope().Handle(new CreateContentScopeCommand("SC-X", "X", Status: "archived"), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Archived_scope_update_returns_409_and_archive_is_idempotent()
    {
        var fx = new Fixture();
        var id = (await fx.CreateScope().Handle(new CreateContentScopeCommand("SC-A", "A"), default)).Data;
        Assert.Equal(200, (await fx.ArchiveScope().Handle(new ArchiveContentScopeCommand(id), default)).StatusCode);
        Assert.Equal(200, (await fx.ArchiveScope().Handle(new ArchiveContentScopeCommand(id), default)).StatusCode);
        var upd = await fx.UpdateScope().Handle(new UpdateContentScopeCommand(id, "A2"), default);
        Assert.Equal(409, upd.StatusCode);
    }

    [Fact]
    public async Task Scope_create_emits_audit()
    {
        var fx = new Fixture();
        var r = await fx.CreateScope().Handle(new CreateContentScopeCommand("SC-AUD", "A"), default);
        Assert.Contains(fx.Audit.Events, e => e.Event == ContentScopeReasonCodes.Created
            && e.EntityType == ContentCompositionAuditEntities.ContentScope && e.EntityId == r.Data);
    }

    // ---------------- ContentSet create / pin / clone ----------------

    [Fact]
    public async Task Create_set_pins_template_version()
    {
        var fx = new Fixture();
        var step = Guid.NewGuid();
        var template = SeedBranchTemplate(fx.Templates, step, version: "3.2");

        var r = await fx.CreateSet().Handle(new CreateContentSetDraftCommand("SET-1", "Set", template.Id), default);
        Assert.Equal(201, r.StatusCode);

        // The source template's version changes AFTER selection — the set keeps the pinned version (D14-d).
        template.ChainVersion = "9.9";
        var dto = (await fx.GetSet().Handle(new GetContentSetQuery(r.Data), default)).Data!;
        Assert.Equal(template.Id, dto.Template.ConceptChainTemplateId);
        Assert.Equal("3.2", dto.Template.ChainVersion);
        Assert.Equal(ContentSetStatuses.Draft, dto.Status);
    }

    [Fact]
    public async Task Create_set_with_scope_pins_scope_version()
    {
        var fx = new Fixture();
        var step = Guid.NewGuid();
        var template = SeedBranchTemplate(fx.Templates, step);
        var scopeId = (await fx.CreateScope().Handle(new CreateContentScopeCommand("SC-1", "S", ScopeVersion: "2.0"), default)).Data;

        var r = await fx.CreateSet().Handle(new CreateContentSetDraftCommand("SET-S", "Set", template.Id, ContentScopeId: scopeId), default);
        var dto = (await fx.GetSet().Handle(new GetContentSetQuery(r.Data), default)).Data!;
        Assert.NotNull(dto.Scope);
        Assert.Equal(scopeId, dto.Scope!.ContentScopeId);
        Assert.Equal("2.0", dto.Scope.ScopeVersion);
    }

    [Fact]
    public async Task Create_set_unknown_template_returns_400()
    {
        var fx = new Fixture();
        var r = await fx.CreateSet().Handle(new CreateContentSetDraftCommand("SET-NT", "Set", Guid.NewGuid()), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Duplicate_set_code_returns_409()
    {
        var fx = new Fixture();
        var template = SeedBranchTemplate(fx.Templates, Guid.NewGuid());
        Assert.Equal(201, (await fx.CreateSet().Handle(new CreateContentSetDraftCommand("SET-DUP", "A", template.Id), default)).StatusCode);
        Assert.Equal(409, (await fx.CreateSet().Handle(new CreateContentSetDraftCommand("SET-DUP", "B", template.Id), default)).StatusCode);
    }

    [Fact]
    public async Task Clone_to_draft_is_a_fresh_draft_with_no_inherited_snapshot()
    {
        var fx = new Fixture();
        var step = Guid.NewGuid();
        var template = SeedBranchTemplate(fx.Templates, step);
        var content = SeedContent(fx.Contents);
        var srcId = (await fx.CreateSet().Handle(new CreateContentSetDraftCommand("SET-SRC", "Src", template.Id), default)).Data;
        Assert.Equal(201, (await fx.AddComponent().Handle(new AddContentSetComponentCommand(srcId, content.Id, step, 0, "B1"), default)).StatusCode);
        Assert.Equal(200, (await fx.ApplyEligibility().Handle(new ApplyContentSetEligibilityCommand(srcId), default)).StatusCode);

        var source = fx.Sets.Items.Single(s => s.Id == srcId);
        Assert.NotNull(source.EligibilitySnapshot);   // source has a snapshot

        var cloneId = (await fx.CloneSet().Handle(new CloneContentSetToDraftCommand(srcId, "SET-CLONE"), default)).Data;
        var clone = fx.Sets.Items.Single(s => s.Id == cloneId);

        Assert.NotEqual(srcId, cloneId);
        Assert.Equal(ContentSetStatuses.Draft, clone.Status);
        Assert.Null(clone.EligibilitySnapshot);        // NO inherited approval / validation
        Assert.Single(clone.SelectedComponents);
        // Selection identity is fresh on the clone.
        Assert.NotEqual(source.SelectedComponents[0].SelectionId, clone.SelectedComponents[0].SelectionId);
        // Pinned versions travel with the clone.
        Assert.Equal(source.Template.ChainVersion, clone.Template.ChainVersion);
    }

    // ---------------- ContentSet arrange / pin / cardinality ----------------

    [Fact]
    public async Task Add_component_pins_version_and_language()
    {
        var fx = new Fixture();
        var step = Guid.NewGuid();
        var template = SeedBranchTemplate(fx.Templates, step);
        var content = SeedContent(fx.Contents, version: "1.0", lang: "en");
        var setId = (await fx.CreateSet().Handle(new CreateContentSetDraftCommand("SET-1", "Set", template.Id), default)).Data;

        var add = await fx.AddComponent().Handle(new AddContentSetComponentCommand(setId, content.Id, step, 0, "B1", "hero"), default);
        Assert.Equal(201, add.StatusCode);

        // The source content's version changes later — the set keeps the pinned version (D14-d).
        content.ContentVersion = "5.0"; content.LanguageCode = "tr";
        var dto = (await fx.GetSet().Handle(new GetContentSetQuery(setId), default)).Data!;
        var sel = Assert.Single(dto.SelectedComponents);
        Assert.Equal("1.0", sel.ContentVersion);
        Assert.Equal("en", sel.LanguageCode);
        Assert.Equal("hero", sel.Role);
        Assert.Equal("B1", sel.Arrangement.BranchId);
    }

    [Fact]
    public async Task Add_component_unknown_branch_returns_400()
    {
        var fx = new Fixture();
        var step = Guid.NewGuid();
        var template = SeedBranchTemplate(fx.Templates, step);
        var content = SeedContent(fx.Contents);
        var setId = (await fx.CreateSet().Handle(new CreateContentSetDraftCommand("SET-1", "Set", template.Id), default)).Data;

        var add = await fx.AddComponent().Handle(new AddContentSetComponentCommand(setId, content.Id, step, 0, "NOPE"), default);
        Assert.Equal(400, add.StatusCode);
    }

    [Fact]
    public async Task Add_component_unknown_step_returns_400()
    {
        var fx = new Fixture();
        var step = Guid.NewGuid();
        var template = SeedBranchTemplate(fx.Templates, step);
        var content = SeedContent(fx.Contents);
        var setId = (await fx.CreateSet().Handle(new CreateContentSetDraftCommand("SET-1", "Set", template.Id), default)).Data;

        var add = await fx.AddComponent().Handle(new AddContentSetComponentCommand(setId, content.Id, Guid.NewGuid(), 0, "B1"), default);
        Assert.Equal(400, add.StatusCode);
    }

    [Fact]
    public async Task Add_component_beyond_max_selection_returns_409()
    {
        var fx = new Fixture();
        var step = Guid.NewGuid();
        var template = SeedBranchTemplate(fx.Templates, step);   // MaxSelection = 1
        var c1 = SeedContent(fx.Contents, "KC-1");
        var c2 = SeedContent(fx.Contents, "KC-2");
        var setId = (await fx.CreateSet().Handle(new CreateContentSetDraftCommand("SET-1", "Set", template.Id), default)).Data;

        Assert.Equal(201, (await fx.AddComponent().Handle(new AddContentSetComponentCommand(setId, c1.Id, step, 0, "B1"), default)).StatusCode);
        var second = await fx.AddComponent().Handle(new AddContentSetComponentCommand(setId, c2.Id, step, 1, "B1"), default);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Remove_component_unknown_selection_returns_404()
    {
        var fx = new Fixture();
        var template = SeedBranchTemplate(fx.Templates, Guid.NewGuid());
        var setId = (await fx.CreateSet().Handle(new CreateContentSetDraftCommand("SET-1", "Set", template.Id), default)).Data;
        var r = await fx.RemoveComponent().Handle(new RemoveContentSetComponentCommand(setId, Guid.NewGuid()), default);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task Add_claim_pins_version()
    {
        var fx = new Fixture();
        var step = Guid.NewGuid();
        var template = SeedBranchTemplate(fx.Templates, step);
        var claim = SeedClaim(fx.Claims, version: "2.1");
        var setId = (await fx.CreateSet().Handle(new CreateContentSetDraftCommand("SET-1", "Set", template.Id), default)).Data;

        var add = await fx.AddClaim().Handle(new AddContentSetClaimCommand(setId, claim.Id, step, 0, "B1"), default);
        Assert.Equal(201, add.StatusCode);
        claim.ClaimVersion = "9.9";
        var dto = (await fx.GetSet().Handle(new GetContentSetQuery(setId), default)).Data!;
        Assert.Equal("2.1", Assert.Single(dto.SelectedClaims).ClaimVersion);
    }

    // ---------------- apply-eligibility ----------------

    [Fact]
    public async Task Apply_eligibility_writes_per_claim_snapshot_and_is_non_blocking()
    {
        var fx = new Fixture();
        var step = Guid.NewGuid();
        var template = SeedBranchTemplate(fx.Templates, step);
        var policyId = Guid.NewGuid();
        var claim = SeedClaim(fx.Claims, policyId: policyId);
        fx.Port.Result = new EligibilityResult(EligibilityState.Blocked, "policy", "blocked-reason", policyId, "1.0",
            Array.Empty<EligibilityConditionOutcome>(), Jan1);

        var setId = (await fx.CreateSet().Handle(new CreateContentSetDraftCommand("SET-1", "Set", template.Id), default)).Data;
        Assert.Equal(201, (await fx.AddClaim().Handle(new AddContentSetClaimCommand(setId, claim.Id, step, 0, "B1"), default)).StatusCode);

        var apply = await fx.ApplyEligibility().Handle(new ApplyContentSetEligibilityCommand(setId), default);
        Assert.Equal(200, apply.StatusCode);   // NON-BLOCKING even though the claim is Blocked

        var set = fx.Sets.Items.Single(s => s.Id == setId);
        var item = Assert.Single(set.EligibilitySnapshot!.Items);
        Assert.Equal("blocked", item.State);
        Assert.Equal(policyId, item.PolicyId);
        Assert.True(fx.Port.Called);
    }

    [Fact]
    public async Task Apply_eligibility_claim_without_policy_is_unresolved()
    {
        var fx = new Fixture();
        var step = Guid.NewGuid();
        var template = SeedBranchTemplate(fx.Templates, step);
        var claim = SeedClaim(fx.Claims, policyId: null);   // no policy anchor
        var setId = (await fx.CreateSet().Handle(new CreateContentSetDraftCommand("SET-1", "Set", template.Id), default)).Data;
        await fx.AddClaim().Handle(new AddContentSetClaimCommand(setId, claim.Id, step, 0, "B1"), default);

        Assert.Equal(200, (await fx.ApplyEligibility().Handle(new ApplyContentSetEligibilityCommand(setId), default)).StatusCode);
        var set = fx.Sets.Items.Single(s => s.Id == setId);
        Assert.Equal("unresolved", Assert.Single(set.EligibilitySnapshot!.Items).State);
        Assert.False(fx.Port.Called);   // no policy → the port was never called
    }

    [Fact]
    public async Task Apply_eligibility_propagates_a_port_infrastructure_failure()
    {
        var fx = new Fixture();
        var step = Guid.NewGuid();
        var template = SeedBranchTemplate(fx.Templates, step);
        var claim = SeedClaim(fx.Claims, policyId: Guid.NewGuid());
        fx.Port.Throw = true;   // fail-closed: an infra failure throws
        var setId = (await fx.CreateSet().Handle(new CreateContentSetDraftCommand("SET-1", "Set", template.Id), default)).Data;
        await fx.AddClaim().Handle(new AddContentSetClaimCommand(setId, claim.Id, step, 0, "B1"), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fx.ApplyEligibility().Handle(new ApplyContentSetEligibilityCommand(setId), default));
    }

    [Fact]
    public async Task Component_on_archived_set_returns_409()
    {
        var fx = new Fixture();
        var step = Guid.NewGuid();
        var template = SeedBranchTemplate(fx.Templates, step);
        var content = SeedContent(fx.Contents);
        var setId = (await fx.CreateSet().Handle(new CreateContentSetDraftCommand("SET-1", "Set", template.Id), default)).Data;
        Assert.Equal(200, (await fx.ArchiveSet().Handle(new ArchiveContentSetCommand(setId), default)).StatusCode);

        var add = await fx.AddComponent().Handle(new AddContentSetComponentCommand(setId, content.Id, step, 0, "B1"), default);
        Assert.Equal(409, add.StatusCode);
    }

    [Fact]
    public async Task Set_create_emits_audit()
    {
        var fx = new Fixture();
        var template = SeedBranchTemplate(fx.Templates, Guid.NewGuid());
        var r = await fx.CreateSet().Handle(new CreateContentSetDraftCommand("SET-AUD", "A", template.Id), default);
        Assert.Contains(fx.Audit.Events, e => e.Event == ContentSetReasonCodes.Created
            && e.EntityType == ContentCompositionAuditEntities.ContentSet && e.EntityId == r.Data);
    }

    // ---------------- fakes ----------------

    private sealed class CapturingAudit : IContentCompositionAuditPublisher
    {
        public List<(string Event, string EntityType, Guid EntityId)> Events { get; } = new();

        public Task PublishAsync(string eventName, Guid tenantId, string entityType, Guid entityId, int version,
            string? detail, CancellationToken cancellationToken)
        {
            Events.Add((eventName, entityType, entityId));
            return Task.CompletedTask;
        }
    }

    private sealed class FakePort : IEligibilityEvaluationPort
    {
        public bool Called { get; private set; }
        public bool Throw { get; set; }
        public EligibilityResult? Result { get; set; }

        public Task<Response<EligibilityResult>> EvaluateAsync(ResolveEligibilityQuery query, CancellationToken ct)
        {
            Called = true;
            if (Throw) { throw new InvalidOperationException("eligibility read failed"); }
            var result = Result ?? new EligibilityResult(EligibilityState.Eligible, null, null, query.PolicyId, "1.0",
                Array.Empty<EligibilityConditionOutcome>(), DateTimeOffset.UtcNow);
            return Task.FromResult(Response<EligibilityResult>.Success(result));
        }
    }

    private sealed class FakeScopeRepo : IContentScopeRepository
    {
        public List<ContentScope> Items { get; } = new();
        public Task<ContentScope?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));
        public Task<IReadOnlyList<ContentScope>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ContentScope>)Items.Where(x => x.TenantId == t && !x.IsDeleted).ToList());
        public Task<ContentScope?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && !x.IsDeleted && x.ScopeCode == code && !x.IsArchived()));
        public Task InsertAsync(ContentScope e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(ContentScope e, CancellationToken ct) => Task.CompletedTask;
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

    private sealed class FakeTemplateRepo : IConceptChainTemplateRepository
    {
        public List<ConceptChainTemplate> Items { get; } = new();
        public Task<ConceptChainTemplate?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));
        public Task<IReadOnlyList<ConceptChainTemplate>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ConceptChainTemplate>)Items.Where(x => x.TenantId == t && !x.IsDeleted).ToList());
        public Task<IReadOnlyList<ConceptChainTemplate>> ListBySubjectAsync(Guid t, Guid subjectId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ConceptChainTemplate>)Items.Where(x => x.TenantId == t && !x.IsDeleted && x.SubjectId == subjectId).ToList());
        public Task<IReadOnlyList<ConceptChainTemplate>> ListByCodeAsync(Guid t, Guid subjectId, string chainCode, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ConceptChainTemplate>)Items.Where(x => x.TenantId == t && !x.IsDeleted && x.SubjectId == subjectId && x.ChainCode == chainCode).ToList());
        public Task InsertAsync(ConceptChainTemplate e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(ConceptChainTemplate e, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeContentRepo : IKnowledgeContentRepository
    {
        public List<KnowledgeContent> Items { get; } = new();
        public Task<KnowledgeContent?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));
        public Task<IReadOnlyList<KnowledgeContent>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<KnowledgeContent>)Items.Where(x => x.TenantId == t && !x.IsDeleted).ToList());
        public Task<KnowledgeContent?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && !x.IsDeleted && x.ContentCode == code && !x.IsArchived()));
        public Task InsertAsync(KnowledgeContent e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(KnowledgeContent e, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeClaimRepo : IClaimRepository
    {
        public List<Claim> Items { get; } = new();
        public Task<Claim?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));
        public Task<IReadOnlyList<Claim>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Claim>)Items.Where(x => x.TenantId == t && !x.IsDeleted).ToList());
        public Task<IReadOnlyList<Claim>> ListByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Claim>)Items.Where(x => x.TenantId == t && !x.IsDeleted && x.ClaimCode == code).ToList());
        public Task<Claim?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && !x.IsDeleted && x.ClaimCode == code && !x.IsArchived()));
        public Task InsertAsync(Claim e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(Claim e, CancellationToken ct) => Task.CompletedTask;
    }
}
