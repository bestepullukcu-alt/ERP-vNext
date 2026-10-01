using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Application.Common.Artifacts;
using Diten.CrmService.Application.Features.Knowledge.Chain;
using Diten.CrmService.Application.Features.Knowledge.Path;
using Diten.CrmService.Application.Features.Knowledge.Path.Commands;
using Diten.CrmService.Application.Features.Knowledge.Path.Handlers;
using Diten.CrmService.Application.Features.Knowledge.Path.Release;
using Diten.CrmService.Application.Features.Knowledge.Path.Review;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Diten.CrmService.Infrastructure.ContentComposition.Rendering;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// WP-KP-3 — knowledge path render (approved only, idempotent, approval code, branch-first steps + path-language claim
/// text + MLR round, a real %PDF), artifact read (from the revision, other tenant 404), release (every gate code, person
/// SoD, effect: published + frozen, previous version inactive + previous_path_in_use with stage names, idempotent),
/// withdrawal (reason_required, path_in_use 409 with stage names, inactive) and the usage read; the legacy publish
/// endpoint is unchanged. MOD-0023 / FU01 are faked at their seams; the PDF engine is the real PdfSharp renderer.
/// </summary>
public sealed class KnowledgePathReleaseTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid ProductX = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly DateTimeOffset Jan1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private const string Author = "user-author";
    private const string Publisher = "user-publisher";

    // ================================================================ render

    [Fact]
    public async Task Only_an_approved_revision_is_rendered()
    {
        var fx = new Fixture();
        var (id, revision) = await fx.SubmittedAsync();
        Assert.Equal((409, KnowledgePathReleaseErrors.RevisionNotApproved), Code(await fx.Render(id, revision.Id)));
        Assert.Empty(fx.Store.Stored);
    }

    [Fact]
    public async Task Render_produces_the_archive_pdf_with_code_branch_first_steps_claim_text_and_the_mlr_round()
    {
        var fx = new Fixture();
        var (id, revision) = await fx.ApprovedAsync();

        var r = await fx.Render(id, revision.Id);
        Assert.Equal(201, r.StatusCode);
        var stored = Assert.Single(fx.Store.Stored);
        Assert.Equal((revision.Id, id), (stored.OwningItemId, stored.OwningVersionId));
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(stored.Content, 0, 4));
        Assert.Equal("KP-A-v1.0-R1.pdf", stored.FileName);
        var artifact = Assert.Single(fx.Revisions.Items.Single().RenderedArtifacts);
        Assert.Equal((KnowledgePathArtifactKinds.Pdf, fx.Store.NextContentId, "application/pdf", Publisher),
            (artifact.Kind, artifact.ContentId, artifact.MediaType, artifact.RenderedBy));

        var model = fx.Renderer.Last!;
        Assert.Equal("KP-A-v1.0-R1", model.ApprovalCode);
        Assert.Equal(("Almiba", "Türkiye", "Türkçe", "ALMIBA 1 g"), (model.ChainName, model.CountryName, model.LanguageName, model.ProductName));
        Assert.Equal(["Fatigue", "Main"], model.Slots.Select(s => s.BranchLabel));          // branch-first (SortOrder)
        Assert.Equal(["Fatigue step", "Need"], model.Slots.Select(s => s.SlotLabel));
        Assert.Equal(["KC-2", "KC-1"], model.Slots.SelectMany(s => s.Contents).Select(c => c.ContentCode)); // field order
        var claim = Assert.Single(model.Slots[1].Claims);
        Assert.Equal(("CLM-OK", "Levokarnitin enerji metabolizmasını destekler.", "Diyaliz hastalarında", "1.0"),
            (claim.ClaimCode, claim.Text, claim.Qualifier, claim.CountryVersion));
        Assert.Contains(model.Review, e => e.StepName == "Medikal inceleme" && e.Comment == "Mekanizma cümlesi uygun." && e.Actor == "Dr. Ayşe Kaya");
    }

    [Fact]
    public async Task Render_is_idempotent_and_needs_the_mlr_history()
    {
        var fx = new Fixture();
        var (id, revision) = await fx.ApprovedAsync();
        var first = (await fx.Render(id, revision.Id)).Data!;
        var again = await fx.Render(id, revision.Id);
        Assert.Equal((200, first.ContentId), (again.StatusCode, again.Data!.ContentId));
        Assert.Single(fx.Store.Stored);

        var other = new Fixture();
        var (id2, revision2) = await other.ApprovedAsync();
        other.Decisions.History.Clear();       // MOD-0023 unreachable → no archive copy without its approval trail
        Assert.Equal((503, ClaimErrorCodes.WorkflowUnavailable), Code(await other.Render(id2, revision2.Id)));
        Assert.Empty(other.Store.Stored);
    }

    [Fact]
    public async Task The_artifact_is_resolved_from_the_revision_and_another_tenant_gets_404()
    {
        var fx = new Fixture();
        var (id, revision) = await fx.ReleasableAsync();
        var read = await new GetKnowledgePathRevisionArtifactHandler(fx.Tenant, fx.Paths, fx.Revisions, fx.Store)
            .Handle(new GetKnowledgePathRevisionArtifactQuery(id, revision.Id, null), default);
        Assert.True(read.IsSuccessful);
        Assert.Equal(fx.Store.NextContentId, fx.Store.Reads.Single());

        var html = await new GetKnowledgePathRevisionArtifactHandler(fx.Tenant, fx.Paths, fx.Revisions, fx.Store)
            .Handle(new GetKnowledgePathRevisionArtifactQuery(id, revision.Id, "html"), default);
        Assert.Equal(404, html.StatusCode);

        var tenantB = new TenantContext();
        tenantB.SetTenant(TenantB);
        var foreign = await new GetKnowledgePathRevisionArtifactHandler(tenantB, fx.Paths, fx.Revisions, fx.Store)
            .Handle(new GetKnowledgePathRevisionArtifactQuery(id, revision.Id, null), default);
        Assert.Equal(404, foreign.StatusCode);
        Assert.Single(fx.Store.Reads);
    }

    // ================================================================ release gate

    [Fact]
    public async Task Release_refuses_an_unapproved_unrendered_or_superseded_revision()
    {
        var fx = new Fixture();
        var (id, revision) = await fx.SubmittedAsync();
        Assert.Equal((409, KnowledgePathReleaseErrors.RevisionNotApproved), Code(await fx.Release(id, revision.Id)));

        var b = new Fixture();
        var (idB, revB) = await b.ApprovedAsync();
        Assert.Equal((409, KnowledgePathReleaseErrors.ArtifactMissing), Code(await b.Release(idB, revB.Id)));

        var c = new Fixture();
        var (idC, revC) = await c.ReleasableAsync();
        c.Paths.Items.Single(p => p.Id == idC).PathStatus = KnowledgePathStatuses.Draft;   // the path moved on
        Assert.Equal((409, KnowledgePathReleaseErrors.RevisionSuperseded), Code(await c.Release(idC, revC.Id)));
    }

    [Fact]
    public async Task The_submitter_cannot_release_person_based_sod()
    {
        var fx = new Fixture();
        var (id, revision) = await fx.ReleasableAsync();
        fx.Actor.Name = Author.ToUpperInvariant();
        Assert.Equal((403, KnowledgePathReleaseErrors.SodSubmitterCannotRelease), Code(await fx.Release(id, revision.Id)));
        Assert.Equal(KnowledgePathStatuses.Approved, fx.Paths.Items.Single(p => p.Id == id).PathStatus);
        Assert.False(KnowledgePathReleaseRules.CanRelease(revision, null));
        Assert.True(KnowledgePathReleaseRules.CanRelease(revision, Publisher));
    }

    [Fact]
    public async Task Release_refuses_unpublished_or_foreign_language_content()
    {
        var fx = new Fixture();
        var (id, revision) = await fx.ReleasableAsync();
        fx.ContentTr.ContentStatus = KnowledgeContentStatuses.Inactive;
        Assert.Equal((409, KnowledgePathReviewErrors.ComponentNotPublished), Code(await fx.Release(id, revision.Id)));

        fx.ContentTr.ContentStatus = KnowledgeContentStatuses.Published;
        revision.Snapshot.Steps[0].ContentLanguage = "en";
        Assert.Equal((409, ChainContextErrors.ComponentLanguageMismatch), Code(await fx.Release(id, revision.Id)));
    }

    [Theory]
    [InlineData("not-approved", "claim_not_approved")]
    [InlineData("no-tr-text", "claim_language_mismatch")]
    [InlineData("no-version", "claim_no_country_version")]
    [InlineData("conformance", "chain_conformance_failed")]
    public async Task Release_refuses_unusable_claims_and_a_broken_chain(string breakIt, string code)
    {
        var fx = new Fixture();
        var (id, revision) = await fx.ReleasableAsync();
        switch (breakIt)
        {
            case "not-approved": fx.VersionOk.Status = ClaimStatuses.Draft; break;
            case "no-tr-text": fx.VersionOk.Texts.RemoveAll(t => t.LanguageCode == "tr"); break;
            case "no-version": revision.Snapshot.Claims[0].CountryVersionId = null; break;
            default: revision.Snapshot.Conformance[0].Status = KnowledgePathConformanceStatuses.Under; break;
        }

        Assert.Equal((409, code), Code(await fx.Release(id, revision.Id)));
        Assert.Equal(KnowledgePathStatuses.Approved, fx.Paths.Items.Single(p => p.Id == id).PathStatus);
        Assert.Null(fx.Revisions.Items.Single().ReleaseState);
    }

    // ================================================================ release effect

    [Fact]
    public async Task Release_publishes_and_freezes_the_path_and_retires_the_previous_version_with_a_pinned_stage_warning()
    {
        var fx = new Fixture();
        var (id, revision) = await fx.ReleasableAsync();
        var previous = fx.SeedPublishedPrevious("KP-A", "0.9");
        fx.SeedJourney("JR-HD", "HD yolculuğu", ContentEngagementJourneyStatuses.Published, "ST-1", "İlk ziyaret", previous, pinned: true);

        var r = await fx.Release(id, revision.Id);
        Assert.True(r.IsSuccessful, string.Join("; ", r.Errors ?? new List<string>()));
        var path = fx.Paths.Items.Single(p => p.Id == id);
        Assert.Equal((KnowledgePathStatuses.Published, Publisher), (path.PathStatus, path.PublishedBy));
        Assert.NotNull(path.StepSetFrozenAt);
        Assert.Equal(KnowledgePathStatuses.Inactive, previous.PathStatus);
        Assert.Equal(KnowledgePathReleaseStates.Released, fx.Revisions.Items.Single().ReleaseState!.State);
        Assert.Equal([KnowledgePathReleaseErrors.PreviousPathInUse], r.Data!.Warnings);
        var stage = Assert.Single(r.Data.PreviousPathInUse);
        Assert.Equal(("HD yolculuğu", "İlk ziyaret"), (stage.JourneyName, stage.StageName));

        // Idempotent: a second call answers the current state, nothing changes.
        var version = path.Version;
        var again = await fx.Release(id, revision.Id);
        Assert.Equal((200, KnowledgePathReleaseStates.Released), (again.StatusCode, again.Data!.State));
        Assert.Equal(version, path.Version);

        // The frozen path takes no step change (V-S02) and no more claims.
        Assert.Equal(409, (await fx.AddStep(id, fx.ContentTr.Id, "S-X")).StatusCode);
    }

    [Fact]
    public async Task A_following_latest_published_stage_is_not_a_previous_version_warning()
    {
        var fx = new Fixture();
        var (id, revision) = await fx.ReleasableAsync();
        var previous = fx.SeedPublishedPrevious("KP-A", "0.9");
        fx.SeedJourney("JR-L", "Takip eden", ContentEngagementJourneyStatuses.Published, "ST-1", "Aşama", previous, pinned: false);

        var r = await fx.Release(id, revision.Id);
        Assert.Empty(r.Data!.Warnings);
        Assert.Equal(KnowledgePathStatuses.Inactive, previous.PathStatus);
    }

    // ================================================================ withdrawal + usage

    [Fact]
    public async Task Withdrawal_needs_a_reason_and_a_released_revision()
    {
        var fx = new Fixture();
        var (id, revision) = await fx.ReleasableAsync();
        Assert.Equal((400, KnowledgePathReleaseErrors.ReasonRequired), Code(await fx.Withdraw(id, revision.Id, "  ")));
        Assert.Equal((409, KnowledgePathReleaseErrors.RevisionNotReleased), Code(await fx.Withdraw(id, revision.Id, "Etiket değişti")));
    }

    [Fact]
    public async Task A_path_used_by_a_published_journey_cannot_be_withdrawn_and_nothing_changes()
    {
        var fx = new Fixture();
        var (id, revision) = await fx.ReleasableAsync();
        await fx.Release(id, revision.Id);
        var path = fx.Paths.Items.Single(p => p.Id == id);
        fx.SeedJourney("JR-HD", "HD yolculuğu", ContentEngagementJourneyStatuses.Published, "ST-2", "Hatırlatma", path, pinned: true);

        var r = await fx.Withdraw(id, revision.Id, "Etiket değişti");
        Assert.Equal((409, KnowledgePathReleaseErrors.PathInUse), Code(r));
        Assert.Contains("HD yolculuğu · Hatırlatma", r.Errors!);
        Assert.Equal(KnowledgePathStatuses.Published, path.PathStatus);
        Assert.Equal(KnowledgePathReleaseStates.Released, fx.Revisions.Items.Single().ReleaseState!.State);
    }

    [Fact]
    public async Task An_unused_path_is_withdrawn_inactive_with_the_reason()
    {
        var fx = new Fixture();
        var (id, revision) = await fx.ReleasableAsync();
        await fx.Release(id, revision.Id);
        var path = fx.Paths.Items.Single(p => p.Id == id);
        fx.SeedJourney("JR-D", "Taslak yolculuk", ContentEngagementJourneyStatuses.Draft, "ST-1", "Aşama", path, pinned: true);

        var r = await fx.Withdraw(id, revision.Id, "Etiket değişti");
        Assert.True(r.IsSuccessful);
        Assert.Equal(KnowledgePathStatuses.Inactive, path.PathStatus);
        var state = fx.Revisions.Items.Single().ReleaseState!;
        Assert.Equal((KnowledgePathReleaseStates.Withdrawn, "Etiket değişti", Publisher), (state.State, state.Reason, state.By));
    }

    [Fact]
    public async Task Usage_lists_journey_stages_and_strategy_templates()
    {
        var fx = new Fixture();
        var (id, _) = await fx.ReleasableAsync();
        var path = fx.Paths.Items.Single(p => p.Id == id);
        fx.SeedJourney("JR-A", "Sabit", ContentEngagementJourneyStatuses.Published, "ST-1", "Giriş", path, pinned: true);
        fx.SeedJourney("JR-B", "Takip", ContentEngagementJourneyStatuses.Draft, "ST-9", "Son", path, pinned: false);
        fx.Strategies.Items.Add(new global::Diten.CrmService.Domain.Entities.StrategyTemplate
        {
            TenantId = TenantA, TemplateCode = "ST-ALM", TemplateName = "Almiba play", TemplateStatus = "active",
            ContentBindings = { new StrategyTemplateContentBinding { ContentRefType = StrategyContentRefTypes.KnowledgePath, ContentRefId = id } }
        });
        fx.Strategies.Items.Add(new global::Diten.CrmService.Domain.Entities.StrategyTemplate { TenantId = TenantA, TemplateCode = "ST-OTHER", TemplateName = "x" });

        var usage = (await new GetKnowledgePathUsageHandler(fx.Tenant, fx.Paths, fx.Journeys, fx.Strategies)
            .Handle(new GetKnowledgePathUsageQuery(id), default)).Data!;
        Assert.Equal([("JR-A", "Giriş", "pinned", "1.0"), ("JR-B", "Son", "latest-published", (string?)null)],
            usage.Journeys.Select(j => (j.Code, j.StageName, j.PinPolicy, j.PinnedVersion)));
        Assert.Equal("ST-ALM", Assert.Single(usage.StrategyTemplates).Code);
    }

    // ================================================================ unchanged surfaces + class map

    [Fact]
    public async Task The_legacy_publish_endpoint_is_unchanged_and_a_chained_path_still_cannot_be_published_directly()
    {
        var fx = new Fixture();
        var legacy = await fx.LegacyPathAsync();
        Assert.True((await fx.Publish().Handle(new PublishKnowledgePathCommand(legacy), default)).IsSuccessful);

        var (chained, _) = await fx.ReleasableAsync();
        Assert.Equal((409, ClaimErrorCodes.ApprovalViaWorkflowOnly),
            Code(await fx.Publish().Handle(new PublishKnowledgePathCommand(chained), default)));
    }

    [Fact]
    public void Rendered_artifacts_and_the_release_state_round_trip()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var revision = new KnowledgePathRevision
        {
            TenantId = TenantA, PathId = Guid.NewGuid(),
            RenderedArtifacts = { new KnowledgePathRenderedArtifact { Kind = "pdf", ContentId = Guid.NewGuid(), MediaType = "application/pdf", FileName = "a.pdf", RenderedBy = "u", ByteSize = 9 } },
            ReleaseState = new KnowledgePathReleaseState { State = KnowledgePathReleaseStates.Withdrawn, Reason = "r", By = "u", At = Jan1 }
        };
        var doc = revision.ToBsonDocument();
        Assert.Equal(BsonType.String, doc["RenderedArtifacts"][0]["ContentId"].BsonType);
        var back = BsonSerializer.Deserialize<KnowledgePathRevision>(doc);
        Assert.Equal(("a.pdf", "application/pdf", "u", "withdrawn", "r"),
            (back.RenderedArtifacts[0].FileName, back.RenderedArtifacts[0].MediaType, back.RenderedArtifacts[0].RenderedBy,
             back.ReleaseState!.State, back.ReleaseState.Reason));
    }

    // ================================================================ fixture

    private static (int, string) Code<T>(Response<T> r) => (r.StatusCode, r.Errors?.FirstOrDefault() ?? string.Empty);

    private sealed class Fixture
    {
        public TenantContext Tenant { get; } = new();
        public ActorBox Actor { get; } = new() { Name = Author };
        public PathRepo Paths { get; } = new();
        public RevisionRepo Revisions { get; } = new();
        public ContentSetTestSubjects Subjects { get; } = new();
        public ContentSetTestProfiles Profiles { get; } = new();
        public ContentSetTestTemplates Templates { get; } = new();
        public ContentSetTestContents Contents { get; } = new();
        public ContentSetTestClaims Claims { get; } = new();
        public VersionRepo Versions { get; } = new();
        public ContentSetTestCatalog Catalog { get; } = new();
        public TypeRepo Types { get; } = new();
        public JourneyRepo Journeys { get; } = new();
        public StrategyRepo Strategies { get; } = new();
        public FakeClaimWorkflowClient Workflow { get; } = new();
        public HistoryClient Decisions { get; } = new();
        public FakeStore Store { get; } = new();
        public CapturingRenderer Renderer { get; } = new();
        public Guid T1 { get; } = Guid.NewGuid();
        public Guid T2 { get; } = Guid.NewGuid();
        public Subject Subject { get; }
        public ConceptChainTemplate Template { get; }
        public KnowledgeContent ContentTr { get; }
        public KnowledgeContent ContentTr2 { get; }
        public Claim ClaimOk { get; }
        public ClaimCountryVersion VersionOk { get; }

        public Fixture()
        {
            Tenant.SetTenant(TenantA);
            Subject = new Subject
            {
                TenantId = TenantA, SubjectCode = "ALM", SubjectName = "Almiba",
                ExternalReferences = { new KnowledgeExternalReference { SourceSystem = "global-product", ExternalId = ProductX.ToString(), ExternalCode = "ALMIBA", ExternalName = "ALMIBA 1 g", IsPrimary = true } }
            };
            Subjects.Items.Add(Subject);
            Types.Items.Add(new ConceptType { Id = T1, TenantId = TenantA, ConceptTypeName = "Need" });
            Types.Items.Add(new ConceptType { Id = T2, TenantId = TenantA, ConceptTypeName = "Fatigue step" });
            Template = new ConceptChainTemplate
            {
                TenantId = TenantA, ChainCode = "TPL-ALM", ChainName = "Almiba", SubjectId = Subject.Id, ChainVersion = "1.0",
                Status = ConceptChainStatuses.Published, EffectiveFrom = Jan1, OrderedConceptTypes = { T1, T2 },
                Branches =
                {
                    new ConceptChainBranch { BranchCode = "BR1", BranchName = "Main", SortOrder = 1, Steps = { new ConceptChainStep { ConceptTypeId = T1, MinSelection = 1, MaxSelection = 2 } } },
                    new ConceptChainBranch { BranchCode = "BR2", BranchName = "Fatigue", SortOrder = 0, Steps = { new ConceptChainStep { ConceptTypeId = T2, MinSelection = 0 } } }
                }
            };
            Templates.Items.Add(Template);
            ContentTr = Content("KC-1", "3.1");
            ContentTr2 = Content("KC-2", "1.0");
            ClaimOk = new Claim { TenantId = TenantA, ClaimCode = "CLM-OK", ClaimName = "Metabolizma", ClaimText = "t", ClaimVersion = "2.0", ProductId = ProductX, Status = ClaimStatuses.Approved };
            Claims.Items.Add(ClaimOk);
            VersionOk = new ClaimCountryVersion
            {
                TenantId = TenantA, ClaimCode = "CLM-OK", ClaimId = ClaimOk.Id, CountryCode = "TR", CountryVersion = "1.0", Status = ClaimStatuses.Approved,
                Texts = { new ClaimLocalizedText { LanguageCode = "tr", Text = "Levokarnitin enerji metabolizmasını destekler." } },
                Qualifiers = { new ClaimLocalizedText { LanguageCode = "tr", Text = "Diyaliz hastalarında" } }
            };
            Versions.Items.Add(VersionOk);
        }

        private KnowledgeContent Content(string code, string version)
        {
            var c = new KnowledgeContent
            {
                TenantId = TenantA, ContentCode = code, ContentTitle = "Title " + code, ContentType = KnowledgeContentTypes.Presentation,
                ContentStatus = KnowledgeContentStatuses.Published, SubjectId = Subject.Id, LanguageCode = "tr",
                ContentVersion = version, EffectiveFrom = Jan1, Url = "https://x"
            };
            Contents.Items.Add(c);
            return c;
        }

        private ChainContextResolver Resolver() => new(Templates, Subjects, Profiles);
        private KnowledgePathStudioReader Studio() => new(Templates, Resolver(), Claims, Versions, Types);
        public KnowledgePathRevisionOutcomeApplier Applier() => new(Revisions, Paths);
        public PublishKnowledgePathHandler Publish() => new(Tenant, Actor, Paths);

        public Task<Response<KnowledgePathArtifactDto>> Render(Guid id, Guid revisionId)
            => new RenderKnowledgePathRevisionHandler(Tenant, Actor, Paths, Revisions, Contents, Versions, Templates, Types, Profiles,
                Decisions, Renderer, Store).Handle(new RenderKnowledgePathRevisionCommand(id, revisionId), default);

        public Task<Response<KnowledgePathReleaseDto>> Release(Guid id, Guid revisionId)
            => new ReleaseKnowledgePathRevisionHandler(Tenant, Actor, Paths, Revisions, Contents, Claims, Versions, Journeys)
                .Handle(new ReleaseKnowledgePathRevisionCommand(id, revisionId), default);

        public Task<Response<KnowledgePathReleaseDto>> Withdraw(Guid id, Guid revisionId, string? reason)
            => new WithdrawKnowledgePathReleaseHandler(Tenant, Actor, Paths, Revisions, Journeys)
                .Handle(new WithdrawKnowledgePathReleaseCommand(id, revisionId, reason), default);

        public Task<Response<Guid>> AddStep(Guid id, Guid contentId, string code, string branch = "BR1")
            => new AddKnowledgePathStepHandler(Tenant, Actor, Paths, Contents, new NoNodes(), Templates)
                .Handle(new AddKnowledgePathStepCommand(id, 10, code, "Step", "core-message", contentId, true,
                    Arrangement: new KnowledgePathArrangementInput(branch == "BR1" ? T1 : T2, branch, 0)), default);

        public async Task<(Guid, KnowledgePathRevision)> SubmittedAsync()
        {
            Actor.Name = Author;
            var created = await new CreateKnowledgePathHandler(Tenant, Actor, Paths, Subjects, new NoTopics(), Profiles, Templates, Catalog, Resolver())
                .Handle(new CreateKnowledgePathCommand("KP-A", "Almiba detay", Guid.Empty, "Objective", "1.0", Jan1,
                    LanguageCode: "tr", ChainTemplateId: Template.Id, CountryCode: "TR"), default);
            var id = created.Data;
            Assert.Equal(201, (await AddStep(id, ContentTr.Id, "S-1")).StatusCode);
            Assert.Equal(201, (await AddStep(id, ContentTr2.Id, "S-2", "BR2")).StatusCode);
            Assert.True((await new AddKnowledgePathClaimHandler(Tenant, Actor, Paths, Templates, Subjects, Claims)
                .Handle(new AddKnowledgePathClaimCommand(id, ClaimOk.Id, new KnowledgePathArrangementInput(T1, "BR1", 1)), default)).IsSuccessful);
            var submitted = await new SubmitKnowledgePathReviewHandler(Tenant, Actor, Paths, Revisions, Contents, Claims, Studio(), Workflow)
                .Handle(new SubmitKnowledgePathReviewCommand(id), default);
            Assert.Equal(201, submitted.StatusCode);
            var revision = Revisions.Items.Single();
            Decisions.History[revision.ReviewRound.WorkflowInstanceId] =
            [
                new WorkflowHistoryEntry(1, "start", Author, null, null, null, null, "CRM_KNOWLEDGE_PATH_SUBMITTED", Jan1),
                new WorkflowHistoryEntry(2, "approve", "u-med", "Dr. Ayşe Kaya", "medical", "Medikal inceleme", "Mekanizma cümlesi uygun.", "APPROVED", Jan1.AddDays(1))
            ];
            Actor.Name = Publisher;
            return (id, revision);
        }

        public async Task<(Guid, KnowledgePathRevision)> ApprovedAsync()
        {
            var (id, revision) = await SubmittedAsync();
            Assert.Equal(ClaimReviewApplyResult.Applied, await Applier().ApplyAsync(TenantA, revision.Id,
                revision.ReviewRound.WorkflowInstanceId, ClaimReviewOutcomes.Approved, "u-reg", "APPROVED", DateTimeOffset.UtcNow, default));
            Assert.Equal(KnowledgePathStatuses.Approved, Paths.Items.Single(p => p.Id == id).PathStatus);
            return (id, revision);
        }

        public async Task<(Guid, KnowledgePathRevision)> ReleasableAsync()
        {
            var (id, revision) = await ApprovedAsync();
            Assert.Equal(201, (await Render(id, revision.Id)).StatusCode);
            return (id, revision);
        }

        public KnowledgePath SeedPublishedPrevious(string code, string version)
        {
            var p = new KnowledgePath
            {
                TenantId = TenantA, PathCode = code, PathName = "Eski", PathVersion = version, SubjectId = Subject.Id,
                PathStatus = KnowledgePathStatuses.Published, StepSetFrozenAt = Jan1, PublishedAt = Jan1, LanguageCode = "tr", EffectiveFrom = Jan1
            };
            Paths.Items.Add(p);
            return p;
        }

        public void SeedJourney(string code, string name, string status, string stageCode, string stageName, KnowledgePath path, bool pinned)
            => Journeys.Items.Add(new ContentEngagementJourney
            {
                TenantId = TenantA, JourneyCode = code, JourneyName = name, JourneyStatus = status, JourneyVersion = "1.0",
                Stages =
                {
                    new ContentEngagementJourneyStage
                    {
                        StageCode = stageCode, StageName = stageName, RecommendedKnowledgePathId = path.Id, PathCode = path.PathCode,
                        PathVersionPinPolicy = pinned ? ContentEngagementJourneyPathPin.Pinned : ContentEngagementJourneyPathPin.LatestPublished
                    }
                }
            });

        public async Task<Guid> LegacyPathAsync()
        {
            var r = await new CreateKnowledgePathHandler(Tenant, Actor, Paths, Subjects, new NoTopics(), Profiles)
                .Handle(new CreateKnowledgePathCommand("KP-OLD", "Eski", Subject.Id, "Objective", "1.0", Jan1, LanguageCode: "tr"), default);
            Assert.Equal(201, (await new AddKnowledgePathStepHandler(Tenant, Actor, Paths, Contents, new NoNodes(), Templates)
                .Handle(new AddKnowledgePathStepCommand(r.Data, 10, "S10", "Step", "core-message", ContentTr.Id, true), default)).StatusCode);
            return r.Data;
        }
    }

    private sealed class ActorBox : IActorContext
    {
        public string? Name { get; set; }
        public string? ActorName => Name;
    }

    private sealed class CapturingRenderer : IKnowledgePathRevisionRenderer
    {
        private readonly PdfSharpKnowledgePathRevisionRenderer _real = new();
        public KnowledgePathRenderModel? Last { get; private set; }

        public RenderedContent Render(KnowledgePathRenderModel model)
        {
            Last = model;
            return _real.Render(model);
        }
    }

    private sealed class FakeStore : IContentArtifactStore
    {
        public Guid NextContentId { get; } = Guid.NewGuid();
        public List<ContentArtifactStoreRequest> Stored { get; } = new();
        public List<Guid> Reads { get; } = new();

        public Task<ContentArtifactStoreResult> StoreAsync(ContentArtifactStoreRequest request, CancellationToken ct)
        {
            Stored.Add(request);
            return Task.FromResult(new ContentArtifactStoreResult(NextContentId, "sha256:x", request.Content.Length, request.MediaType));
        }

        public Task<ContentArtifactReadResult?> OpenReadAsync(Guid contentId, CancellationToken ct)
        {
            Reads.Add(contentId);
            return Task.FromResult<ContentArtifactReadResult?>(new ContentArtifactReadResult(new MemoryStream([1]), "application/pdf", "a.pdf", 1));
        }
    }

    private sealed class HistoryClient : IWorkflowDecisionClient
    {
        public Dictionary<Guid, IReadOnlyList<WorkflowHistoryEntry>> History { get; } = new();
        public Task<IReadOnlyList<ClaimWorkflowTaskState>?> GetMyTasksAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ClaimWorkflowTaskState>?>([]);
        public Task<WorkflowDecisionResult> DecideTaskAsync(Guid taskId, bool approve, string actorId, string reasonCode, string idempotencyKey, string? comment, CancellationToken ct)
            => Task.FromResult(new WorkflowDecisionResult(ClaimWorkflowCallOutcome.Ok, null));
        public Task<IReadOnlyList<WorkflowHistoryEntry>?> GetInstanceHistoryAsync(Guid workflowInstanceId, CancellationToken ct)
            => Task.FromResult(History.TryGetValue(workflowInstanceId, out var h) ? h : null);
    }

    private sealed class PathRepo : IKnowledgePathRepository
    {
        public List<KnowledgePath> Items { get; } = new();
        public Task<KnowledgePath?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id));
        public Task<IReadOnlyList<KnowledgePath>> ListAsync(Guid t, CancellationToken ct) => Task.FromResult((IReadOnlyList<KnowledgePath>)Items.Where(x => x.TenantId == t).ToList());
        public Task<IReadOnlyList<KnowledgePath>> ListByCodeAsync(Guid t, string code, CancellationToken ct) => Task.FromResult((IReadOnlyList<KnowledgePath>)Items.Where(x => x.TenantId == t && x.PathCode == code).ToList());
        public Task InsertAsync(KnowledgePath e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task<bool> ReplaceAsync(KnowledgePath e, int expectedVersion, CancellationToken ct)
        {
            var stored = Items.FirstOrDefault(x => x.Id == e.Id);
            if (stored is null || stored.Version != expectedVersion) return Task.FromResult(false);
            e.Version = expectedVersion + 1;
            return Task.FromResult(true);
        }
    }

    private sealed class RevisionRepo : IKnowledgePathRevisionRepository
    {
        public List<KnowledgePathRevision> Items { get; } = new();
        public Task<KnowledgePathRevision?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id));
        public Task<IReadOnlyList<KnowledgePathRevision>> ListByPathAsync(Guid t, Guid pathId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<KnowledgePathRevision>)Items.Where(x => x.TenantId == t && x.PathId == pathId).OrderBy(x => x.RevisionNumber).ToList());
        public Task InsertAsync(KnowledgePathRevision e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task<bool> ReplaceAsync(KnowledgePathRevision e, int expectedVersion, CancellationToken ct)
        {
            var stored = Items.FirstOrDefault(x => x.Id == e.Id);
            if (stored is null || stored.Version != expectedVersion) return Task.FromResult(false);
            e.Version = expectedVersion + 1;
            return Task.FromResult(true);
        }
    }

    private sealed class VersionRepo : IClaimCountryVersionRepository
    {
        public List<ClaimCountryVersion> Items { get; } = new();
        public Task<ClaimCountryVersion?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id));
        public Task<IReadOnlyList<ClaimCountryVersion>> ListByClaimCodeAsync(Guid t, string code, CancellationToken ct) => Task.FromResult((IReadOnlyList<ClaimCountryVersion>)Items.Where(x => x.TenantId == t && x.ClaimCode == code).ToList());
        public Task<IReadOnlyList<ClaimCountryVersion>> ListByClaimIdAsync(Guid t, Guid claimId, CancellationToken ct) => Task.FromResult((IReadOnlyList<ClaimCountryVersion>)Items.Where(x => x.TenantId == t && x.ClaimId == claimId).ToList());
        public Task<IReadOnlyList<ClaimCountryVersion>> ListAsync(Guid t, CancellationToken ct) => Task.FromResult((IReadOnlyList<ClaimCountryVersion>)Items.Where(x => x.TenantId == t).ToList());
        public Task InsertAsync(ClaimCountryVersion e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(ClaimCountryVersion e, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class TypeRepo : IConceptTypeRepository
    {
        public List<ConceptType> Items { get; } = new();
        public Task<ConceptType?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id));
        public Task<IReadOnlyList<ConceptType>> ListAsync(Guid t, CancellationToken ct) => Task.FromResult((IReadOnlyList<ConceptType>)Items.Where(x => x.TenantId == t).ToList());
        public Task<IReadOnlyList<ConceptType>> ListBySubjectAsync(Guid t, Guid s, CancellationToken ct) => ListAsync(t, ct);
        public Task<ConceptType?> GetActiveByCodeAsync(Guid t, Guid s, string code, CancellationToken ct) => Task.FromResult<ConceptType?>(null);
        public Task InsertAsync(ConceptType e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(ConceptType e, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class JourneyRepo : IContentEngagementJourneyRepository
    {
        public List<ContentEngagementJourney> Items { get; } = new();
        public Task<ContentEngagementJourney?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id));
        public Task<IReadOnlyList<ContentEngagementJourney>> ListAsync(Guid t, CancellationToken ct) => Task.FromResult((IReadOnlyList<ContentEngagementJourney>)Items.Where(x => x.TenantId == t).ToList());
        public Task<IReadOnlyList<ContentEngagementJourney>> ListByCodeAsync(Guid t, string code, CancellationToken ct) => Task.FromResult((IReadOnlyList<ContentEngagementJourney>)Items.Where(x => x.TenantId == t && x.JourneyCode == code).ToList());
        public Task InsertAsync(ContentEngagementJourney e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task<bool> ReplaceAsync(ContentEngagementJourney e, int expectedVersion, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class StrategyRepo : IStrategyTemplateRepository
    {
        public List<global::Diten.CrmService.Domain.Entities.StrategyTemplate> Items { get; } = new();
        public Task<global::Diten.CrmService.Domain.Entities.StrategyTemplate?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id));
        public Task<IReadOnlyList<global::Diten.CrmService.Domain.Entities.StrategyTemplate>> ListAsync(Guid t, CancellationToken ct) => Task.FromResult((IReadOnlyList<global::Diten.CrmService.Domain.Entities.StrategyTemplate>)Items.Where(x => x.TenantId == t).ToList());
        public Task<IReadOnlyList<global::Diten.CrmService.Domain.Entities.StrategyTemplate>> ListByLineageAsync(Guid t, Guid lineageId, CancellationToken ct) => ListAsync(t, ct);
        public Task<IReadOnlyList<global::Diten.CrmService.Domain.Entities.StrategyTemplate>> ListByCodeAsync(Guid t, string code, CancellationToken ct) => ListAsync(t, ct);
        public Task InsertAsync(global::Diten.CrmService.Domain.Entities.StrategyTemplate e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task<bool> ReplaceAsync(global::Diten.CrmService.Domain.Entities.StrategyTemplate e, int expectedVersion, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class NoNodes : IConceptNodeRepository
    {
        public Task<ConceptNode?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult<ConceptNode?>(null);
        public Task<IReadOnlyList<ConceptNode>> ListAsync(Guid t, CancellationToken ct) => Task.FromResult((IReadOnlyList<ConceptNode>)Array.Empty<ConceptNode>());
        public Task<IReadOnlyList<ConceptNode>> ListBySubjectAsync(Guid t, Guid s, CancellationToken ct) => ListAsync(t, ct);
        public Task<ConceptNode?> GetActiveByCodeAsync(Guid t, Guid s, Guid ty, string code, CancellationToken ct) => Task.FromResult<ConceptNode?>(null);
        public Task InsertAsync(ConceptNode e, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(ConceptNode e, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class NoTopics : ITopicRepository
    {
        public Task<Topic?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult<Topic?>(null);
        public Task<IReadOnlyList<Topic>> ListAsync(Guid t, CancellationToken ct) => Task.FromResult((IReadOnlyList<Topic>)Array.Empty<Topic>());
        public Task<IReadOnlyList<Topic>> ListBySubjectAsync(Guid t, Guid s, CancellationToken ct) => ListAsync(t, ct);
        public Task<Topic?> GetActiveByCodeAsync(Guid t, Guid s, string code, CancellationToken ct) => Task.FromResult<Topic?>(null);
        public Task InsertAsync(Topic e, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(Topic e, CancellationToken ct) => Task.CompletedTask;
    }
}
