using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney;
using Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney.Commands;
using Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney.Contract;
using Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney.Handlers;
using Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;

namespace Diten.CrmService.Application.Tests;

using JourneyEntity = Diten.CrmService.Domain.Entities.ContentEngagementJourney;

/// <summary>
/// MOD-0162 FU05 — ContentEngagementJourney runtime tests (S2 = embedded stages). In-memory fakes; the journey repo
/// mutates in place (ReplaceAsync bumps Version and returns matched). Covers happy paths, the embedded-model invariants
/// (AC-EMBED-1/2), V-J/V-S rules, freeze + new-version incl. the id REMAP (AC-FREEZE-1/2), path resolution
/// (AC-PIN-1), repeat visibility (AC-REPEAT-1), the never-evaluated advancement/fallback/branch data, tenant isolation,
/// concurrency, the contract flags and the read-only reader seam.
/// </summary>
public sealed class ContentEngagementJourneyRuntimeTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset Past = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Future = new(2999, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static TenantContext Tenant(Guid id)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id);
        return ctx;
    }

    private sealed class Fixture
    {
        public FakeJourneyRepo Journeys { get; } = new();
        public FakePathRepo Paths { get; } = new();
        public FakeSubjectRepo Subjects { get; } = new();
        public FakeTopicRepo Topics { get; } = new();
        public FakeProfileRepo Profiles { get; } = new();
        public Guid TenantId { get; }

        public Fixture(Guid tenant) => TenantId = tenant;

        private ContentEngagementJourneyPathResolver Resolver() => new(Paths);

        public CreateContentEngagementJourneyHandler CreateJourney()
            => new(Tenant(TenantId), new NullActorContext(), Journeys, Subjects, Topics, Profiles);
        public UpdateContentEngagementJourneyHandler UpdateJourney()
            => new(Tenant(TenantId), new NullActorContext(), Journeys, Subjects, Topics, Profiles);
        public PublishContentEngagementJourneyHandler PublishJourney()
            => new(Tenant(TenantId), new NullActorContext(), Journeys);
        public CreateContentEngagementJourneyVersionHandler NewVersion()
            => new(Tenant(TenantId), new NullActorContext(), Journeys);
        public ArchiveContentEngagementJourneyHandler ArchiveJourney()
            => new(Tenant(TenantId), new NullActorContext(), Journeys);
        public AddContentEngagementJourneyStageHandler AddStage()
            => new(Tenant(TenantId), new NullActorContext(), Journeys, Resolver());
        public UpdateContentEngagementJourneyStageHandler UpdateStage()
            => new(Tenant(TenantId), new NullActorContext(), Journeys, Resolver());
        public ArchiveContentEngagementJourneyStageHandler ArchiveStage()
            => new(Tenant(TenantId), new NullActorContext(), Journeys);
        public GetContentEngagementJourneyHandler GetJourney(Guid? t = null)
            => new(Tenant(t ?? TenantId), Journeys, Resolver());
        public GetContentEngagementJourneyStagesHandler GetStages(Guid? t = null)
            => new(Tenant(t ?? TenantId), Journeys, Resolver());
        public ListContentEngagementJourneysHandler ListJourneys(Guid? t = null)
            => new(Tenant(t ?? TenantId), Journeys, Resolver());
        public ContentEngagementJourneyReader Reader()
            => new(Tenant(TenantId), Journeys, Paths);

        public Guid SeedSubject(bool archived = false)
        {
            var s = new Subject
            {
                TenantId = TenantId, SubjectCode = "SUB-" + Guid.NewGuid().ToString("N")[..6],
                SubjectName = "Subject", Status = archived ? TaxonomyStatuses.Archived : TaxonomyStatuses.Active,
                EffectiveFrom = Past, ArchivedAt = archived ? Past : null
            };
            Subjects.Items.Add(s);
            return s.Id;
        }

        public Guid SeedTopic(Guid subjectId, bool archived = false)
        {
            var t = new Topic
            {
                TenantId = TenantId, SubjectId = subjectId, TopicCode = "TOP-" + Guid.NewGuid().ToString("N")[..6],
                TopicName = "Topic", Status = archived ? TaxonomyStatuses.Archived : TaxonomyStatuses.Active,
                EffectiveFrom = Past, ArchivedAt = archived ? Past : null
            };
            Topics.Items.Add(t);
            return t.Id;
        }

        public Guid SeedProfile(bool archived = false)
        {
            var p = new AudienceProfile
            {
                TenantId = TenantId, ProfileCode = "AP-" + Guid.NewGuid().ToString("N")[..6], ProfileName = "P",
                Status = archived ? TaxonomyStatuses.Archived : TaxonomyStatuses.Active, EffectiveFrom = Past,
                ArchivedAt = archived ? Past : null
            };
            Profiles.Items.Add(p);
            return p.Id;
        }

        /// <summary>Seeds a FU04 KnowledgePath directly (FU05 never creates or mutates one).</summary>
        public KnowledgePath SeedPath(
            Guid subjectId, string? code = null, string version = "1.0", bool published = true,
            bool archived = false, string? language = "en", DateTimeOffset? from = null, DateTimeOffset? to = null,
            int activeSteps = 2)
        {
            var path = new KnowledgePath
            {
                TenantId = TenantId,
                PathCode = code ?? ("KP-" + Guid.NewGuid().ToString("N")[..6]),
                PathName = "Path",
                SubjectId = subjectId,
                Objective = "Objective",
                LanguageCode = language,
                PathVersion = version,
                PathStatus = archived ? KnowledgePathStatuses.Archived
                    : published ? KnowledgePathStatuses.Published : KnowledgePathStatuses.Draft,
                EffectiveFrom = from ?? Past,
                EffectiveTo = to,
                ArchivedAt = archived ? Past : null,
                StepSetFrozenAt = published ? Past : null
            };

            for (var i = 0; i < activeSteps; i++)
            {
                path.Steps.Add(new KnowledgePathStep
                {
                    StepOrder = (i + 1) * 10, StepCode = "S" + i, StepTitle = "Step", StepType = "core-message",
                    ContentId = Guid.NewGuid(), ContentCode = "KC-" + i, IsRequired = true
                });
            }

            Paths.Items.Add(path);
            return path;
        }

        public async Task<Guid> SeedJourney(Guid subjectId, string code = "J1", string version = "1.0")
        {
            var r = await CreateJourney().Handle(new CreateContentEngagementJourneyCommand(
                code, "Journey " + code, subjectId, "Objective", version, Past), default);
            Assert.True(r.StatusCode == 201, string.Join("; ", r.Errors ?? new List<string>()));
            return r.Data;
        }

        public Task<Diten.CrmService.Application.Common.Models.Response<Guid>> AddSimpleStage(
            Guid journeyId, int order, Guid pathId, bool required = true, bool repeatable = false,
            string? pin = null, string? stageType = null, string? advancementRule = null, Guid? fallback = null,
            int? minVisit = null, int? maxVisit = null,
            IReadOnlyList<ContentEngagementJourneyBranchConditionInput>? branches = null,
            int? expectedVersion = null, string? code = null)
            => AddStage().Handle(new AddContentEngagementJourneyStageCommand(
                journeyId, order, code ?? ("ST" + order), "Stage " + order, "Stage objective", pathId, required,
                repeatable, stageType, pin, minVisit, maxVisit, advancementRule, fallback, null, branches,
                expectedVersion), default);
    }

    // ---------------- happy paths (clusters 1 & 2) ----------------

    [Fact]
    public async Task Create_journey_returns_201()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var r = await fx.CreateJourney().Handle(new CreateContentEngagementJourneyCommand(
            "J1", "Journey", s, "Obj", "1.0", Past), default);
        Assert.Equal(201, r.StatusCode);
    }

    [Fact]
    public async Task Update_journey_returns_200()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var id = await fx.SeedJourney(s);
        var r = await fx.UpdateJourney().Handle(new UpdateContentEngagementJourneyCommand(
            id, "Renamed", s, "Obj2", "1.0", Past), default);
        Assert.True(r.IsSuccessful);
    }

    [Fact]
    public async Task Publish_journey_freezes_the_stage_set()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        await fx.AddSimpleStage(id, 10, path.Id);

        var r = await fx.PublishJourney().Handle(new PublishContentEngagementJourneyCommand(id), default);
        Assert.True(r.IsSuccessful);
        var stored = fx.Journeys.Items.Single(x => x.Id == id);
        Assert.True(stored.IsPublished());
        Assert.NotNull(stored.StageSetFrozenAt);
    }

    [Fact]
    public async Task New_version_returns_201_draft_clone()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        await fx.AddSimpleStage(id, 10, path.Id);
        await fx.PublishJourney().Handle(new PublishContentEngagementJourneyCommand(id), default);

        var r = await fx.NewVersion().Handle(new CreateContentEngagementJourneyVersionCommand(id), default);
        Assert.Equal(201, r.StatusCode);
        var clone = fx.Journeys.Items.Single(x => x.Id == r.Data);
        Assert.Equal(ContentEngagementJourneyStatuses.Draft, clone.JourneyStatus);
        Assert.Equal(id, clone.SupersedesJourneyId);
        Assert.Null(clone.StageSetFrozenAt);
    }

    [Fact]
    public async Task Archive_journey_is_idempotent()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var id = await fx.SeedJourney(s);
        Assert.True((await fx.ArchiveJourney().Handle(
            new ArchiveContentEngagementJourneyCommand(id), default)).IsSuccessful);
        Assert.True((await fx.ArchiveJourney().Handle(
            new ArchiveContentEngagementJourneyCommand(id), default)).IsSuccessful);
    }

    [Fact]
    public async Task Add_stage_happy_path_resolves_pinned_path_and_carries_path_code()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s, activeSteps: 3);
        var id = await fx.SeedJourney(s);

        var r = await fx.AddSimpleStage(id, 10, path.Id, pin: ContentEngagementJourneyPathPin.Pinned);
        Assert.Equal(201, r.StatusCode);

        var stages = await fx.GetStages().Handle(new GetContentEngagementJourneyStagesQuery(id), default);
        var stage = Assert.Single(stages.Data!.Items);
        Assert.Equal(path.Id, stage.ResolvedKnowledgePathId);
        Assert.Equal(path.PathCode, stage.PathCode);
        Assert.Equal(ContentEngagementJourneyPathResolutionStatuses.Pinned, stage.PathResolutionStatus);
        Assert.Equal(3, stage.ResolvedPathStepCount);
    }

    [Fact]
    public async Task Update_and_archive_stage()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        var stageId = (await fx.AddSimpleStage(id, 10, path.Id)).Data;

        var upd = await fx.UpdateStage().Handle(new UpdateContentEngagementJourneyStageCommand(
            id, stageId, 10, "ST10", "Renamed", "Obj", path.Id, true), default);
        Assert.True(upd.IsSuccessful);
        Assert.True((await fx.ArchiveStage().Handle(
            new ArchiveContentEngagementJourneyStageCommand(id, stageId), default)).IsSuccessful);
    }

    [Fact]
    public async Task Stage_read_order_is_deterministic()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        await fx.AddSimpleStage(id, 30, path.Id, code: "C");
        await fx.AddSimpleStage(id, 10, path.Id, code: "A");
        await fx.AddSimpleStage(id, 20, path.Id, code: "B");

        var first = await fx.GetStages().Handle(new GetContentEngagementJourneyStagesQuery(id), default);
        var second = await fx.GetStages().Handle(new GetContentEngagementJourneyStagesQuery(id), default);
        Assert.Equal(new[] { "A", "B", "C" }, first.Data!.Items.Select(i => i.StageCode));
        Assert.Equal(
            first.Data!.Items.Select(i => i.StageId), second.Data!.Items.Select(i => i.StageId));
    }

    [Fact]
    public async Task Journey_list_projects_counters_without_stages()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        await fx.AddSimpleStage(id, 10, path.Id);
        await fx.AddSimpleStage(id, 20, path.Id, required: false, repeatable: true, code: "ST20");

        var list = await fx.ListJourneys().Handle(new ListContentEngagementJourneysQuery(), default);
        var item = Assert.Single(list.Data!.Items);
        Assert.Equal(2, item.ActiveStageCount);
        Assert.Equal(1, item.RequiredStageCount);
        Assert.Equal(1, item.RepeatableStageCount);
    }

    [Fact]
    public async Task Journey_list_can_filter_by_knowledge_path()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var used = fx.SeedPath(s);
        var unused = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        await fx.AddSimpleStage(id, 10, used.Id);

        var hit = await fx.ListJourneys().Handle(
            new ListContentEngagementJourneysQuery(KnowledgePathId: used.Id), default);
        var miss = await fx.ListJourneys().Handle(
            new ListContentEngagementJourneysQuery(KnowledgePathId: unused.Id), default);
        Assert.Single(hit.Data!.Items);
        Assert.Empty(miss.Data!.Items);
    }

    // ---------------- AC-EMBED-1 / AC-EMBED-2 ----------------

    [Fact]
    public async Task Stage_write_goes_to_single_document_and_bumps_journey_version()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        var before = fx.Journeys.Items.Single().Version;

        await fx.AddSimpleStage(id, 10, path.Id);

        var stored = fx.Journeys.Items.Single();
        Assert.Single(fx.Journeys.Items);       // still one document
        Assert.Single(stored.Stages);           // stage embedded in the same document
        Assert.True(stored.Version > before);   // journey version bumped by the stage write
    }

    [Fact]
    public async Task Stage_has_no_own_tenant_or_version_members()
    {
        var members = typeof(ContentEngagementJourneyStage).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("TenantId", members);
        Assert.DoesNotContain("Version", members);
        Assert.DoesNotContain("JourneyId", members);
        Assert.False(typeof(ContentEngagementJourneyStage).IsSubclassOf(typeof(EntityBase)));
    }

    [Fact]
    public async Task Archived_stage_stays_in_document_and_is_visible_with_includeArchived()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        await fx.AddSimpleStage(id, 10, path.Id);
        var drop = (await fx.AddSimpleStage(id, 20, path.Id, required: false, code: "ST20")).Data;

        await fx.ArchiveStage().Handle(new ArchiveContentEngagementJourneyStageCommand(id, drop), default);

        var active = await fx.GetStages().Handle(new GetContentEngagementJourneyStagesQuery(id), default);
        Assert.Single(active.Data!.Items);
        var all = await fx.GetStages().Handle(
            new GetContentEngagementJourneyStagesQuery(id, IncludeArchived: true), default);
        Assert.Equal(2, all.Data!.Items.Count);
        Assert.Equal(2, fx.Journeys.Items.Single().Stages.Count); // nothing removed from the array
    }

    // ---------------- V-J duplicate / references ----------------

    [Fact]
    public async Task Duplicate_code_and_version_returns_409()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        await fx.SeedJourney(s, "J1", "1.0");
        var r = await fx.CreateJourney().Handle(new CreateContentEngagementJourneyCommand(
            "J1", "Other", s, "Obj", "1.0", Past), default);
        Assert.Equal(409, r.StatusCode);
    }

    [Fact]
    public async Task Archived_subject_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject(archived: true);
        var r = await fx.CreateJourney().Handle(new CreateContentEngagementJourneyCommand(
            "J1", "Journey", s, "Obj", "1.0", Past), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Topic_from_another_subject_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s1 = fx.SeedSubject();
        var s2 = fx.SeedSubject();
        var foreignTopic = fx.SeedTopic(s2);
        var r = await fx.CreateJourney().Handle(new CreateContentEngagementJourneyCommand(
            "J1", "Journey", s1, "Obj", "1.0", Past, TopicId: foreignTopic), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Archived_audience_profile_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var profile = fx.SeedProfile(archived: true);
        var r = await fx.CreateJourney().Handle(new CreateContentEngagementJourneyCommand(
            "J1", "Journey", s, "Obj", "1.0", Past, AudienceProfileId: profile), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Effective_to_before_from_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var r = await fx.CreateJourney().Handle(new CreateContentEngagementJourneyCommand(
            "J1", "Journey", s, "Obj", "1.0", Future, EffectiveTo: Past), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Unknown_status_or_source_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var badStatus = await fx.CreateJourney().Handle(new CreateContentEngagementJourneyCommand(
            "J1", "Journey", s, "Obj", "1.0", Past, JourneyStatus: "wobbly"), default);
        var badSource = await fx.CreateJourney().Handle(new CreateContentEngagementJourneyCommand(
            "J2", "Journey", s, "Obj", "1.0", Past, Source: "telepathy"), default);
        Assert.Equal(400, badStatus.StatusCode);
        Assert.Equal(400, badSource.StatusCode);
    }

    [Fact]
    public async Task Create_as_published_is_rejected()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var r = await fx.CreateJourney().Handle(new CreateContentEngagementJourneyCommand(
            "J1", "Journey", s, "Obj", "1.0", Past,
            JourneyStatus: ContentEngagementJourneyStatuses.Published), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Archived_journey_cannot_be_updated_or_take_stage_writes()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        await fx.ArchiveJourney().Handle(new ArchiveContentEngagementJourneyCommand(id), default);

        var upd = await fx.UpdateJourney().Handle(new UpdateContentEngagementJourneyCommand(
            id, "X", s, "Obj", "1.0", Past), default);
        var stage = await fx.AddSimpleStage(id, 10, path.Id);
        Assert.Equal(409, upd.StatusCode);
        Assert.Equal(409, stage.StatusCode);
    }

    [Fact]
    public async Task Stages_array_on_update_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var id = await fx.SeedJourney(s);
        var r = await fx.UpdateJourney().Handle(new UpdateContentEngagementJourneyCommand(
            id, "Journey", s, "Obj", "1.0", Past, StagesProvided: true), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Overlapping_second_published_version_returns_409()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);

        var v1 = await fx.SeedJourney(s, "J1", "1.0");
        await fx.AddSimpleStage(v1, 10, path.Id);
        Assert.True((await fx.PublishJourney().Handle(
            new PublishContentEngagementJourneyCommand(v1), default)).IsSuccessful);

        var v2 = await fx.SeedJourney(s, "J1", "2.0");
        await fx.AddSimpleStage(v2, 10, path.Id);
        var second = await fx.PublishJourney().Handle(new PublishContentEngagementJourneyCommand(v2), default);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Publish_without_required_stage_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        await fx.AddSimpleStage(id, 10, path.Id, required: false);

        var r = await fx.PublishJourney().Handle(new PublishContentEngagementJourneyCommand(id), default);
        Assert.Equal(400, r.StatusCode);
        Assert.Equal(ContentEngagementJourneyStatuses.Draft, fx.Journeys.Items.Single().JourneyStatus);
    }

    [Fact]
    public async Task Update_cannot_transition_to_published()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var id = await fx.SeedJourney(s);
        var r = await fx.UpdateJourney().Handle(new UpdateContentEngagementJourneyCommand(
            id, "Journey", s, "Obj", "1.0", Past,
            JourneyStatus: ContentEngagementJourneyStatuses.Published), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task New_version_on_a_draft_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var id = await fx.SeedJourney(s);
        var r = await fx.NewVersion().Handle(new CreateContentEngagementJourneyVersionCommand(id), default);
        Assert.Equal(400, r.StatusCode);
    }

    // ---------------- AC-FREEZE-1 / AC-FREEZE-2 ----------------

    [Fact]
    public async Task Published_journey_rejects_stage_add_update_and_archive()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        var stageId = (await fx.AddSimpleStage(id, 10, path.Id)).Data;
        await fx.PublishJourney().Handle(new PublishContentEngagementJourneyCommand(id), default);

        var add = await fx.AddSimpleStage(id, 20, path.Id, code: "ST20");
        var upd = await fx.UpdateStage().Handle(new UpdateContentEngagementJourneyStageCommand(
            id, stageId, 10, "ST10", "Renamed", "Obj", path.Id, true), default);
        var arch = await fx.ArchiveStage().Handle(
            new ArchiveContentEngagementJourneyStageCommand(id, stageId), default);

        Assert.Equal(409, add.StatusCode);
        Assert.Equal(409, upd.StatusCode);
        Assert.Equal(409, arch.StatusCode);
    }

    [Fact]
    public async Task Published_journey_rejects_field_changes_but_allows_lifecycle_move()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        await fx.AddSimpleStage(id, 10, path.Id);
        await fx.PublishJourney().Handle(new PublishContentEngagementJourneyCommand(id), default);

        var rename = await fx.UpdateJourney().Handle(new UpdateContentEngagementJourneyCommand(
            id, "Renamed", s, "Objective", "1.0", Past,
            JourneyStatus: ContentEngagementJourneyStatuses.Published), default);
        var deactivate = await fx.UpdateJourney().Handle(new UpdateContentEngagementJourneyCommand(
            id, "Journey J1", s, "Objective", "1.0", Past,
            JourneyStatus: ContentEngagementJourneyStatuses.Inactive), default);

        Assert.Equal(409, rename.StatusCode);
        Assert.True(deactivate.IsSuccessful);
    }

    [Fact]
    public async Task New_version_clone_gets_new_stage_ids_and_leaves_source_untouched()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s, "J1", "1.0");
        var stageId = (await fx.AddSimpleStage(id, 10, path.Id)).Data;
        await fx.PublishJourney().Handle(new PublishContentEngagementJourneyCommand(id), default);

        var r = await fx.NewVersion().Handle(new CreateContentEngagementJourneyVersionCommand(id), default);
        var clone = fx.Journeys.Items.Single(x => x.Id == r.Data);
        var source = fx.Journeys.Items.Single(x => x.Id == id);

        Assert.NotEqual("1.0", clone.JourneyVersion);
        Assert.Single(clone.Stages);
        Assert.NotEqual(stageId, clone.Stages[0].StageId);
        Assert.Equal(stageId, source.Stages[0].StageId);           // source untouched
        Assert.True(source.IsPublished());                          // source still published
        Assert.Equal(ContentEngagementJourneyStatuses.Draft, clone.JourneyStatus); // no auto-publish
    }

    [Fact]
    public async Task New_version_clone_remaps_fallback_and_branch_targets_onto_its_own_stage_ids()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s, "J1", "1.0");

        var first = (await fx.AddSimpleStage(id, 10, path.Id, code: "ST10")).Data;
        var second = (await fx.AddSimpleStage(
            id, 20, path.Id, code: "ST20", fallback: first,
            branches: new[] { new ContentEngagementJourneyBranchConditionInput("not-convinced", null, first) })).Data;
        await fx.PublishJourney().Handle(new PublishContentEngagementJourneyCommand(id), default);

        var r = await fx.NewVersion().Handle(new CreateContentEngagementJourneyVersionCommand(id), default);
        var clone = fx.Journeys.Items.Single(x => x.Id == r.Data);
        var cloneIds = clone.Stages.Select(x => x.StageId).ToHashSet();
        var clonedSecond = clone.Stages.Single(x => x.StageCode == "ST20");

        Assert.DoesNotContain(first, cloneIds);
        Assert.DoesNotContain(second, cloneIds);
        Assert.NotNull(clonedSecond.FallbackStageId);
        Assert.Contains(clonedSecond.FallbackStageId!.Value, cloneIds);          // remapped, not the old id
        Assert.Contains(clonedSecond.BranchConditions[0].TargetStageId!.Value, cloneIds);
        Assert.NotEqual(first, clonedSecond.FallbackStageId);
        Assert.NotEqual(first, clonedSecond.BranchConditions[0].TargetStageId);
    }

    // ---------------- V-S in-array uniqueness (handler is the only defence) ----------------

    [Fact]
    public async Task Duplicate_stage_order_returns_409()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        await fx.AddSimpleStage(id, 10, path.Id, code: "A");
        var r = await fx.AddSimpleStage(id, 10, path.Id, code: "B");
        Assert.Equal(409, r.StatusCode);
        Assert.Single(fx.Journeys.Items.Single().Stages);
    }

    [Fact]
    public async Task Duplicate_stage_code_returns_409()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        await fx.AddSimpleStage(id, 10, path.Id, code: "SAME");
        var r = await fx.AddSimpleStage(id, 20, path.Id, code: "same");
        Assert.Equal(409, r.StatusCode);
    }

    [Fact]
    public async Task Stage_order_freed_by_archive_can_be_reused()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        var stageId = (await fx.AddSimpleStage(id, 10, path.Id, code: "A")).Data;
        await fx.ArchiveStage().Handle(new ArchiveContentEngagementJourneyStageCommand(id, stageId), default);

        var r = await fx.AddSimpleStage(id, 10, path.Id, code: "B");
        Assert.Equal(201, r.StatusCode);
    }

    // ---------------- V-S05/S06/S07 path binding ----------------

    [Fact]
    public async Task Draft_path_cannot_be_bound_to_a_stage()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s, published: false);
        var id = await fx.SeedJourney(s);
        var r = await fx.AddSimpleStage(id, 10, path.Id);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Archived_path_cannot_be_bound_to_a_stage()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s, archived: true);
        var id = await fx.SeedJourney(s);
        var r = await fx.AddSimpleStage(id, 10, path.Id);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Not_yet_effective_path_cannot_be_bound_to_a_stage()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s, from: Future);
        var id = await fx.SeedJourney(s);
        var r = await fx.AddSimpleStage(id, 10, path.Id);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Unknown_path_id_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var id = await fx.SeedJourney(s);
        var r = await fx.AddSimpleStage(id, 10, Guid.NewGuid());
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Untouched_path_binding_is_not_revalidated_on_update()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        var stageId = (await fx.AddSimpleStage(id, 10, path.Id)).Data;

        // The bound path leaves the publishable state AFTER the stage was created (FU04 side, not ours).
        path.PathStatus = KnowledgePathStatuses.Inactive;

        var r = await fx.UpdateStage().Handle(new UpdateContentEngagementJourneyStageCommand(
            id, stageId, 10, "ST10", "Renamed", "New objective", path.Id, true), default);
        Assert.True(r.IsSuccessful); // dirty-check: the untouched binding is not re-validated
    }

    [Fact]
    public async Task Changing_the_binding_to_an_archived_path_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var good = fx.SeedPath(s);
        var archived = fx.SeedPath(s, archived: true);
        var id = await fx.SeedJourney(s);
        var stageId = (await fx.AddSimpleStage(id, 10, good.Id)).Data;

        var r = await fx.UpdateStage().Handle(new UpdateContentEngagementJourneyStageCommand(
            id, stageId, 10, "ST10", "Stage", "Obj", archived.Id, true), default);
        Assert.Equal(400, r.StatusCode);
    }

    // ---------------- V-S08 vocabulary (fail-closed) ----------------

    [Fact]
    public async Task Unknown_pin_policy_stage_type_or_advancement_rule_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);

        var badPin = await fx.AddSimpleStage(id, 10, path.Id, pin: "sticky", code: "A");
        var badType = await fx.AddSimpleStage(id, 20, path.Id, stageType: "vibes", code: "B");
        var badRule = await fx.AddSimpleStage(id, 30, path.Id, advancementRule: "telepathy", code: "C");

        Assert.Equal(400, badPin.StatusCode);
        Assert.Equal(400, badType.StatusCode);
        Assert.Equal(400, badRule.StatusCode);
    }

    [Fact]
    public async Task Declared_advancement_rule_is_stored_and_echoed_but_never_evaluated()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        await fx.AddSimpleStage(
            id, 10, path.Id, advancementRule: ContentEngagementJourneyAdvancementRules.ObjectionRecorded);

        var stages = await fx.GetStages().Handle(new GetContentEngagementJourneyStagesQuery(id), default);
        var stage = Assert.Single(stages.Data!.Items);
        Assert.Equal(ContentEngagementJourneyAdvancementRules.ObjectionRecorded, stage.AdvancementRule);

        // Nothing in the read model reports a current stage / progress / next stage.
        var members = typeof(ContentEngagementJourneyStageDto).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("CurrentStage", members);
        Assert.DoesNotContain("NextStageId", members);
        Assert.DoesNotContain("Progress", members);
    }

    // ---------------- V-S10 fallback / V-S15 branch (never evaluated) ----------------

    [Fact]
    public async Task Fallback_to_itself_or_to_a_foreign_journey_stage_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s, "J1");
        var other = await fx.SeedJourney(s, "J2");
        var foreignStage = (await fx.AddSimpleStage(other, 10, path.Id)).Data;
        var stageId = (await fx.AddSimpleStage(id, 10, path.Id)).Data;

        var self = await fx.UpdateStage().Handle(new UpdateContentEngagementJourneyStageCommand(
            id, stageId, 10, "ST10", "Stage", "Obj", path.Id, true, FallbackStageId: stageId), default);
        var foreign = await fx.UpdateStage().Handle(new UpdateContentEngagementJourneyStageCommand(
            id, stageId, 10, "ST10", "Stage", "Obj", path.Id, true, FallbackStageId: foreignStage), default);

        Assert.Equal(400, self.StatusCode);
        Assert.Equal(400, foreign.StatusCode);
    }

    [Fact]
    public async Task Fallback_may_point_backwards()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        var earlier = (await fx.AddSimpleStage(id, 10, path.Id, code: "A")).Data;
        var later = await fx.AddSimpleStage(id, 20, path.Id, code: "B", fallback: earlier);
        Assert.Equal(201, later.StatusCode);
    }

    [Fact]
    public async Task Branch_condition_target_outside_the_journey_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        var r = await fx.AddSimpleStage(id, 10, path.Id, branches: new[]
        {
            new ContentEngagementJourneyBranchConditionInput("asks-clinical-evidence", null, Guid.NewGuid())
        });
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Branch_condition_data_is_echoed_back_unchanged()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        var first = (await fx.AddSimpleStage(id, 10, path.Id, code: "A")).Data;
        await fx.AddSimpleStage(id, 20, path.Id, code: "B", branches: new[]
        {
            new ContentEngagementJourneyBranchConditionInput("price-objection", "Doctor pushed back", first)
        });

        var stages = await fx.GetStages().Handle(new GetContentEngagementJourneyStagesQuery(id), default);
        var stage = stages.Data!.Items.Single(x => x.StageCode == "B");
        var condition = Assert.Single(stage.BranchConditions);
        Assert.Equal("price-objection", condition.ConditionCode);
        Assert.Equal("Doctor pushed back", condition.Description);
        Assert.Equal(first, condition.TargetStageId);
    }

    [Fact]
    public async Task Journey_is_linearly_walkable_without_any_branch_or_fallback()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        await fx.AddSimpleStage(id, 10, path.Id, code: "A");
        await fx.AddSimpleStage(id, 20, path.Id, code: "B");
        await fx.AddSimpleStage(id, 30, path.Id, code: "C");

        var stages = await fx.GetStages().Handle(new GetContentEngagementJourneyStagesQuery(id), default);
        Assert.Equal(3, stages.Data!.Items.Count);
        Assert.All(stages.Data!.Items, x => Assert.Null(x.FallbackStageId));
        Assert.All(stages.Data!.Items, x => Assert.Empty(x.BranchConditions));
    }

    // ---------------- V-S11 visit range ----------------

    [Fact]
    public async Task Invalid_visit_range_returns_400_and_valid_range_is_accepted()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);

        var inverted = await fx.AddSimpleStage(id, 10, path.Id, minVisit: 3, maxVisit: 2, code: "A");
        var zero = await fx.AddSimpleStage(id, 20, path.Id, minVisit: 0, code: "B");
        var ok = await fx.AddSimpleStage(id, 30, path.Id, minVisit: 1, maxVisit: 3, code: "C");

        Assert.Equal(400, inverted.StatusCode);
        Assert.Equal(400, zero.StatusCode);
        Assert.Equal(201, ok.StatusCode);
    }

    // ---------------- AC-REPEAT-1 ----------------

    [Fact]
    public async Task Same_path_in_two_stages_is_allowed_and_visible_as_a_repeat()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        await fx.AddSimpleStage(id, 10, path.Id, code: "A");
        await fx.AddSimpleStage(id, 20, path.Id, code: "B", repeatable: true);

        var journey = await fx.GetJourney().Handle(new GetContentEngagementJourneyQuery(id), default);
        Assert.True(journey.Data!.HasRepeatedPaths);
        Assert.All(journey.Data!.Stages, x => Assert.Equal(2, x.PathUsageCountInJourney));
    }

    [Fact]
    public async Task Repeatable_defaults_to_false()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        await fx.AddSimpleStage(id, 10, path.Id);

        var stages = await fx.GetStages().Handle(new GetContentEngagementJourneyStagesQuery(id), default);
        Assert.False(Assert.Single(stages.Data!.Items).Repeatable);
    }

    // ---------------- cross subject / language visibility ----------------

    [Fact]
    public async Task Cross_subject_and_cross_language_stages_are_accepted_and_flagged()
    {
        var fx = new Fixture(TenantA);
        var journeySubject = fx.SeedSubject();
        var otherSubject = fx.SeedSubject();
        var path = fx.SeedPath(otherSubject, language: "de");
        var r = await fx.CreateJourney().Handle(new CreateContentEngagementJourneyCommand(
            "J1", "Journey", journeySubject, "Obj", "1.0", Past, LanguageCode: "en"), default);
        var id = r.Data;

        var add = await fx.AddSimpleStage(id, 10, path.Id);
        Assert.Equal(201, add.StatusCode);

        var stages = await fx.GetStages().Handle(new GetContentEngagementJourneyStagesQuery(id), default);
        var stage = Assert.Single(stages.Data!.Items);
        Assert.True(stage.IsCrossSubjectStage);
        Assert.True(stage.IsCrossLanguageStage);
    }

    // ---------------- V-S17 dangling references / V-S20 journey archive ----------------

    [Fact]
    public async Task Stage_used_as_fallback_cannot_be_archived()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        var target = (await fx.AddSimpleStage(id, 10, path.Id, code: "A")).Data;
        await fx.AddSimpleStage(id, 20, path.Id, code: "B", fallback: target);

        var r = await fx.ArchiveStage().Handle(
            new ArchiveContentEngagementJourneyStageCommand(id, target), default);
        Assert.Equal(409, r.StatusCode);
    }

    [Fact]
    public async Task Stage_used_as_branch_target_cannot_be_archived()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        var target = (await fx.AddSimpleStage(id, 10, path.Id, code: "A")).Data;
        await fx.AddSimpleStage(id, 20, path.Id, code: "B", branches: new[]
        {
            new ContentEngagementJourneyBranchConditionInput("not-convinced", null, target)
        });

        var r = await fx.ArchiveStage().Handle(
            new ArchiveContentEngagementJourneyStageCommand(id, target), default);
        Assert.Equal(409, r.StatusCode);
    }

    [Fact]
    public async Task Journey_archive_keeps_the_stages_in_the_same_document()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        await fx.AddSimpleStage(id, 10, path.Id);
        await fx.ArchiveJourney().Handle(new ArchiveContentEngagementJourneyCommand(id), default);

        var stored = fx.Journeys.Items.Single();
        Assert.True(stored.IsArchived());
        Assert.Single(stored.Stages); // no cascade write, nothing removed
        var read = await fx.GetJourney().Handle(new GetContentEngagementJourneyQuery(id), default);
        Assert.Single(read.Data!.Stages);
    }

    // ---------------- V-S18 limits ----------------

    [Fact]
    public async Task Stage_limit_is_enforced()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        var journey = fx.Journeys.Items.Single();
        for (var i = 0; i < ContentEngagementJourneyLimits.MaxStagesPerJourney; i++)
        {
            journey.Stages.Add(new ContentEngagementJourneyStage
            {
                StageOrder = 1000 + i, StageCode = "SEED" + i, StageName = "S", StageObjective = "O",
                RecommendedKnowledgePathId = path.Id, PathCode = path.PathCode
            });
        }

        var r = await fx.AddSimpleStage(id, 10, path.Id);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Branch_condition_limit_is_enforced()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        var conditions = Enumerable
            .Range(0, ContentEngagementJourneyLimits.MaxBranchConditionsPerStage + 1)
            .Select(i => new ContentEngagementJourneyBranchConditionInput("c" + i, null, null))
            .ToList();

        var r = await fx.AddSimpleStage(id, 10, path.Id, branches: conditions);
        Assert.Equal(400, r.StatusCode);
    }

    // ---------------- AC-PIN-1 resolution ----------------

    [Fact]
    public async Task Pinned_stage_does_not_follow_a_newer_published_path_version()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var v1 = fx.SeedPath(s, code: "KP-1", version: "1.0");
        var id = await fx.SeedJourney(s);
        await fx.AddSimpleStage(id, 10, v1.Id, pin: ContentEngagementJourneyPathPin.Pinned);

        fx.SeedPath(s, code: "KP-1", version: "2.0"); // FU04 publishes a newer version of the SAME code

        var stages = await fx.GetStages().Handle(new GetContentEngagementJourneyStagesQuery(id), default);
        var stage = Assert.Single(stages.Data!.Items);
        Assert.Equal(v1.Id, stage.ResolvedKnowledgePathId);
        Assert.Equal("1.0", stage.ResolvedPathVersion);
        Assert.Equal(ContentEngagementJourneyPathResolutionStatuses.Pinned, stage.PathResolutionStatus);
    }

    [Fact]
    public async Task Latest_published_stage_follows_the_newer_published_path_version()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var v1 = fx.SeedPath(s, code: "KP-1", version: "1.0");
        var id = await fx.SeedJourney(s);
        await fx.AddSimpleStage(id, 10, v1.Id, pin: ContentEngagementJourneyPathPin.LatestPublished);

        var v2 = fx.SeedPath(s, code: "KP-1", version: "2.0", from: Past.AddYears(1));

        var stages = await fx.GetStages().Handle(new GetContentEngagementJourneyStagesQuery(id), default);
        var stage = Assert.Single(stages.Data!.Items);
        Assert.Equal(v2.Id, stage.ResolvedKnowledgePathId);
        Assert.Equal("2.0", stage.ResolvedPathVersion);
        Assert.Equal(ContentEngagementJourneyPathResolutionStatuses.ResolvedLatest, stage.PathResolutionStatus);
    }

    [Fact]
    public async Task Unresolvable_latest_published_stage_is_visible_as_unresolved()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s, code: "KP-1");
        var id = await fx.SeedJourney(s);
        await fx.AddSimpleStage(id, 10, path.Id, pin: ContentEngagementJourneyPathPin.LatestPublished);

        path.PathStatus = KnowledgePathStatuses.Inactive; // the path leaves publication AFTER the stage was authored

        var stages = await fx.GetStages().Handle(new GetContentEngagementJourneyStagesQuery(id), default);
        var stage = Assert.Single(stages.Data!.Items);
        Assert.Equal(ContentEngagementJourneyPathResolutionStatuses.Unresolved, stage.PathResolutionStatus);
        Assert.Null(stage.ResolvedKnowledgePathId);

        var journey = await fx.GetJourney().Handle(new GetContentEngagementJourneyQuery(id), default);
        Assert.True(journey.Data!.HasUnresolvedStagePath); // surfaced, never hidden or dropped
    }

    [Fact]
    public async Task Stage_read_never_copies_the_path_steps()
    {
        var members = typeof(ContentEngagementJourneyStageDto).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("Steps", members);
        Assert.Contains("ResolvedPathStepCount", members); // only a counter
    }

    // ---------------- concurrency ----------------

    [Fact]
    public async Task Stale_expected_version_on_a_stage_write_returns_409()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        await fx.AddSimpleStage(id, 10, path.Id, code: "A");

        var stale = await fx.AddSimpleStage(id, 20, path.Id, code: "B", expectedVersion: 0);
        Assert.Equal(409, stale.StatusCode);
    }

    [Fact]
    public async Task Stale_expected_version_on_journey_update_returns_409()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var id = await fx.SeedJourney(s);
        await fx.AddSimpleStage(id, 10, path.Id); // bumps the shared token

        var r = await fx.UpdateJourney().Handle(new UpdateContentEngagementJourneyCommand(
            id, "Journey", s, "Obj", "1.0", Past, ExpectedVersion: 0), default);
        Assert.Equal(409, r.StatusCode);
    }

    // ---------------- tenant isolation ----------------

    [Fact]
    public async Task Another_tenants_journey_is_invisible_and_unwritable()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var id = await fx.SeedJourney(s);

        var read = await fx.GetJourney(TenantB).Handle(new GetContentEngagementJourneyQuery(id), default);
        var list = await fx.ListJourneys(TenantB).Handle(new ListContentEngagementJourneysQuery(), default);
        Assert.Equal(404, read.StatusCode);
        Assert.Empty(list.Data!.Items);
    }

    [Fact]
    public async Task Path_of_another_tenant_cannot_be_bound()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var id = await fx.SeedJourney(s);
        var foreignPath = new KnowledgePath
        {
            TenantId = TenantB, PathCode = "KP-X", PathName = "Foreign", SubjectId = Guid.NewGuid(),
            Objective = "O", PathVersion = "1.0", PathStatus = KnowledgePathStatuses.Published, EffectiveFrom = Past
        };
        fx.Paths.Items.Add(foreignPath);

        var r = await fx.AddSimpleStage(id, 10, foreignPath.Id);
        Assert.Equal(400, r.StatusCode);
    }

    // ---------------- contract ----------------

    [Fact]
    public async Task Contract_publishes_the_fourteen_flags_vocabulary_and_limits()
    {
        var handler = new GetContentEngagementJourneyContractHandler(Tenant(TenantA));
        var r = await handler.Handle(new GetContentEngagementJourneyContractQuery(), default);

        Assert.True(r.IsSuccessful);
        var dto = r.Data!;
        Assert.Equal("MOD-0162-FU05", dto.ModuleId);
        Assert.True(dto.Features.SupportsContentEngagementJourney);
        Assert.True(dto.Features.SupportsPublishedStageSetFreeze);
        Assert.True(dto.Features.SupportsStageKnowledgePathBinding);
        Assert.True(dto.Features.SupportsPathVersionPinPolicy);
        Assert.Equal(14, FlagNames().Count); // the fourteen documented flags — nothing else is advertised

        Assert.Equal(
            ContentEngagementJourneyStatuses.All, dto.Vocabularies.JourneyStatuses);
        Assert.Equal(
            ContentEngagementJourneyAdvancementRules.All, dto.Vocabularies.AdvancementRules);
        Assert.Equal(
            ContentEngagementJourneyLimits.MaxStagesPerJourney, dto.Limits.MaxStagesPerJourney);
        Assert.Equal(
            ContentEngagementJourneyLimits.MaxBranchConditionsPerStage, dto.Limits.MaxBranchConditionsPerStage);
        Assert.True(dto.Limits.StagesAreEmbeddedInJourneyDocument);
        Assert.Equal(ContentEngagementJourneyPermissions.All, dto.Permissions);
        Assert.NotEmpty(dto.ReasonCodes);
        Assert.NotEmpty(dto.Limitations);
    }

    /// <summary>Instance flag members only — the static <c>Current</c> factory is not a flag.</summary>
    private static List<string> FlagNames()
        => typeof(ContentEngagementJourneyFeatureFlags)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

    [Fact]
    public void Contract_never_advertises_a_forbidden_engine_flag()
    {
        var flags = FlagNames();
        var forbidden = new[]
        {
            "SupportsStageAdvancementEngine", "SupportsBranchEvaluator", "SupportsRecommendationEngine",
            "SupportsBestNextStage", "SupportsJourneyRuntimeProgress", "SupportsCurrentStageState",
            "SupportsJourneyTargetAssignment", "SupportsCompletionTracking", "SupportsDigitalDetailing",
            "SupportsVisitPlanning", "SupportsRoutePlanning", "SupportsCampaignEngine", "SupportsFrequencyEngine",
            "SupportsWorkflowApproval", "SupportsHardDelete"
        };
        foreach (var name in forbidden)
        {
            Assert.DoesNotContain(name, flags); // absent — not even as false
        }
    }

    [Fact]
    public void Journey_carries_no_campaign_brand_product_or_segment_member()
    {
        var members = typeof(JourneyEntity).GetProperties().Select(p => p.Name).ToList();
        foreach (var name in new[] { "CampaignId", "BrandId", "ProductId", "SegmentId" })
        {
            Assert.DoesNotContain(name, members);
        }

        var dtoMembers = typeof(ContentEngagementJourneyDto).GetProperties().Select(p => p.Name).ToList();
        foreach (var name in new[] { "CampaignId", "BrandId", "ProductId", "SegmentId", "CurrentStageId", "Progress" })
        {
            Assert.DoesNotContain(name, dtoMembers);
        }
    }

    [Fact]
    public void Permission_keys_use_the_canonical_content_engagement_journey_form()
    {
        Assert.Equal("crm.knowledge.content-engagement-journey.read", ContentEngagementJourneyPermissions.Read);
        Assert.Equal("crm.knowledge.content-engagement-journey.manage", ContentEngagementJourneyPermissions.Manage);
        Assert.Equal("crm.knowledge.content-engagement-journey.publish", ContentEngagementJourneyPermissions.Publish);
        Assert.Equal(3, ContentEngagementJourneyPermissions.All.Count); // no stage-level key (S2)
    }

    // ---------------- reader seam ----------------

    [Fact]
    public async Task Reader_returns_only_published_effective_journeys_with_active_stages()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var published = await fx.SeedJourney(s, "J1");
        await fx.AddSimpleStage(published, 10, path.Id, code: "A");
        var archivedStage = (await fx.AddSimpleStage(published, 20, path.Id, code: "B", required: false)).Data;
        await fx.ArchiveStage().Handle(
            new ArchiveContentEngagementJourneyStageCommand(published, archivedStage), default);
        await fx.PublishJourney().Handle(new PublishContentEngagementJourneyCommand(published), default);

        var draft = await fx.SeedJourney(s, "J2");
        await fx.AddSimpleStage(draft, 10, path.Id);

        var journeys = await fx.Reader().ResolvePublishedJourneysAsync(
            new ContentEngagementJourneyCriteria(SubjectId: s), default);
        Assert.Single(journeys);
        Assert.Equal(published, journeys[0].JourneyId);

        var stages = await fx.Reader().GetOrderedStagesAsync(published, DateTimeOffset.UtcNow, default);
        Assert.Single(stages); // archived stage never reaches a consumer
        Assert.Equal("A", stages[0].StageCode);
    }

    [Fact]
    public async Task Reader_returns_empty_for_a_draft_journey_and_invents_no_default()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var path = fx.SeedPath(s);
        var draft = await fx.SeedJourney(s);
        await fx.AddSimpleStage(draft, 10, path.Id);

        Assert.Empty(await fx.Reader().GetOrderedStagesAsync(draft, DateTimeOffset.UtcNow, default));
        Assert.Empty(await fx.Reader().ResolvePublishedJourneysAsync(
            new ContentEngagementJourneyCriteria(SubjectId: Guid.NewGuid()), default));
    }

    [Fact]
    public void Reader_seam_exposes_no_engine_method()
    {
        var methods = typeof(IContentEngagementJourneyReader).GetMethods().Select(m => m.Name).ToList();
        Assert.Equal(
            new[] { "ResolvePublishedJourneysAsync", "GetOrderedStagesAsync" }.OrderBy(x => x),
            methods.OrderBy(x => x));
    }

    // ---------------- fakes ----------------

    private sealed class FakeJourneyRepo : IContentEngagementJourneyRepository
    {
        public List<JourneyEntity> Items { get; } = new();

        public Task<JourneyEntity?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));

        public Task<IReadOnlyList<JourneyEntity>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<JourneyEntity>)Items
                .Where(x => x.TenantId == t && !x.IsDeleted).ToList());

        public Task<IReadOnlyList<JourneyEntity>> ListByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<JourneyEntity>)Items
                .Where(x => x.TenantId == t && !x.IsDeleted && x.JourneyCode == code).ToList());

        public Task InsertAsync(JourneyEntity e, CancellationToken ct)
        {
            Items.Add(e);
            return Task.CompletedTask;
        }

        public Task<bool> ReplaceAsync(JourneyEntity e, int expectedVersion, CancellationToken ct)
        {
            var stored = Items.FirstOrDefault(x => x.Id == e.Id && x.TenantId == e.TenantId);
            if (stored is null || stored.Version != expectedVersion)
            {
                return Task.FromResult(false);
            }

            e.Version = expectedVersion + 1; // in-place fake: stored IS e, so this bumps the stored version too
            return Task.FromResult(true);
        }
    }

    private sealed class FakePathRepo : IKnowledgePathRepository
    {
        public List<KnowledgePath> Items { get; } = new();

        public Task<KnowledgePath?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));

        public Task<IReadOnlyList<KnowledgePath>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<KnowledgePath>)Items
                .Where(x => x.TenantId == t && !x.IsDeleted).ToList());

        public Task<IReadOnlyList<KnowledgePath>> ListByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<KnowledgePath>)Items
                .Where(x => x.TenantId == t && !x.IsDeleted && x.PathCode == code).ToList());

        public Task InsertAsync(KnowledgePath e, CancellationToken ct)
            => throw new InvalidOperationException("FU05 never writes a KnowledgePath.");

        public Task<bool> ReplaceAsync(KnowledgePath e, int expectedVersion, CancellationToken ct)
            => throw new InvalidOperationException("FU05 never writes a KnowledgePath.");
    }

    private sealed class FakeSubjectRepo : ISubjectRepository
    {
        public List<Subject> Items { get; } = new();
        public Task<Subject?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));
        public Task<IReadOnlyList<Subject>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Subject>)Items.Where(x => x.TenantId == t && !x.IsDeleted).ToList());
        public Task<Subject?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x =>
                x.TenantId == t && !x.IsDeleted && x.SubjectCode == code && !x.IsArchived()));
        public Task InsertAsync(Subject e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(Subject e, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeTopicRepo : ITopicRepository
    {
        public List<Topic> Items { get; } = new();
        public Task<Topic?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));
        public Task<IReadOnlyList<Topic>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Topic>)Items.Where(x => x.TenantId == t && !x.IsDeleted).ToList());
        public Task<IReadOnlyList<Topic>> ListBySubjectAsync(Guid t, Guid s, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Topic>)Items
                .Where(x => x.TenantId == t && !x.IsDeleted && x.SubjectId == s).ToList());
        public Task<Topic?> GetActiveByCodeAsync(Guid t, Guid s, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x =>
                x.TenantId == t && !x.IsDeleted && x.SubjectId == s && x.TopicCode == code && !x.IsArchived()));
        public Task InsertAsync(Topic e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(Topic e, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeProfileRepo : IAudienceProfileRepository
    {
        public List<AudienceProfile> Items { get; } = new();
        public Task<AudienceProfile?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));
        public Task<IReadOnlyList<AudienceProfile>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<AudienceProfile>)Items
                .Where(x => x.TenantId == t && !x.IsDeleted).ToList());
        public Task<AudienceProfile?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x =>
                x.TenantId == t && !x.IsDeleted && x.ProfileCode == code && !x.IsArchived()));
        public Task InsertAsync(AudienceProfile e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(AudienceProfile e, CancellationToken ct) => Task.CompletedTask;
    }
}
