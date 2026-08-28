using System.Reflection;
using Diten.CrmService.Api.Controllers.CRM;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Knowledge.AudienceProfile.Commands;
using Diten.CrmService.Application.Features.Knowledge.AudienceProfile.Handlers;
using Diten.CrmService.Application.Features.Knowledge.Content;
using Diten.CrmService.Application.Features.Knowledge.Content.Commands;
using Diten.CrmService.Application.Features.Knowledge.Content.Handlers;
using Diten.CrmService.Application.Features.Knowledge.Content.Queries;
using Diten.CrmService.Application.Features.Knowledge.Contract;
using Diten.CrmService.Application.Features.Knowledge.Subject.Commands;
using Diten.CrmService.Application.Features.Knowledge.Subject.Handlers;
using Diten.CrmService.Application.Features.Knowledge.Topic.Commands;
using Diten.CrmService.Application.Features.Knowledge.Topic.Handlers;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using ContentEntity = Diten.CrmService.Domain.Entities.KnowledgeContent;
using SubjectEntity = Diten.CrmService.Domain.Entities.Subject;
using TopicEntity = Diten.CrmService.Domain.Entities.Topic;
using AudienceProfileEntity = Diten.CrmService.Domain.Entities.AudienceProfile;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// MOD-0162 FU02 — Knowledge content + subject/topic/audience-profile runtime. Pins down: in-domain vocabulary is
/// fail-closed (unknown type/status/source → 400), TenantId is claim-only, ContentCode uniqueness (409), at least one
/// content pointer, effective-window validation, the archived read-only freeze (409 on update, idempotent archive),
/// the topic hierarchy guards (cross-subject / self / cycle → 400), new content cannot attach to an archived
/// classification (409), cross-tenant isolation (404), the seven positive contract flags, the ABSENCE of every
/// forbidden flag, and that the content-linkage read provider returns published + effective content only and mutates
/// nothing.
/// </summary>
public sealed class KnowledgeContentRuntimeTests
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
        public FakeContentRepo Contents { get; } = new();
        public FakeSubjectRepo Subjects { get; } = new();
        public FakeTopicRepo Topics { get; } = new();
        public FakeAudienceProfileRepo Profiles { get; } = new();
        public FakeConceptNodeRepo ConceptNodes { get; } = new();
        public Guid TenantId { get; }

        public Fixture(Guid tenant) => TenantId = tenant;

        public CreateKnowledgeContentHandler CreateContent(Guid? tenant = null)
            => new(Tenant(tenant ?? TenantId), new NullActorContext(), Contents, Subjects, Topics, Profiles, ConceptNodes);

        public UpdateKnowledgeContentHandler UpdateContent()
            => new(Tenant(TenantId), new NullActorContext(), Contents, Subjects, Topics, Profiles, ConceptNodes);

        public ArchiveKnowledgeContentHandler ArchiveContent()
            => new(Tenant(TenantId), new NullActorContext(), Contents);

        public GetKnowledgeContentHandler GetContent(Guid? tenant = null)
            => new(Tenant(tenant ?? TenantId), Contents);

        public CreateSubjectHandler CreateSubject() => new(Tenant(TenantId), new NullActorContext(), Subjects);
        public ArchiveSubjectHandler ArchiveSubject() => new(Tenant(TenantId), new NullActorContext(), Subjects);

        public UnarchiveSubjectHandler UnarchiveSubject()
            => new(Tenant(TenantId), new NullActorContext(), Subjects);

        public UpdateSubjectHandler UpdateSubject() => new(Tenant(TenantId), new NullActorContext(), Subjects);

        public CreateTopicHandler CreateTopic() => new(Tenant(TenantId), new NullActorContext(), Topics, Subjects);
        public UpdateTopicHandler UpdateTopic() => new(Tenant(TenantId), new NullActorContext(), Topics);
        public ArchiveTopicHandler ArchiveTopic() => new(Tenant(TenantId), new NullActorContext(), Topics);

        public UnarchiveTopicHandler UnarchiveTopic()
            => new(Tenant(TenantId), new NullActorContext(), Topics, Subjects);

        public UnarchiveAudienceProfileHandler UnarchiveProfile()
            => new(Tenant(TenantId), new NullActorContext(), Profiles);

        public CreateAudienceProfileHandler CreateProfile()
            => new(Tenant(TenantId), new NullActorContext(), Profiles);

        public ArchiveAudienceProfileHandler ArchiveProfile()
            => new(Tenant(TenantId), new NullActorContext(), Profiles);

        public KnowledgeContentLinkageReader LinkageReader(Guid? tenant = null)
            => new(Tenant(tenant ?? TenantId), Contents);

        public async Task<Guid> SeedSubjectAsync(string code = "SUB-1")
        {
            var response = await CreateSubject().Handle(
                new CreateSubjectCommand(code, "Subject " + code, Jan1, Status: TaxonomyStatuses.Active), default);
            Assert.Equal(201, response.StatusCode);
            return response.Data;
        }
    }

    private static CreateKnowledgeContentCommand ContentCmd(
        Guid subjectId,
        string code = "KC-1",
        string type = KnowledgeContentTypes.Presentation,
        string? status = KnowledgeContentStatuses.Published,
        string? source = KnowledgeContentSources.Manual,
        string? url = "https://example.test/deck",
        string? body = null,
        string? asset = null,
        string? file = null,
        Guid? brandId = null,
        Guid? topicId = null,
        Guid? profileId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null)
        => new(code, "Content " + code, type, subjectId, "en", "1.0", from ?? Jan1, status, topicId, profileId,
            null, brandId, null, null, null, null, body, asset, file, url, to, source);

    // 1
    [Fact]
    public async Task Create_content_valid_returns_201()
    {
        var fx = new Fixture(TenantA);
        var subjectId = await fx.SeedSubjectAsync();
        var response = await fx.CreateContent().Handle(ContentCmd(subjectId), default);
        Assert.Equal(201, response.StatusCode);
        Assert.NotEqual(Guid.Empty, response.Data);
    }

    // 2
    [Fact]
    public async Task Duplicate_content_code_returns_409()
    {
        var fx = new Fixture(TenantA);
        var subjectId = await fx.SeedSubjectAsync();
        Assert.Equal(201, (await fx.CreateContent().Handle(ContentCmd(subjectId, "KC-DUP"), default)).StatusCode);
        var second = await fx.CreateContent().Handle(ContentCmd(subjectId, "KC-DUP"), default);
        Assert.Equal(409, second.StatusCode);
    }

    // 3
    [Fact]
    public async Task Unknown_content_status_returns_400()
    {
        var fx = new Fixture(TenantA);
        var subjectId = await fx.SeedSubjectAsync();
        var response = await fx.CreateContent().Handle(ContentCmd(subjectId, status: "bogus-status"), default);
        Assert.Equal(400, response.StatusCode);
    }

    // 4
    [Fact]
    public async Task Unknown_content_type_returns_400()
    {
        var fx = new Fixture(TenantA);
        var subjectId = await fx.SeedSubjectAsync();
        var response = await fx.CreateContent().Handle(ContentCmd(subjectId, type: "bogus-type"), default);
        Assert.Equal(400, response.StatusCode);
    }

    // 5
    [Fact]
    public async Task Unknown_source_returns_400()
    {
        var fx = new Fixture(TenantA);
        var subjectId = await fx.SeedSubjectAsync();
        var response = await fx.CreateContent().Handle(ContentCmd(subjectId, source: "bogus-source"), default);
        Assert.Equal(400, response.StatusCode);
    }

    // 6
    [Fact]
    public async Task Missing_all_content_pointers_returns_400()
    {
        var fx = new Fixture(TenantA);
        var subjectId = await fx.SeedSubjectAsync();
        var response = await fx.CreateContent().Handle(
            ContentCmd(subjectId, url: null, body: null, asset: null, file: null), default);
        Assert.Equal(400, response.StatusCode);
    }

    // 7
    [Fact]
    public async Task Effective_to_before_from_returns_400()
    {
        var fx = new Fixture(TenantA);
        var subjectId = await fx.SeedSubjectAsync();
        var response = await fx.CreateContent().Handle(
            ContentCmd(subjectId, from: Jun1, to: Jan1), default);
        Assert.Equal(400, response.StatusCode);
    }

    // 8
    [Fact]
    public async Task Archived_content_update_returns_409()
    {
        var fx = new Fixture(TenantA);
        var subjectId = await fx.SeedSubjectAsync();
        var id = (await fx.CreateContent().Handle(ContentCmd(subjectId, "KC-ARC"), default)).Data;
        Assert.Equal(200, (await fx.ArchiveContent().Handle(new ArchiveKnowledgeContentCommand(id), default)).StatusCode);

        var update = await fx.UpdateContent().Handle(
            new UpdateKnowledgeContentCommand(id, "New title", KnowledgeContentTypes.Presentation, subjectId, "en",
                "1.1", Jan1, KnowledgeContentStatuses.Published, Url: "https://example.test/x"), default);
        Assert.Equal(409, update.StatusCode);
    }

    // 9
    [Fact]
    public async Task Archive_content_is_idempotent()
    {
        var fx = new Fixture(TenantA);
        var subjectId = await fx.SeedSubjectAsync();
        var id = (await fx.CreateContent().Handle(ContentCmd(subjectId, "KC-IDEM"), default)).Data;
        Assert.Equal(200, (await fx.ArchiveContent().Handle(new ArchiveKnowledgeContentCommand(id), default)).StatusCode);
        Assert.Equal(200, (await fx.ArchiveContent().Handle(new ArchiveKnowledgeContentCommand(id), default)).StatusCode);
    }

    // 10
    [Fact]
    public void No_delete_endpoint_exists_on_any_knowledge_controller()
    {
        var controllers = new[]
        {
            typeof(KnowledgeContentsController), typeof(KnowledgeSubjectsController),
            typeof(KnowledgeTopicsController), typeof(KnowledgeAudienceProfilesController),
            typeof(KnowledgeContractController)
        };

        foreach (var controller in controllers)
        {
            foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.Empty(method.GetCustomAttributes<HttpDeleteAttribute>());
                Assert.Empty(method.GetCustomAttributes<HttpPatchAttribute>());
            }
        }
    }

    // 11
    [Fact]
    public async Task Create_subject_valid_returns_201()
    {
        var fx = new Fixture(TenantA);
        Assert.NotEqual(Guid.Empty, await fx.SeedSubjectAsync());
    }

    // 12
    [Fact]
    public async Task Duplicate_subject_code_returns_409()
    {
        var fx = new Fixture(TenantA);
        await fx.SeedSubjectAsync("SUB-DUP");
        var second = await fx.CreateSubject().Handle(
            new CreateSubjectCommand("SUB-DUP", "Another", Jan1, Status: TaxonomyStatuses.Active), default);
        Assert.Equal(409, second.StatusCode);
    }

    // 13
    [Fact]
    public async Task New_content_on_archived_subject_returns_409()
    {
        var fx = new Fixture(TenantA);
        var subjectId = await fx.SeedSubjectAsync("SUB-ARC");
        Assert.Equal(200, (await fx.ArchiveSubject().Handle(new ArchiveSubjectCommand(subjectId), default)).StatusCode);
        var response = await fx.CreateContent().Handle(ContentCmd(subjectId, "KC-ONARC"), default);
        Assert.Equal(409, response.StatusCode);
    }

    // 14
    [Fact]
    public async Task Create_topic_valid_returns_201()
    {
        var fx = new Fixture(TenantA);
        var subjectId = await fx.SeedSubjectAsync();
        var response = await fx.CreateTopic().Handle(
            new CreateTopicCommand(subjectId, "TOP-1", "Topic 1", Jan1, Status: TaxonomyStatuses.Active), default);
        Assert.Equal(201, response.StatusCode);
    }

    // 15
    [Fact]
    public async Task Topic_cross_subject_parent_returns_400()
    {
        var fx = new Fixture(TenantA);
        var subjectA = await fx.SeedSubjectAsync("SUB-A");
        var subjectB = await fx.SeedSubjectAsync("SUB-B");
        var parentInB = (await fx.CreateTopic().Handle(
            new CreateTopicCommand(subjectB, "TOP-B", "Topic B", Jan1, Status: TaxonomyStatuses.Active), default)).Data;

        var response = await fx.CreateTopic().Handle(
            new CreateTopicCommand(subjectA, "TOP-A", "Topic A", Jan1, ParentTopicId: parentInB,
                Status: TaxonomyStatuses.Active), default);
        Assert.Equal(400, response.StatusCode);
    }

    // 16
    [Fact]
    public async Task Topic_self_parent_returns_400()
    {
        var fx = new Fixture(TenantA);
        var subjectId = await fx.SeedSubjectAsync();
        var topicId = (await fx.CreateTopic().Handle(
            new CreateTopicCommand(subjectId, "TOP-SELF", "Self", Jan1, Status: TaxonomyStatuses.Active), default)).Data;

        var response = await fx.UpdateTopic().Handle(
            new UpdateTopicCommand(topicId, "Self", Jan1, ParentTopicId: topicId, Status: TaxonomyStatuses.Active),
            default);
        Assert.Equal(400, response.StatusCode);
    }

    // 17
    [Fact]
    public async Task Topic_parent_cycle_returns_400()
    {
        var fx = new Fixture(TenantA);
        var subjectId = await fx.SeedSubjectAsync();
        var a = (await fx.CreateTopic().Handle(
            new CreateTopicCommand(subjectId, "A", "A", Jan1, Status: TaxonomyStatuses.Active), default)).Data;
        var b = (await fx.CreateTopic().Handle(
            new CreateTopicCommand(subjectId, "B", "B", Jan1, ParentTopicId: a, Status: TaxonomyStatuses.Active),
            default)).Data;

        // A → parent B would close the cycle A→B→A.
        var response = await fx.UpdateTopic().Handle(
            new UpdateTopicCommand(a, "A", Jan1, ParentTopicId: b, Status: TaxonomyStatuses.Active), default);
        Assert.Equal(400, response.StatusCode);
    }

    // 18
    [Fact]
    public async Task Create_audience_profile_valid_returns_201()
    {
        var fx = new Fixture(TenantA);
        var response = await fx.CreateProfile().Handle(
            new CreateAudienceProfileCommand("AP-1", "Cardiology A", Jan1, Status: TaxonomyStatuses.Active), default);
        Assert.Equal(201, response.StatusCode);
    }

    // 19
    [Fact]
    public async Task New_content_on_archived_profile_returns_409()
    {
        var fx = new Fixture(TenantA);
        var subjectId = await fx.SeedSubjectAsync();
        var profileId = (await fx.CreateProfile().Handle(
            new CreateAudienceProfileCommand("AP-ARC", "Prof", Jan1, Status: TaxonomyStatuses.Active), default)).Data;
        Assert.Equal(200,
            (await fx.ArchiveProfile().Handle(new ArchiveAudienceProfileCommand(profileId), default)).StatusCode);

        var response = await fx.CreateContent().Handle(ContentCmd(subjectId, "KC-PROF", profileId: profileId), default);
        Assert.Equal(409, response.StatusCode);
    }

    // 20
    [Fact]
    public async Task Cross_tenant_read_returns_404()
    {
        var fx = new Fixture(TenantA);
        var subjectId = await fx.SeedSubjectAsync();
        var id = (await fx.CreateContent().Handle(ContentCmd(subjectId, "KC-ISO"), default)).Data;

        var response = await fx.GetContent(TenantB).Handle(new GetKnowledgeContentQuery(id), default);
        Assert.Equal(404, response.StatusCode);
    }

    // 21
    [Fact]
    public async Task Contract_publishes_the_seven_positive_flags_true()
    {
        var handler = new GetKnowledgeContractHandler(Tenant(TenantA));
        var response = await handler.Handle(new GetKnowledgeContractQuery(), default);
        Assert.Equal(200, response.StatusCode);
        var f = response.Data!.Features;
        Assert.True(f.SupportsKnowledgeContentManagement);
        Assert.True(f.SupportsSubjectTaxonomyManagement);
        Assert.True(f.SupportsConceptGraphReference);
        Assert.True(f.SupportsBrandProductReference);
        Assert.True(f.SupportsArchiveLifecycle);
        Assert.True(f.SupportsEffectiveDating);
        Assert.True(f.SupportsContractDrivenUi);
        // Exactly seven boolean capability flags — no more, no less.
        var boolFlags = typeof(KnowledgeFeatureFlags).GetProperties()
            .Where(p => p.PropertyType == typeof(bool)).ToList();
        Assert.Equal(7, boolFlags.Count);
    }

    // 22
    [Fact]
    public void Contract_flags_never_expose_a_forbidden_capability()
    {
        var forbidden = new[]
        {
            "SupportsVisitPlanning", "SupportsRoutePlanning", "SupportsRecommendationEngine",
            "SupportsDigitalDetailingRuntime", "SupportsWorkflowApproval", "SupportsCampaignRuntimeMutation",
            "SupportsBrandProductMasterOwnership", "SupportsFileStorage", "SupportsHardDelete"
        };
        var names = typeof(KnowledgeFeatureFlags).GetProperties().Select(p => p.Name).ToHashSet();
        foreach (var name in forbidden)
        {
            Assert.DoesNotContain(name, names);
        }
    }

    // 23 + 24
    [Fact]
    public async Task Linkage_reader_returns_published_effective_only_and_mutates_nothing()
    {
        var fx = new Fixture(TenantA);
        var subjectId = await fx.SeedSubjectAsync();
        var brandId = Guid.NewGuid();

        // published + effective now
        await fx.CreateContent().Handle(ContentCmd(subjectId, "KC-PUB", brandId: brandId), default);
        // draft — must be excluded by the seam
        await fx.CreateContent().Handle(
            ContentCmd(subjectId, "KC-DRAFT", status: KnowledgeContentStatuses.Draft), default);
        // published but not yet effective — must be excluded
        await fx.CreateContent().Handle(
            ContentCmd(subjectId, "KC-FUTURE", from: Jun1), default);

        var writesBefore = fx.Contents.WriteCount;
        var results = await fx.LinkageReader().ResolvePublishedContentAsync(
            new KnowledgeContentLinkageCriteria(SubjectId: subjectId, EffectiveAt: Jan1), default);

        Assert.Single(results);
        Assert.Equal("KC-PUB", results[0].ContentCode);
        // BrandId is carried as a reference, unresolved and unchanged (no MDM master is read/mutated).
        Assert.Equal(brandId, results[0].BrandId);
        // The read seam never writes.
        Assert.Equal(writesBefore, fx.Contents.WriteCount);
    }

    // ---------------- Unarchive (restore) ----------------

    // 25
    [Fact]
    public async Task Unarchive_subject_restores_it_as_inactive_and_clears_the_archive_stamp()
    {
        var fx = new Fixture(TenantA);
        var subjectId = await fx.SeedSubjectAsync("SUB-RES");
        Assert.Equal(200, (await fx.ArchiveSubject().Handle(new ArchiveSubjectCommand(subjectId), default)).StatusCode);

        var response = await fx.UnarchiveSubject().Handle(new UnarchiveSubjectCommand(subjectId), default);

        Assert.Equal(200, response.StatusCode);
        var subject = fx.Subjects.Items.Single(s => s.Id == subjectId);
        Assert.False(subject.IsArchived());
        Assert.Null(subject.ArchivedBy);
        // Restored, not put back in use: re-activation stays a separate decision.
        Assert.Equal(TaxonomyStatuses.Inactive, subject.Status);
    }

    // 26
    [Fact]
    public async Task Unarchive_subject_is_idempotent_on_a_live_row()
    {
        var fx = new Fixture(TenantA);
        var subjectId = await fx.SeedSubjectAsync("SUB-LIVE");

        Assert.Equal(200,
            (await fx.UnarchiveSubject().Handle(new UnarchiveSubjectCommand(subjectId), default)).StatusCode);
        // A live row keeps the status it already had — restore must not silently deactivate it.
        Assert.Equal(TaxonomyStatuses.Active, fx.Subjects.Items.Single(s => s.Id == subjectId).Status);
    }

    // 27
    [Fact]
    public async Task Unarchive_subject_whose_code_was_reused_returns_409()
    {
        var fx = new Fixture(TenantA);
        var subjectId = await fx.SeedSubjectAsync("SUB-REUSE");
        Assert.Equal(200, (await fx.ArchiveSubject().Handle(new ArchiveSubjectCommand(subjectId), default)).StatusCode);
        // An archived code is reusable, so a new subject may legitimately take it while the first one is closed.
        await fx.SeedSubjectAsync("SUB-REUSE");

        var response = await fx.UnarchiveSubject().Handle(new UnarchiveSubjectCommand(subjectId), default);

        Assert.Equal(409, response.StatusCode);
        Assert.True(fx.Subjects.Items.Single(s => s.Id == subjectId).IsArchived());
    }

    // 28
    [Fact]
    public async Task Unarchived_subject_accepts_new_content_again()
    {
        var fx = new Fixture(TenantA);
        var subjectId = await fx.SeedSubjectAsync("SUB-ROUND");
        Assert.Equal(200, (await fx.ArchiveSubject().Handle(new ArchiveSubjectCommand(subjectId), default)).StatusCode);
        Assert.Equal(409, (await fx.CreateContent().Handle(ContentCmd(subjectId, "KC-CLOSED"), default)).StatusCode);

        Assert.Equal(200,
            (await fx.UnarchiveSubject().Handle(new UnarchiveSubjectCommand(subjectId), default)).StatusCode);

        Assert.Equal(201, (await fx.CreateContent().Handle(ContentCmd(subjectId, "KC-REOPEN"), default)).StatusCode);
    }

    // 29
    [Fact]
    public async Task Unarchive_topic_under_an_archived_subject_returns_409()
    {
        var fx = new Fixture(TenantA);
        var subjectId = await fx.SeedSubjectAsync("SUB-TOPARC");
        var topicId = (await fx.CreateTopic().Handle(
            new CreateTopicCommand(subjectId, "TOP-ARC", "Topic", Jan1, Status: TaxonomyStatuses.Active), default)).Data;
        Assert.Equal(200, (await fx.ArchiveTopic().Handle(new ArchiveTopicCommand(topicId), default)).StatusCode);
        Assert.Equal(200, (await fx.ArchiveSubject().Handle(new ArchiveSubjectCommand(subjectId), default)).StatusCode);

        var response = await fx.UnarchiveTopic().Handle(new UnarchiveTopicCommand(topicId), default);

        // Restoring must never produce a row the create path would have rejected.
        Assert.Equal(409, response.StatusCode);
        Assert.True(fx.Topics.Items.Single(t => t.Id == topicId).IsArchived());
    }

    // 30
    [Fact]
    public async Task Unarchive_topic_with_an_archived_parent_returns_409()
    {
        var fx = new Fixture(TenantA);
        var subjectId = await fx.SeedSubjectAsync("SUB-PARENT");
        var parentId = (await fx.CreateTopic().Handle(
            new CreateTopicCommand(subjectId, "TOP-P", "Parent", Jan1, Status: TaxonomyStatuses.Active), default)).Data;
        var childId = (await fx.CreateTopic().Handle(
            new CreateTopicCommand(subjectId, "TOP-C", "Child", Jan1, parentId, Status: TaxonomyStatuses.Active),
            default)).Data;
        Assert.Equal(200, (await fx.ArchiveTopic().Handle(new ArchiveTopicCommand(childId), default)).StatusCode);
        Assert.Equal(200, (await fx.ArchiveTopic().Handle(new ArchiveTopicCommand(parentId), default)).StatusCode);

        var response = await fx.UnarchiveTopic().Handle(new UnarchiveTopicCommand(childId), default);

        Assert.Equal(409, response.StatusCode);
    }

    // 31
    [Fact]
    public async Task Unarchive_topic_restores_it_as_inactive()
    {
        var fx = new Fixture(TenantA);
        var subjectId = await fx.SeedSubjectAsync("SUB-TOPOK");
        var topicId = (await fx.CreateTopic().Handle(
            new CreateTopicCommand(subjectId, "TOP-OK", "Topic", Jan1, Status: TaxonomyStatuses.Active), default)).Data;
        Assert.Equal(200, (await fx.ArchiveTopic().Handle(new ArchiveTopicCommand(topicId), default)).StatusCode);

        Assert.Equal(200, (await fx.UnarchiveTopic().Handle(new UnarchiveTopicCommand(topicId), default)).StatusCode);

        var topic = fx.Topics.Items.Single(t => t.Id == topicId);
        Assert.False(topic.IsArchived());
        Assert.Equal(TaxonomyStatuses.Inactive, topic.Status);
    }

    // 32
    [Fact]
    public async Task Unarchive_audience_profile_whose_code_was_reused_returns_409()
    {
        var fx = new Fixture(TenantA);
        var profileId = (await fx.CreateProfile().Handle(
            new CreateAudienceProfileCommand("AP-REUSE", "Prof", Jan1, Status: TaxonomyStatuses.Active), default)).Data;
        Assert.Equal(200,
            (await fx.ArchiveProfile().Handle(new ArchiveAudienceProfileCommand(profileId), default)).StatusCode);
        Assert.Equal(201, (await fx.CreateProfile().Handle(
            new CreateAudienceProfileCommand("AP-REUSE", "Other", Jan1, Status: TaxonomyStatuses.Active), default))
            .StatusCode);

        var response = await fx.UnarchiveProfile().Handle(new UnarchiveAudienceProfileCommand(profileId), default);

        Assert.Equal(409, response.StatusCode);
    }

    // 33
    [Fact]
    public async Task Unarchive_audience_profile_restores_it_as_inactive()
    {
        var fx = new Fixture(TenantA);
        var profileId = (await fx.CreateProfile().Handle(
            new CreateAudienceProfileCommand("AP-RES", "Prof", Jan1, Status: TaxonomyStatuses.Active), default)).Data;
        Assert.Equal(200,
            (await fx.ArchiveProfile().Handle(new ArchiveAudienceProfileCommand(profileId), default)).StatusCode);

        Assert.Equal(200,
            (await fx.UnarchiveProfile().Handle(new UnarchiveAudienceProfileCommand(profileId), default)).StatusCode);

        var profile = fx.Profiles.Items.Single(p => p.Id == profileId);
        Assert.False(profile.IsArchived());
        Assert.Equal(TaxonomyStatuses.Inactive, profile.Status);
    }

    // 34
    [Fact]
    public async Task Unarchive_subject_that_does_not_exist_returns_404()
    {
        var fx = new Fixture(TenantA);
        var response = await fx.UnarchiveSubject().Handle(new UnarchiveSubjectCommand(Guid.NewGuid()), default);
        Assert.Equal(404, response.StatusCode);
    }

    // ---------------- Parent subject (subject hierarchy) ----------------

    // 35
    [Fact]
    public async Task Create_subject_under_a_parent_persists_the_link()
    {
        var fx = new Fixture(TenantA);
        var parentId = await fx.SeedSubjectAsync("SUB-P");

        var response = await fx.CreateSubject().Handle(
            new CreateSubjectCommand("SUB-C", "Child", Jan1, parentId, Status: TaxonomyStatuses.Active), default);

        Assert.Equal(201, response.StatusCode);
        Assert.Equal(parentId, fx.Subjects.Items.Single(s => s.Id == response.Data).ParentSubjectId);
    }

    // 36
    [Fact]
    public async Task Create_subject_with_an_unknown_parent_returns_400()
    {
        var fx = new Fixture(TenantA);
        var response = await fx.CreateSubject().Handle(
            new CreateSubjectCommand("SUB-NOP", "Child", Jan1, Guid.NewGuid(), Status: TaxonomyStatuses.Active),
            default);
        Assert.Equal(400, response.StatusCode);
    }

    // 37
    [Fact]
    public async Task Create_subject_under_an_archived_parent_returns_400()
    {
        var fx = new Fixture(TenantA);
        var parentId = await fx.SeedSubjectAsync("SUB-PARC");
        Assert.Equal(200, (await fx.ArchiveSubject().Handle(new ArchiveSubjectCommand(parentId), default)).StatusCode);

        var response = await fx.CreateSubject().Handle(
            new CreateSubjectCommand("SUB-CARC", "Child", Jan1, parentId, Status: TaxonomyStatuses.Active), default);

        Assert.Equal(400, response.StatusCode);
    }

    // 38
    [Fact]
    public async Task Subject_cannot_be_its_own_parent()
    {
        var fx = new Fixture(TenantA);
        var subjectId = await fx.SeedSubjectAsync("SUB-SELF");

        var response = await fx.UpdateSubject().Handle(
            new UpdateSubjectCommand(subjectId, "Self", Jan1, subjectId, Status: TaxonomyStatuses.Active), default);

        Assert.Equal(400, response.StatusCode);
    }

    // 39
    [Fact]
    public async Task Subject_parent_assignment_that_would_create_a_cycle_returns_400()
    {
        var fx = new Fixture(TenantA);
        var rootId = await fx.SeedSubjectAsync("SUB-ROOT");
        var childId = (await fx.CreateSubject().Handle(
            new CreateSubjectCommand("SUB-CHILD", "Child", Jan1, rootId, Status: TaxonomyStatuses.Active), default))
            .Data;

        // root → child would close the loop root → child → root.
        var response = await fx.UpdateSubject().Handle(
            new UpdateSubjectCommand(rootId, "Root", Jan1, childId, Status: TaxonomyStatuses.Active), default);

        Assert.Equal(400, response.StatusCode);
    }

    // 40
    [Fact]
    public async Task Unarchive_subject_with_an_archived_parent_returns_409()
    {
        var fx = new Fixture(TenantA);
        var parentId = await fx.SeedSubjectAsync("SUB-PU");
        var childId = (await fx.CreateSubject().Handle(
            new CreateSubjectCommand("SUB-CU", "Child", Jan1, parentId, Status: TaxonomyStatuses.Active), default))
            .Data;
        Assert.Equal(200, (await fx.ArchiveSubject().Handle(new ArchiveSubjectCommand(childId), default)).StatusCode);
        Assert.Equal(200, (await fx.ArchiveSubject().Handle(new ArchiveSubjectCommand(parentId), default)).StatusCode);

        var response = await fx.UnarchiveSubject().Handle(new UnarchiveSubjectCommand(childId), default);

        Assert.Equal(409, response.StatusCode);
    }

    // 41
    [Fact]
    public async Task Subject_parent_can_be_cleared_on_update()
    {
        var fx = new Fixture(TenantA);
        var parentId = await fx.SeedSubjectAsync("SUB-CLRP");
        var childId = (await fx.CreateSubject().Handle(
            new CreateSubjectCommand("SUB-CLRC", "Child", Jan1, parentId, Status: TaxonomyStatuses.Active), default))
            .Data;

        Assert.Equal(200, (await fx.UpdateSubject().Handle(
            new UpdateSubjectCommand(childId, "Child", Jan1, null, Status: TaxonomyStatuses.Active), default))
            .StatusCode);

        Assert.Null(fx.Subjects.Items.Single(s => s.Id == childId).ParentSubjectId);
    }

    // ---------------- in-memory fakes (Update = no-op; handlers mutate the tracked reference in place) ----------------

    private sealed class FakeContentRepo : IKnowledgeContentRepository
    {
        public List<ContentEntity> Items { get; } = new();
        public int WriteCount { get; private set; }

        public Task<ContentEntity?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.Id == id && !c.IsDeleted));

        public Task<IReadOnlyList<ContentEntity>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ContentEntity>)Items
                .Where(c => c.TenantId == t && !c.IsDeleted).OrderByDescending(c => c.CreatedAt).ToList());

        public Task<ContentEntity?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(c =>
                c.TenantId == t && !c.IsDeleted && c.ContentCode == code && !c.IsArchived()));

        public Task InsertAsync(ContentEntity content, CancellationToken ct)
        {
            WriteCount++;
            Items.Add(content);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ContentEntity content, CancellationToken ct)
        {
            WriteCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSubjectRepo : ISubjectRepository
    {
        public List<SubjectEntity> Items { get; } = new();

        public Task<SubjectEntity?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(s => s.TenantId == t && s.Id == id && !s.IsDeleted));

        public Task<IReadOnlyList<SubjectEntity>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<SubjectEntity>)Items.Where(s => s.TenantId == t && !s.IsDeleted).ToList());

        public Task<SubjectEntity?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(s =>
                s.TenantId == t && !s.IsDeleted && s.SubjectCode == code && !s.IsArchived()));

        public Task InsertAsync(SubjectEntity subject, CancellationToken ct)
        {
            Items.Add(subject);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(SubjectEntity subject, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeTopicRepo : ITopicRepository
    {
        public List<TopicEntity> Items { get; } = new();

        public Task<TopicEntity?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));

        public Task<IReadOnlyList<TopicEntity>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<TopicEntity>)Items.Where(x => x.TenantId == t && !x.IsDeleted).ToList());

        public Task<IReadOnlyList<TopicEntity>> ListBySubjectAsync(Guid t, Guid subjectId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<TopicEntity>)Items
                .Where(x => x.TenantId == t && !x.IsDeleted && x.SubjectId == subjectId).ToList());

        public Task<TopicEntity?> GetActiveByCodeAsync(Guid t, Guid subjectId, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x =>
                x.TenantId == t && !x.IsDeleted && x.SubjectId == subjectId && x.TopicCode == code && !x.IsArchived()));

        public Task InsertAsync(TopicEntity topic, CancellationToken ct)
        {
            Items.Add(topic);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TopicEntity topic, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeAudienceProfileRepo : IAudienceProfileRepository
    {
        public List<AudienceProfileEntity> Items { get; } = new();

        public Task<AudienceProfileEntity?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(p => p.TenantId == t && p.Id == id && !p.IsDeleted));

        public Task<IReadOnlyList<AudienceProfileEntity>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<AudienceProfileEntity>)Items
                .Where(p => p.TenantId == t && !p.IsDeleted).ToList());

        public Task<AudienceProfileEntity?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(p =>
                p.TenantId == t && !p.IsDeleted && p.ProfileCode == code && !p.IsArchived()));

        public Task InsertAsync(AudienceProfileEntity profile, CancellationToken ct)
        {
            Items.Add(profile);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(AudienceProfileEntity profile, CancellationToken ct) => Task.CompletedTask;
    }

    // MOD-0162 FU03 — the concept-node repo the content handler now consults for V17. FU02 content tests never set a
    // ConceptNodeId, so this repo stays empty and the resolution is a no-op; the 23 FU02 assertions are unchanged.
    private sealed class FakeConceptNodeRepo : Diten.CrmService.Domain.Repositories.IConceptNodeRepository
    {
        public List<Diten.CrmService.Domain.Entities.ConceptNode> Items { get; } = new();

        public Task<Diten.CrmService.Domain.Entities.ConceptNode?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(n => n.TenantId == t && n.Id == id && !n.IsDeleted));

        public Task<IReadOnlyList<Diten.CrmService.Domain.Entities.ConceptNode>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Diten.CrmService.Domain.Entities.ConceptNode>)Items
                .Where(n => n.TenantId == t && !n.IsDeleted).ToList());

        public Task<IReadOnlyList<Diten.CrmService.Domain.Entities.ConceptNode>> ListBySubjectAsync(
            Guid t, Guid subjectId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Diten.CrmService.Domain.Entities.ConceptNode>)Items
                .Where(n => n.TenantId == t && !n.IsDeleted && n.SubjectId == subjectId).ToList());

        public Task<Diten.CrmService.Domain.Entities.ConceptNode?> GetActiveByCodeAsync(
            Guid t, Guid subjectId, Guid typeId, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(n =>
                n.TenantId == t && !n.IsDeleted && n.SubjectId == subjectId && n.ConceptTypeId == typeId
                && n.ConceptNodeCode == code && !n.IsArchived()));

        public Task InsertAsync(Diten.CrmService.Domain.Entities.ConceptNode n, CancellationToken ct)
        {
            Items.Add(n);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Diten.CrmService.Domain.Entities.ConceptNode n, CancellationToken ct)
            => Task.CompletedTask;
    }
}
