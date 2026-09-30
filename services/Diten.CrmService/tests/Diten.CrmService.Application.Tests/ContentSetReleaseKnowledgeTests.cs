using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ContentComposition;
using Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions;
using Diten.CrmService.Application.Features.Knowledge.Content.Commands;
using Diten.CrmService.Application.Features.Knowledge.Content.Handlers;
using Diten.CrmService.Application.Features.Knowledge.Path.Commands;
using Diten.CrmService.Application.Features.Knowledge.Path.Handlers;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// WP-SB-2 — a content set release produces (A) an assembled-presentation KnowledgeContent and (D) a chain-ordered,
/// published KnowledgePath, through the existing Knowledge commands. Pins: the produced fields and claim refs (country /
/// core), the branch-major step order (user decision 2026-09-29) and the legacy spine order, idempotency, the fail-closed
/// preconditions (claims usable, components published, one language — nothing produced on refusal), compensation when
/// the revision save fails, the withdrawal (inactive / path_in_use), the re-release versioning (new path version +
/// previous inactive; suffixed content code + previous inactive) and the class-map round-trip of the new fields.
/// </summary>
public sealed class ContentSetReleaseKnowledgeTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ProductX = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid T1 = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid T2 = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
    private static readonly Guid T3 = Guid.Parse("00000000-0000-0000-0000-0000000000a3");
    private static readonly DateTimeOffset Jan1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // ================================================================ A + D production

    [Fact]
    public async Task Release_produces_the_assembled_presentation_with_a_country_claim_ref()
    {
        var fx = new Fixture();
        var revision = fx.SeedBranchedRevision();

        var r = await fx.Release().Handle(new ReleaseContentSetRevisionCommand(revision.Id), default);

        Assert.Equal(200, r.StatusCode);
        var content = fx.Contents.Items.Single(c => c.ContentType == KnowledgeContentTypes.AssembledPresentation);
        Assert.Equal("KC-SET-ALM", content.ContentCode);
        Assert.Equal("Almiba seti", content.ContentTitle);
        Assert.Equal(KnowledgeContentStatuses.Published, content.ContentStatus);
        Assert.Equal(fx.SubjectId, content.SubjectId);
        Assert.Equal(ProductX, content.ProductId);                  // the subject's primary MDM Global Product
        Assert.Equal("tr", content.LanguageCode);
        Assert.Equal(fx.AudienceId, content.AudienceProfileId);     // the chain's single ForWhom profile
        Assert.Equal(revision.RenderedArtifact!.ContentId.ToString("D"), content.ContentAssetRef);
        Assert.Equal(KnowledgeContentSources.ContentStudio, content.Source);
        Assert.Equal("1", content.ContentVersion);
        var claimRef = Assert.Single(content.ClaimRefs);
        Assert.Equal("CLM-A", claimRef.ClaimCode);
        Assert.Equal(fx.VersionTr!.Id, claimRef.CountryVersionId);  // the set country (frozen in the revision)
        Assert.Equal("TR", claimRef.CountryCode);
        Assert.NotNull(content.StudioOrigin);
        Assert.Equal(revision.ContentSetId, content.StudioOrigin!.ContentSetId);
        Assert.Equal(revision.Id, content.StudioOrigin.ContentSetRevisionId);
        Assert.Equal(fx.Template.Id, content.StudioOrigin.ConceptChainTemplateId);
        Assert.Equal("2.0", content.StudioOrigin.ChainVersion);

        Assert.Equal(content.Id, revision.ProducedKnowledgeContentId);
        Assert.Equal("KC-SET-ALM", revision.ProducedKnowledgeContentCode);
        Assert.Equal(content.Id, r.Data!.ProducedKnowledgeContentId);
        Assert.Equal("KC-SET-ALM", r.Data.ProducedKnowledgeContentCode);
        Assert.Equal("KP-SET-ALM", r.Data.ProducedKnowledgePathCode);
        Assert.True(revision.IsReleased());

        var dto = ContentSetRevisionMapper.ToDto(revision);
        Assert.Equal(content.Id, dto.ProducedKnowledgeContentId);
        Assert.Equal(revision.ProducedKnowledgePathId, dto.ProducedKnowledgePathId);
        Assert.Equal("KP-SET-ALM", dto.ProducedKnowledgePathCode);
    }

    [Fact]
    public async Task Release_produces_a_published_frozen_path_in_branch_major_order()
    {
        var fx = new Fixture();
        var revision = fx.SeedBranchedRevision();

        await fx.Release().Handle(new ReleaseContentSetRevisionCommand(revision.Id), default);

        var path = Assert.Single(fx.Paths.Items);
        Assert.Equal(revision.ProducedKnowledgePathId, path.Id);
        Assert.Equal("KP-SET-ALM", path.PathCode);
        Assert.Equal("1", path.PathVersion);
        Assert.True(path.IsPublished());
        Assert.True(path.IsStepSetFrozen());
        Assert.Equal(fx.SubjectId, path.SubjectId);
        Assert.Equal("tr", path.LanguageCode);
        Assert.Equal(fx.AudienceId, path.AudienceProfileId);
        Assert.Equal(KnowledgePathSources.ContentStudio, path.Source);
        Assert.Equal(revision.Id, path.StudioOrigin!.ContentSetRevisionId);

        // Branch A (SortOrder 1) before B (SortOrder 2, listed first); inside a branch its Steps list order (A: T2 → T1),
        // then Position — exactly the Studio workspace order. Column-major would put c3/c2/c1 (all T1) together.
        var steps = path.OrderedActiveSteps();
        Assert.Equal(new[] { fx.C4.Id, fx.C3.Id, fx.C2.Id, fx.C1.Id, fx.C5.Id }, steps.Select(s => s.ContentId).ToArray());
        Assert.Equal(new[] { 10, 20, 30, 40, 50 }, steps.Select(s => s.StepOrder).ToArray());
        Assert.All(steps, s => Assert.True(s.IsRequired));
        Assert.Equal(fx.NodeId, steps[0].ConceptNodeId);            // the component content's concept node
        Assert.Null(steps[1].ConceptNodeId);
        Assert.Equal(KnowledgePathStepTypes.Faq, steps[0].StepType);
        Assert.Equal(KnowledgePathStepTypes.CoreMessage, steps[1].StepType);
    }

    [Fact]
    public void Legacy_template_orders_by_the_spine_then_position()
    {
        var template = new ConceptChainTemplate { OrderedConceptTypes = { T3, T1 } };
        var a = Component(T1, null, 1);
        var b = Component(T1, null, 0);
        var c = Component(T3, null, 0);

        var ordered = ContentSetPathOrder.Order(template, new[] { a, b, c });

        Assert.Equal(new[] { c, b, a }, ordered);
    }

    // WP-SB-1R — the release reads the country from the set context (ONE point: ContentSetReleaseProducer.ReleaseContext).
    [Fact]
    public async Task A_set_without_a_country_binds_the_core_claim()
    {
        var fx = new Fixture();
        var revision = fx.SeedBranchedRevision(country: null);

        var r = await fx.Release().Handle(new ReleaseContentSetRevisionCommand(revision.Id), default);

        Assert.Equal(200, r.StatusCode);
        var claimRef = Assert.Single(fx.Produced().ClaimRefs);
        Assert.Equal(fx.ClaimA.Id, claimRef.ClaimId);
        Assert.Null(claimRef.CountryVersionId);
        Assert.Null(claimRef.CountryCode);
    }

    [Fact]
    public async Task The_country_frozen_in_the_revision_wins_over_a_later_change_of_the_set()
    {
        var fx = new Fixture();
        var revision = fx.SeedBranchedRevision(country: "TR");
        fx.Set.CountryCode = "UZ";                                   // the draft moves on after submit

        var r = await fx.Release().Handle(new ReleaseContentSetRevisionCommand(revision.Id), default);

        Assert.Equal(200, r.StatusCode);
        var claimRef = Assert.Single(fx.Produced().ClaimRefs);
        Assert.Equal(fx.VersionTr!.Id, claimRef.CountryVersionId);
        Assert.Equal("TR", claimRef.CountryCode);
    }

    [Fact]
    public async Task A_pre_sb1r_revision_without_a_frozen_context_reads_the_set_country()
    {
        var fx = new Fixture();
        var revision = fx.SeedBranchedRevision(country: "TR", frozenContext: false);

        var r = await fx.Release().Handle(new ReleaseContentSetRevisionCommand(revision.Id), default);

        Assert.Equal(200, r.StatusCode);
        Assert.Equal("TR", Assert.Single(fx.Produced().ClaimRefs).CountryCode);
    }

    [Fact]
    public async Task A_set_language_other_than_the_components_is_409_component_language_mixed()
    {
        var fx = new Fixture();
        var revision = fx.SeedBranchedRevision(setLanguage: "uz");   // components are all "tr"

        var r = await fx.Release().Handle(new ReleaseContentSetRevisionCommand(revision.Id), default);

        Assert.Equal(409, r.StatusCode);
        Assert.Equal(ContentSetReleaseErrors.ComponentLanguageMixed, r.Errors![0]);
        Assert.Contains("set=uz", r.Errors[1]);
        fx.AssertNothingProduced(revision);
    }

    [Fact]
    public async Task Release_is_idempotent_and_never_produces_a_second_pair()
    {
        var fx = new Fixture();
        var revision = fx.SeedBranchedRevision();

        var first = await fx.Release().Handle(new ReleaseContentSetRevisionCommand(revision.Id), default);
        var contentCount = fx.Contents.Items.Count;
        var sent = fx.Sender.Sent.Count;
        var second = await fx.Release().Handle(new ReleaseContentSetRevisionCommand(revision.Id), default);

        Assert.Equal(200, second.StatusCode);
        Assert.Equal(first.Data!.ProducedKnowledgeContentId, second.Data!.ProducedKnowledgeContentId);
        Assert.Equal(first.Data.ProducedKnowledgePathId, second.Data.ProducedKnowledgePathId);
        Assert.Equal(contentCount, fx.Contents.Items.Count);
        Assert.Single(fx.Paths.Items);
        Assert.Equal(sent, fx.Sender.Sent.Count);                 // no command at all on the replay
    }

    // ================================================================ preconditions (fail-closed)

    [Fact]
    public async Task An_unapproved_claim_is_409_and_nothing_is_produced()
    {
        var fx = new Fixture();
        var revision = fx.SeedBranchedRevision(trStatus: ClaimStatuses.InReview);

        var r = await fx.Release().Handle(new ReleaseContentSetRevisionCommand(revision.Id), default);

        Assert.Equal(409, r.StatusCode);
        Assert.Equal(KnowledgeContentClaimErrors.ClaimNotApproved, r.Errors![0]);
        fx.AssertNothingProduced(revision);
    }

    [Fact]
    public async Task A_country_version_without_the_content_language_is_409_language_mismatch()
    {
        var fx = new Fixture();
        var revision = fx.SeedBranchedRevision(trLanguages: new[] { "en" });

        var r = await fx.Release().Handle(new ReleaseContentSetRevisionCommand(revision.Id), default);

        Assert.Equal(409, r.StatusCode);
        Assert.Equal(KnowledgeContentClaimErrors.ClaimLanguageMismatch, r.Errors![0]);
        fx.AssertNothingProduced(revision);
    }

    [Fact]
    public async Task A_component_that_is_not_published_is_409_and_nothing_is_produced()
    {
        var fx = new Fixture();
        var revision = fx.SeedBranchedRevision();
        fx.C2.ContentStatus = KnowledgeContentStatuses.Draft;

        var r = await fx.Release().Handle(new ReleaseContentSetRevisionCommand(revision.Id), default);

        Assert.Equal(409, r.StatusCode);
        Assert.Equal(ContentSetReleaseErrors.ComponentNotPublished, r.Errors![0]);
        Assert.Contains(fx.C2.ContentCode, r.Errors[1]);
        fx.AssertNothingProduced(revision);
    }

    [Fact]
    public async Task A_revision_without_components_is_409()
    {
        var fx = new Fixture();
        var revision = fx.SeedBranchedRevision();
        revision.SelectedComponents.Clear();

        var r = await fx.Release().Handle(new ReleaseContentSetRevisionCommand(revision.Id), default);

        Assert.Equal(409, r.StatusCode);
        Assert.Equal(ContentSetReleaseErrors.ComponentNotPublished, r.Errors![0]);
        fx.AssertNothingProduced(revision);
    }

    [Fact]
    public async Task Components_in_more_than_one_language_are_409_component_language_mixed()
    {
        var fx = new Fixture();
        var revision = fx.SeedBranchedRevision();
        revision.SelectedComponents[1].LanguageCode = "en";

        var r = await fx.Release().Handle(new ReleaseContentSetRevisionCommand(revision.Id), default);

        Assert.Equal(409, r.StatusCode);
        Assert.Equal(ContentSetReleaseErrors.ComponentLanguageMixed, r.Errors![0]);
        Assert.Contains("=en", r.Errors[1]);
        Assert.Contains("=tr", r.Errors[1]);
        fx.AssertNothingProduced(revision);
    }

    [Fact]
    public async Task A_failed_revision_save_compensates_the_outputs_and_leaves_the_revision_unreleased()
    {
        var fx = new Fixture();
        var revision = fx.SeedBranchedRevision();
        fx.Revisions.ThrowOnUpdate = true;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fx.Release().Handle(new ReleaseContentSetRevisionCommand(revision.Id), default));

        Assert.Null(revision.ReleaseState);
        Assert.Null(revision.ProducedKnowledgeContentId);
        Assert.True(fx.Contents.Items.Single(c => c.ContentType == KnowledgeContentTypes.AssembledPresentation).IsArchived());
        Assert.True(Assert.Single(fx.Paths.Items).IsArchived());
    }

    // ================================================================ withdrawal

    [Fact]
    public async Task Withdraw_sets_the_produced_content_and_path_inactive()
    {
        var fx = new Fixture();
        var revision = fx.SeedBranchedRevision();
        await fx.Release().Handle(new ReleaseContentSetRevisionCommand(revision.Id), default);

        var r = await fx.Withdraw().Handle(new WithdrawContentSetRevisionCommand(revision.Id, "recall"), default);

        Assert.Equal(200, r.StatusCode);
        Assert.Empty(r.Data!.Warnings!);
        Assert.Equal(KnowledgeContentStatuses.Inactive, fx.Produced().ContentStatus);
        Assert.Equal(KnowledgePathStatuses.Inactive, Assert.Single(fx.Paths.Items).PathStatus);
        Assert.True(revision.IsWithdrawn());
    }

    [Fact]
    public async Task Withdraw_leaves_a_path_a_published_journey_stage_uses_and_warns_path_in_use()
    {
        var fx = new Fixture();
        var revision = fx.SeedBranchedRevision();
        await fx.Release().Handle(new ReleaseContentSetRevisionCommand(revision.Id), default);
        fx.SeedJourneyOn(fx.Paths.Items.Single());

        var r = await fx.Withdraw().Handle(new WithdrawContentSetRevisionCommand(revision.Id, "recall"), default);

        Assert.Equal(200, r.StatusCode);
        Assert.Contains(ContentSetReleaseWarnings.PathInUse, r.Data!.Warnings!);
        Assert.Equal(KnowledgeContentStatuses.Inactive, fx.Produced().ContentStatus);
        Assert.True(Assert.Single(fx.Paths.Items).IsPublished());   // untouched
    }

    // ================================================================ re-release → new versions

    [Fact]
    public async Task A_later_revision_opens_a_new_path_version_and_supersedes_the_previous_outputs()
    {
        var fx = new Fixture();
        var first = fx.SeedBranchedRevision();
        await fx.Release().Handle(new ReleaseContentSetRevisionCommand(first.Id), default);
        var oldContent = fx.Produced();
        var oldPath = fx.Paths.Items.Single();
        fx.SeedJourneyOn(oldPath);

        var second = fx.SeedBranchedRevision(revisionNumber: 2, reuseComponents: true);
        second.SelectedComponents.RemoveAt(0);                      // the new revision drops one component
        var r = await fx.Release().Handle(new ReleaseContentSetRevisionCommand(second.Id), default);

        Assert.Equal(200, r.StatusCode);
        // content: no version command → a new record under a suffixed code, the previous one inactive
        var newContent = fx.Contents.Items.Single(c => c.Id == second.ProducedKnowledgeContentId);
        Assert.Equal("KC-SET-ALM-R2", newContent.ContentCode);
        Assert.Equal("2", newContent.ContentVersion);
        Assert.Equal(KnowledgeContentStatuses.Published, newContent.ContentStatus);
        Assert.Equal(KnowledgeContentStatuses.Inactive, oldContent.ContentStatus);

        // path: CreateKnowledgePathVersion → same code, SupersedesPathId, previous inactive, new published
        var newPath = fx.Paths.Items.Single(p => p.Id == second.ProducedKnowledgePathId);
        Assert.Equal("KP-SET-ALM", newPath.PathCode);
        Assert.Equal("2", newPath.PathVersion);
        Assert.Equal(oldPath.Id, newPath.SupersedesPathId);
        Assert.True(newPath.IsPublished());
        Assert.Equal(KnowledgePathStatuses.Inactive, oldPath.PathStatus);
        Assert.Equal(4, newPath.OrderedActiveSteps().Count);       // the new revision's steps only (clone's archived)
        Assert.Equal(second.Id, newPath.StudioOrigin!.ContentSetRevisionId);
        Assert.Contains(ContentSetReleaseWarnings.PreviousPathInUse, r.Data!.Warnings!);
    }

    // ================================================================ class map

    [Fact]
    public void New_guid_fields_round_trip_as_strings_and_legacy_documents_read_null()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var origin = new KnowledgeStudioOrigin
        {
            ContentSetId = Guid.NewGuid(), ContentSetRevisionId = Guid.NewGuid(),
            ConceptChainTemplateId = Guid.NewGuid(), ChainVersion = "2.0"
        };

        var revision = new ContentSetRevision
        {
            TenantId = TenantA, ProducedKnowledgeContentId = Guid.NewGuid(), ProducedKnowledgeContentCode = "KC-1",
            ProducedKnowledgePathId = Guid.NewGuid(), ProducedKnowledgePathCode = "KP-1"
        };
        var revisionDoc = revision.ToBsonDocument();
        Assert.Equal(BsonType.String, revisionDoc["ProducedKnowledgeContentId"].BsonType);
        Assert.Equal(BsonType.String, revisionDoc["ProducedKnowledgePathId"].BsonType);
        var readRevision = BsonSerializer.Deserialize<ContentSetRevision>(revisionDoc);
        Assert.Equal(revision.ProducedKnowledgePathId, readRevision.ProducedKnowledgePathId);
        Assert.Equal("KP-1", readRevision.ProducedKnowledgePathCode);

        var content = new KnowledgeContent { TenantId = TenantA, ContentCode = "KC-1", StudioOrigin = origin };
        var contentDoc = content.ToBsonDocument();
        var storedOrigin = contentDoc["StudioOrigin"].AsBsonDocument;
        Assert.Equal(BsonType.String, storedOrigin["ContentSetId"].BsonType);
        Assert.Equal(BsonType.String, storedOrigin["ContentSetRevisionId"].BsonType);
        Assert.Equal(BsonType.String, storedOrigin["ConceptChainTemplateId"].BsonType);
        Assert.Equal(origin.ContentSetRevisionId, BsonSerializer.Deserialize<KnowledgeContent>(contentDoc).StudioOrigin!.ContentSetRevisionId);

        var path = new KnowledgePath { TenantId = TenantA, PathCode = "KP-1", StudioOrigin = origin };
        var pathDoc = path.ToBsonDocument();
        Assert.Equal(BsonType.String, pathDoc["StudioOrigin"]["ConceptChainTemplateId"].BsonType);
        Assert.Equal("2.0", BsonSerializer.Deserialize<KnowledgePath>(pathDoc).StudioOrigin!.ChainVersion);

        // Pre-SB-2 documents lack the elements and read back null.
        contentDoc.Remove("StudioOrigin");
        pathDoc.Remove("StudioOrigin");
        revisionDoc.Remove("ProducedKnowledgeContentId");
        revisionDoc.Remove("ProducedKnowledgePathId");
        Assert.Null(BsonSerializer.Deserialize<KnowledgeContent>(contentDoc).StudioOrigin);
        Assert.Null(BsonSerializer.Deserialize<KnowledgePath>(pathDoc).StudioOrigin);
        Assert.Null(BsonSerializer.Deserialize<ContentSetRevision>(revisionDoc).ProducedKnowledgeContentId);
    }

    // ================================================================ fixture

    private static ContentSetComponent Component(Guid step, string? branch, int position, KnowledgeContent? content = null)
        => new()
        {
            SelectionId = Guid.NewGuid(),
            KnowledgeContentId = content?.Id ?? Guid.NewGuid(),
            ContentVersion = "1.0",
            LanguageCode = content?.LanguageCode ?? "tr",
            Arrangement = new ContentArrangement { TemplateStepId = step, BranchId = branch, Position = position }
        };

    private sealed class Fixture
    {
        public FakeContents Contents { get; } = new();
        public FakePaths Paths { get; } = new();
        public FakeRevisions Revisions { get; } = new();
        public FakeSets Sets { get; } = new();
        public FakeTemplates Templates { get; } = new();
        public FakeSubjects Subjects { get; } = new();
        public FakeProfiles Profiles { get; } = new();
        public FakeNodes Nodes { get; } = new();
        public FakeClaims Claims { get; } = new();
        public FakeVersions Versions { get; } = new();
        public FakeJourneys Journeys { get; } = new();
        public RoutingSender Sender { get; }

        public Guid SubjectId { get; }
        public Guid AudienceId { get; }
        public Guid NodeId { get; }
        public ConceptChainTemplate Template { get; }
        public ContentSet Set { get; }
        public Claim ClaimA { get; }
        public ClaimCountryVersion? VersionTr { get; private set; }
        public KnowledgeContent C1 { get; }
        public KnowledgeContent C2 { get; }
        public KnowledgeContent C3 { get; }
        public KnowledgeContent C4 { get; }
        public KnowledgeContent C5 { get; }

        private readonly List<ContentSetComponent> _components = new();

        public Fixture()
        {
            var subject = new Subject
            {
                TenantId = TenantA, SubjectCode = "ALM", SubjectName = "Almiba", Status = TaxonomyStatuses.Active,
                ExternalReferences =
                {
                    new KnowledgeExternalReference { SourceSystem = "global-product", ExternalId = Guid.NewGuid().ToString(), IsPrimary = false },
                    new KnowledgeExternalReference { SourceSystem = "global-product", ExternalId = ProductX.ToString(), IsPrimary = true }
                }
            };
            Subjects.Items.Add(subject);
            SubjectId = subject.Id;

            var profile = new AudienceProfile { TenantId = TenantA };
            Profiles.Items.Add(profile);
            AudienceId = profile.Id;

            var node = new ConceptNode { TenantId = TenantA };
            Nodes.Items.Add(node);
            NodeId = node.Id;

            // Branch B is listed FIRST but sorts SECOND; branch A walks T2 before T1.
            Template = new ConceptChainTemplate
            {
                TenantId = TenantA, SubjectId = SubjectId, ChainCode = "TPL-ALM", ChainVersion = "2.0",
                OrderedConceptTypes = { T1, T2, T3 },
                ForWhomAudienceProfileIds = { AudienceId },
                Branches =
                {
                    new ConceptChainBranch
                    {
                        BranchCode = "B", SortOrder = 2,
                        Steps = { new ConceptChainStep { ConceptTypeId = T1 }, new ConceptChainStep { ConceptTypeId = T3 } }
                    },
                    new ConceptChainBranch
                    {
                        BranchCode = "A", SortOrder = 1,
                        Steps = { new ConceptChainStep { ConceptTypeId = T2 }, new ConceptChainStep { ConceptTypeId = T1 } }
                    }
                }
            };
            Templates.Items.Add(Template);

            Set = new ContentSet { TenantId = TenantA, SetCode = "SET-ALM", SetName = "Almiba seti", Description = "HD anlatısı" };
            Sets.Items.Add(Set);

            C1 = SeedContent("KC-C1");
            C2 = SeedContent("KC-C2");
            C3 = SeedContent("KC-C3");
            C4 = SeedContent("KC-C4", KnowledgeContentTypes.Faq, NodeId);
            C5 = SeedContent("KC-C5");
            _components.Add(Component(T1, "b", 0, C1));  // branch code matched case-insensitively
            _components.Add(Component(T1, "A", 1, C2));
            _components.Add(Component(T1, "A", 0, C3));
            _components.Add(Component(T2, "A", 0, C4));
            _components.Add(Component(T3, "B", 0, C5));

            ClaimA = new Claim
            {
                TenantId = TenantA, ClaimCode = "CLM-A", ClaimName = "A", ClaimText = "t", ClaimVersion = "1.0",
                Status = ClaimStatuses.Approved, ProductId = ProductX, EffectiveFrom = Jan1
            };
            Claims.Items.Add(ClaimA);

            Sender = new RoutingSender(this);
        }

        private KnowledgeContent SeedContent(string code, string type = KnowledgeContentTypes.Presentation, Guid? node = null)
        {
            var content = new KnowledgeContent
            {
                TenantId = TenantA, ContentCode = code, ContentTitle = "Title " + code, ContentType = type,
                ContentStatus = KnowledgeContentStatuses.Published, SubjectId = SubjectId, LanguageCode = "tr",
                ContentVersion = "1.0", EffectiveFrom = Jan1, Url = "https://example.test/" + code, ConceptNodeId = node
            };
            Contents.Items.Add(content);
            return content;
        }

        public ContentSetRevision SeedBranchedRevision(
            string? country = "TR", string trStatus = ClaimStatuses.Approved, string[]? trLanguages = null,
            int revisionNumber = 1, bool reuseComponents = false, bool frozenContext = true, string? setLanguage = "tr")
        {
            if (VersionTr is null)
            {
                VersionTr = new ClaimCountryVersion
                {
                    TenantId = TenantA, ClaimCode = ClaimA.ClaimCode, ClaimId = ClaimA.Id, CountryCode = "TR",
                    Status = trStatus, AdaptationTypeCode = "verbatim", ValidFrom = Jan1, CountryVersion = "1.0"
                };
                foreach (var language in trLanguages ?? new[] { "tr" })
                {
                    VersionTr.Texts.Add(new ClaimLocalizedText { LanguageCode = language, Text = "x" });
                }

                Versions.Items.Add(VersionTr);
            }

            // WP-SB-1R — the set carries its country + language; the revision freezes them at submit (a pre-SB-1R
            // revision has no frozen context and the release falls back to the set's fields).
            Set.CountryCode = country;
            Set.LanguageCode = setLanguage;

            var revision = new ContentSetRevision
            {
                TenantId = TenantA,
                ContentSetId = Set.Id,
                RevisionNumber = revisionNumber,
                RevisionCode = $"SET-ALM-R{revisionNumber}",
                ReviewStatus = ContentSetReviewStatuses.Approved,
                SubmittedBy = "author",
                SubmittedAt = Jan1,
                Template = new ContentSetTemplateRef { ConceptChainTemplateId = Template.Id, ChainVersion = "2.0" },
                Context = frozenContext
                    ? new ContentSetContextSnapshot { CountryCode = country, LanguageCode = setLanguage }
                    : null,
                SelectedComponents = _components.Select(c => new ContentSetComponent
                {
                    SelectionId = reuseComponents ? Guid.NewGuid() : c.SelectionId,
                    KnowledgeContentId = c.KnowledgeContentId, ContentVersion = c.ContentVersion, LanguageCode = c.LanguageCode,
                    Arrangement = new ContentArrangement
                    {
                        TemplateStepId = c.Arrangement.TemplateStepId, BranchId = c.Arrangement.BranchId,
                        Position = c.Arrangement.Position
                    }
                }).ToList(),
                SelectedClaims = { new ContentSetClaim { ClaimId = ClaimA.Id, ClaimVersion = "1.0" } },
                Decision = new ContentSetReviewDecision { ReviewerId = "reviewer", Decision = "approve", DecidedAt = Jan1 },
                RenderedArtifact = new ContentSetRenderedArtifact
                {
                    ContentId = Guid.NewGuid(), Checksum = "abc", FileName = "set.pdf", RenderedAtUtc = Jan1
                }
            };
            Revisions.Items.Add(revision);
            return revision;
        }

        public void SeedJourneyOn(KnowledgePath path)
        {
            Journeys.Items.Add(new ContentEngagementJourney
            {
                TenantId = TenantA,
                JourneyStatus = ContentEngagementJourneyStatuses.Published,
                Stages =
                {
                    new ContentEngagementJourneyStage
                    {
                        StageCode = "S1", RecommendedKnowledgePathId = path.Id, PathCode = path.PathCode, IsRequired = true
                    }
                }
            });
        }

        public KnowledgeContent Produced()
            => Contents.Items.Where(c => c.ContentType == KnowledgeContentTypes.AssembledPresentation)
                .OrderByDescending(c => c.ContentVersion).First();

        public void AssertNothingProduced(ContentSetRevision revision)
        {
            Assert.DoesNotContain(Contents.Items, c => c.ContentType == KnowledgeContentTypes.AssembledPresentation);
            Assert.Empty(Paths.Items);
            Assert.Empty(Sender.Sent);
            Assert.Null(revision.ReleaseState);
            Assert.Null(revision.ProducedKnowledgeContentId);
            Assert.Null(revision.ProducedKnowledgePathId);
        }

        private static TenantContext Tenant()
        {
            var ctx = new TenantContext();
            ctx.SetTenant(TenantA);
            return ctx;
        }

        public ContentSetReleaseProducer Producer() => new(
            Sender, Sets, Revisions, Templates, Subjects, Contents, Paths, Journeys, Claims, Versions);

        public ReleaseContentSetRevisionHandler Release()
            => new(Tenant(), new Actor("releaser"), Revisions, new NullAudit(), Producer());

        public WithdrawContentSetRevisionHandler Withdraw()
            => new(Tenant(), new Actor("admin"), Revisions, new NullAudit(), Producer());

        // The real Knowledge handlers — the release must pass their validation and versioning rules unchanged.
        public object Handle(object command, CancellationToken ct)
        {
            var tenant = Tenant();
            var actor = new Actor("releaser");
            var topics = new NoTopics();
            return command switch
            {
                CreateKnowledgeContentCommand c => new CreateKnowledgeContentHandler(
                    tenant, actor, Contents, Subjects, topics, Profiles, Nodes, Claims, Versions).Handle(c, ct),
                UpdateKnowledgeContentCommand c => new UpdateKnowledgeContentHandler(
                    tenant, actor, Contents, Subjects, topics, Profiles, Nodes, null, Claims, Versions).Handle(c, ct),
                ArchiveKnowledgeContentCommand c => new ArchiveKnowledgeContentHandler(tenant, actor, Contents).Handle(c, ct),
                CreateKnowledgePathCommand c => new CreateKnowledgePathHandler(
                    tenant, actor, Paths, Subjects, topics, Profiles).Handle(c, ct),
                UpdateKnowledgePathCommand c => new UpdateKnowledgePathHandler(
                    tenant, actor, Paths, Subjects, topics, Profiles).Handle(c, ct),
                PublishKnowledgePathCommand c => new PublishKnowledgePathHandler(tenant, actor, Paths).Handle(c, ct),
                CreateKnowledgePathVersionCommand c => new CreateKnowledgePathVersionHandler(tenant, actor, Paths).Handle(c, ct),
                ArchiveKnowledgePathCommand c => new ArchiveKnowledgePathHandler(tenant, actor, Paths).Handle(c, ct),
                AddKnowledgePathStepCommand c => new AddKnowledgePathStepHandler(tenant, actor, Paths, Contents, Nodes).Handle(c, ct),
                ArchiveKnowledgePathStepCommand c => new ArchiveKnowledgePathStepHandler(tenant, actor, Paths).Handle(c, ct),
                _ => throw new NotSupportedException(command.GetType().Name)
            };
        }
    }

    private sealed class RoutingSender : ISender
    {
        private readonly Fixture _fx;
        public RoutingSender(Fixture fx) => _fx = fx;
        public List<string> Sent { get; } = new();

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            Sent.Add(request.GetType().Name);
            var task = (Task<TResponse>)_fx.Handle(request, ct);
            return await task;
        }

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class Actor : IActorContext
    {
        public Actor(string? name) => ActorName = name;
        public string? ActorName { get; }
    }

    private sealed class NullAudit : IContentCompositionAuditPublisher
    {
        public Task PublishAsync(string e, Guid t, string et, Guid id, int v, string? d, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeContents : IKnowledgeContentRepository
    {
        public List<KnowledgeContent> Items { get; } = new();
        public Task<KnowledgeContent?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.Id == id));
        public Task<IReadOnlyList<KnowledgeContent>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<KnowledgeContent>)Items.Where(c => c.TenantId == t).ToList());
        public Task<KnowledgeContent?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.ContentCode == code && !c.IsArchived()));
        public Task InsertAsync(KnowledgeContent content, CancellationToken ct) { Items.Add(content); return Task.CompletedTask; }
        public Task UpdateAsync(KnowledgeContent content, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakePaths : IKnowledgePathRepository
    {
        public List<KnowledgePath> Items { get; } = new();
        public Task<KnowledgePath?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(p => p.TenantId == t && p.Id == id));
        public Task<IReadOnlyList<KnowledgePath>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<KnowledgePath>)Items.Where(p => p.TenantId == t).ToList());
        public Task<IReadOnlyList<KnowledgePath>> ListByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<KnowledgePath>)Items.Where(p => p.TenantId == t && p.PathCode == code).ToList());
        public Task InsertAsync(KnowledgePath entity, CancellationToken ct) { Items.Add(entity); return Task.CompletedTask; }
        public Task<bool> ReplaceAsync(KnowledgePath entity, int expectedVersion, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class FakeRevisions : IContentSetRevisionRepository
    {
        public List<ContentSetRevision> Items { get; } = new();
        public bool ThrowOnUpdate { get; set; }
        public Task<ContentSetRevision?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id));
        public Task<IReadOnlyList<ContentSetRevision>> ListByContentSetAsync(Guid t, Guid setId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ContentSetRevision>)Items.Where(x => x.TenantId == t && x.ContentSetId == setId).ToList());
        public Task InsertAsync(ContentSetRevision e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(ContentSetRevision e, CancellationToken ct)
            => ThrowOnUpdate ? throw new InvalidOperationException("save failed") : Task.CompletedTask;
    }

    private sealed class FakeSets : IContentSetRepository
    {
        public List<ContentSet> Items { get; } = new();
        public Task<ContentSet?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id));
        public Task<IReadOnlyList<ContentSet>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ContentSet>)Items.Where(x => x.TenantId == t).ToList());
        public Task<ContentSet?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.SetCode == code));
        public Task InsertAsync(ContentSet entity, CancellationToken ct) { Items.Add(entity); return Task.CompletedTask; }
        public Task UpdateAsync(ContentSet entity, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeTemplates : IConceptChainTemplateRepository
    {
        public List<ConceptChainTemplate> Items { get; } = new();
        public Task<ConceptChainTemplate?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id));
        public Task<IReadOnlyList<ConceptChainTemplate>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ConceptChainTemplate>)Items.Where(x => x.TenantId == t).ToList());
        public Task<IReadOnlyList<ConceptChainTemplate>> ListBySubjectAsync(Guid t, Guid s, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ConceptChainTemplate>)Items.Where(x => x.TenantId == t && x.SubjectId == s).ToList());
        public Task<IReadOnlyList<ConceptChainTemplate>> ListByCodeAsync(Guid t, Guid s, string code, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ConceptChainTemplate>)Items.Where(x => x.TenantId == t && x.ChainCode == code).ToList());
        public Task InsertAsync(ConceptChainTemplate entity, CancellationToken ct) { Items.Add(entity); return Task.CompletedTask; }
        public Task UpdateAsync(ConceptChainTemplate entity, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeSubjects : ISubjectRepository
    {
        public List<Subject> Items { get; } = new();
        public Task<Subject?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(s => s.TenantId == t && s.Id == id));
        public Task<IReadOnlyList<Subject>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Subject>)Items.Where(s => s.TenantId == t).ToList());
        public Task<Subject?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct) => Task.FromResult<Subject?>(null);
        public Task InsertAsync(Subject subject, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(Subject subject, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeProfiles : IAudienceProfileRepository
    {
        public List<AudienceProfile> Items { get; } = new();
        public Task<AudienceProfile?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(p => p.TenantId == t && p.Id == id));
        public Task<IReadOnlyList<AudienceProfile>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<AudienceProfile>)Items.Where(p => p.TenantId == t).ToList());
        public Task<AudienceProfile?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult<AudienceProfile?>(null);
        public Task InsertAsync(AudienceProfile profile, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(AudienceProfile profile, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class NoTopics : ITopicRepository
    {
        public Task<Topic?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult<Topic?>(null);
        public Task<IReadOnlyList<Topic>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Topic>)new List<Topic>());
        public Task<IReadOnlyList<Topic>> ListBySubjectAsync(Guid t, Guid subjectId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Topic>)new List<Topic>());
        public Task<Topic?> GetActiveByCodeAsync(Guid t, Guid subjectId, string code, CancellationToken ct)
            => Task.FromResult<Topic?>(null);
        public Task InsertAsync(Topic topic, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(Topic topic, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeNodes : IConceptNodeRepository
    {
        public List<ConceptNode> Items { get; } = new();
        public Task<ConceptNode?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(n => n.TenantId == t && n.Id == id));
        public Task<IReadOnlyList<ConceptNode>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ConceptNode>)Items.Where(n => n.TenantId == t).ToList());
        public Task<IReadOnlyList<ConceptNode>> ListBySubjectAsync(Guid t, Guid subjectId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ConceptNode>)new List<ConceptNode>());
        public Task<ConceptNode?> GetActiveByCodeAsync(Guid t, Guid subjectId, Guid typeId, string code, CancellationToken ct)
            => Task.FromResult<ConceptNode?>(null);
        public Task InsertAsync(ConceptNode n, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(ConceptNode n, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeClaims : IClaimRepository
    {
        public List<Claim> Items { get; } = new();
        public Task<Claim?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.Id == id));
        public Task<IReadOnlyList<Claim>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Claim>)Items.Where(c => c.TenantId == t).ToList());
        public Task<IReadOnlyList<Claim>> ListByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Claim>)Items.Where(c => c.TenantId == t && c.ClaimCode == code).ToList());
        public Task<Claim?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.ClaimCode == code && !c.IsArchived()));
        public Task InsertAsync(Claim entity, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(Claim entity, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeVersions : IClaimCountryVersionRepository
    {
        public List<ClaimCountryVersion> Items { get; } = new();
        public Task<ClaimCountryVersion?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(v => v.TenantId == t && v.Id == id));
        public Task<IReadOnlyList<ClaimCountryVersion>> ListByClaimCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ClaimCountryVersion>)Items.Where(v => v.TenantId == t && v.ClaimCode == code).ToList());
        public Task<IReadOnlyList<ClaimCountryVersion>> ListByClaimIdAsync(Guid t, Guid claimId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ClaimCountryVersion>)Items.Where(v => v.TenantId == t && v.ClaimId == claimId).ToList());
        public Task<IReadOnlyList<ClaimCountryVersion>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ClaimCountryVersion>)Items.Where(v => v.TenantId == t).ToList());
        public Task InsertAsync(ClaimCountryVersion entity, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(ClaimCountryVersion entity, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeJourneys : IContentEngagementJourneyRepository
    {
        public List<ContentEngagementJourney> Items { get; } = new();
        public Task<ContentEngagementJourney?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(j => j.TenantId == t && j.Id == id));
        public Task<IReadOnlyList<ContentEngagementJourney>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ContentEngagementJourney>)Items.Where(j => j.TenantId == t).ToList());
        public Task<IReadOnlyList<ContentEngagementJourney>> ListByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ContentEngagementJourney>)new List<ContentEngagementJourney>());
        public Task InsertAsync(ContentEngagementJourney entity, CancellationToken ct) { Items.Add(entity); return Task.CompletedTask; }
        public Task<bool> ReplaceAsync(ContentEngagementJourney entity, int expectedVersion, CancellationToken ct) => Task.FromResult(true);
    }
}
