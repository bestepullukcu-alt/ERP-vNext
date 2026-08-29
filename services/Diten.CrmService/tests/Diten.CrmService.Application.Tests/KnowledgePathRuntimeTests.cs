using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Knowledge.Path;
using Diten.CrmService.Application.Features.Knowledge.Path.Commands;
using Diten.CrmService.Application.Features.Knowledge.Path.Contract;
using Diten.CrmService.Application.Features.Knowledge.Path.Handlers;
using Diten.CrmService.Application.Features.Knowledge.Path.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// MOD-0162 FU04 — KnowledgePath runtime tests (D2 = embedded steps). In-memory fakes; the path repo mutates in place
/// (ReplaceAsync bumps Version and returns matched). Covers happy paths, the embedded-model invariants (AC-EMBED-1/2),
/// V-P/V-S rules, freeze + new-version (AC-FREEZE-1), content resolution (AC-PIN-1), the D6 assessment rule, the D7
/// branch data, tenant isolation, concurrency, the contract flags and the read-only reader seam.
/// </summary>
public sealed class KnowledgePathRuntimeTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
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
        public FakePathRepo Paths { get; } = new();
        public FakeSubjectRepo Subjects { get; } = new();
        public FakeTopicRepo Topics { get; } = new();
        public FakeProfileRepo Profiles { get; } = new();
        public FakeContentRepo Contents { get; } = new();
        public FakeNodeRepo Nodes { get; } = new();
        public Guid TenantId { get; }

        public Fixture(Guid tenant) => TenantId = tenant;

        public CreateKnowledgePathHandler CreatePath()
            => new(Tenant(TenantId), new NullActorContext(), Paths, Subjects, Topics, Profiles);
        public UpdateKnowledgePathHandler UpdatePath()
            => new(Tenant(TenantId), new NullActorContext(), Paths, Subjects, Topics, Profiles);
        public PublishKnowledgePathHandler PublishPath()
            => new(Tenant(TenantId), new NullActorContext(), Paths);
        public CreateKnowledgePathVersionHandler NewVersion()
            => new(Tenant(TenantId), new NullActorContext(), Paths);
        public ArchiveKnowledgePathHandler ArchivePath()
            => new(Tenant(TenantId), new NullActorContext(), Paths);
        public AddKnowledgePathStepHandler AddStep()
            => new(Tenant(TenantId), new NullActorContext(), Paths, Contents, Nodes);
        public UpdateKnowledgePathStepHandler UpdateStep()
            => new(Tenant(TenantId), new NullActorContext(), Paths, Contents, Nodes);
        public ArchiveKnowledgePathStepHandler ArchiveStep()
            => new(Tenant(TenantId), new NullActorContext(), Paths);
        public GetKnowledgePathHandler GetPath()
            => new(Tenant(TenantId), Paths, Contents, Nodes);
        public GetKnowledgePathStepsHandler GetSteps(Guid? t = null)
            => new(Tenant(t ?? TenantId), Paths, Contents, Nodes);
        public ListKnowledgePathsHandler ListPaths(Guid? t = null)
            => new(Tenant(t ?? TenantId), Paths, Contents, Nodes);
        public KnowledgePathReader Reader()
            => new(Tenant(TenantId), Paths, Contents, Nodes);

        public Guid SeedSubject(bool archived = false)
        {
            var s = new Subject
            {
                TenantId = TenantId, SubjectCode = "SUB-" + Guid.NewGuid().ToString("N")[..6],
                SubjectName = "Subject", Status = archived ? TaxonomyStatuses.Archived : TaxonomyStatuses.Active,
                EffectiveFrom = Jan1, ArchivedAt = archived ? Jan1 : null
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
                EffectiveFrom = Jan1, ArchivedAt = archived ? Jan1 : null
            };
            Topics.Items.Add(t);
            return t.Id;
        }

        public Guid SeedProfile(bool archived = false)
        {
            var p = new AudienceProfile
            {
                TenantId = TenantId, ProfileCode = "AP-" + Guid.NewGuid().ToString("N")[..6], ProfileName = "P",
                Status = archived ? TaxonomyStatuses.Archived : TaxonomyStatuses.Active, EffectiveFrom = Jan1,
                ArchivedAt = archived ? Jan1 : null
            };
            Profiles.Items.Add(p);
            return p.Id;
        }

        public Guid SeedContent(
            Guid subjectId, string? code = null, string type = "presentation", bool archived = false,
            bool published = true, string version = "1.0", string language = "en",
            DateTimeOffset? from = null, DateTimeOffset? to = null)
        {
            var c = new KnowledgeContent
            {
                TenantId = TenantId, ContentCode = code ?? ("KC-" + Guid.NewGuid().ToString("N")[..6]),
                ContentTitle = "Content", ContentType = type,
                ContentStatus = archived ? KnowledgeContentStatuses.Archived
                    : published ? KnowledgeContentStatuses.Published : KnowledgeContentStatuses.Draft,
                SubjectId = subjectId, LanguageCode = language, ContentVersion = version,
                EffectiveFrom = from ?? Jan1, EffectiveTo = to, Url = "https://x.test",
                ArchivedAt = archived ? Jan1 : null
            };
            Contents.Items.Add(c);
            return c.Id;
        }

        public Guid SeedNode(Guid subjectId, bool archived = false)
        {
            var n = new ConceptNode
            {
                TenantId = TenantId, SubjectId = subjectId, ConceptTypeId = Guid.NewGuid(),
                ConceptNodeCode = "N-" + Guid.NewGuid().ToString("N")[..6], ConceptNodeName = "Node",
                Status = archived ? ConceptStatuses.Archived : ConceptStatuses.Active, EffectiveFrom = Jan1,
                ArchivedAt = archived ? Jan1 : null
            };
            Nodes.Items.Add(n);
            return n.Id;
        }

        public async Task<Guid> SeedPath(Guid subjectId, string code = "P1", string version = "1.0")
        {
            var r = await CreatePath().Handle(new CreateKnowledgePathCommand(
                code, "Path " + code, subjectId, "Objective", version, Jan1), default);
            Assert.True(r.StatusCode == 201, string.Join("; ", r.Errors ?? new List<string>()));
            return r.Data;
        }

        public Task<Diten.CrmService.Application.Common.Models.Response<Guid>> AddSimpleStep(
            Guid pathId, int order, Guid contentId, bool required = true, string type = "core-message",
            string? pin = null, string? rule = null, Guid? prereq = null, Guid? node = null, int? duration = null,
            IReadOnlyList<KnowledgePathBranchConditionInput>? branches = null, int? expectedVersion = null,
            string? code = null)
            => AddStep().Handle(new AddKnowledgePathStepCommand(
                pathId, order, code ?? ("S" + order), "Step " + order, type, contentId, required, pin, rule, prereq,
                node, duration, null, branches, expectedVersion), default);
    }

    // ---------------- happy paths (cluster 1 & 2) ----------------

    [Fact]
    public async Task Create_path_returns_201()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var r = await fx.CreatePath().Handle(new CreateKnowledgePathCommand("P1", "Path", s, "Obj", "1.0", Jan1), default);
        Assert.Equal(201, r.StatusCode);
    }

    [Fact]
    public async Task Update_path_returns_200()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var id = await fx.SeedPath(s);
        var r = await fx.UpdatePath().Handle(new UpdateKnowledgePathCommand(
            id, "Renamed", s, "Obj2", "1.0", Jan1), default);
        Assert.True(r.IsSuccessful);
    }

    [Fact]
    public async Task Archive_path_is_idempotent()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var id = await fx.SeedPath(s);
        Assert.True((await fx.ArchivePath().Handle(new ArchiveKnowledgePathCommand(id), default)).IsSuccessful);
        Assert.True((await fx.ArchivePath().Handle(new ArchiveKnowledgePathCommand(id), default)).IsSuccessful);
    }

    [Fact]
    public async Task Add_step_happy_path_returns_201_and_resolves_content()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var content = fx.SeedContent(s);
        var id = await fx.SeedPath(s);
        var r = await fx.AddSimpleStep(id, 10, content, pin: "pinned");
        Assert.Equal(201, r.StatusCode);

        var steps = await fx.GetSteps().Handle(new GetKnowledgePathStepsQuery(id), default);
        var step = Assert.Single(steps.Data!.Items);
        Assert.Equal(content, step.ResolvedContentId);
        Assert.Equal(KnowledgePathContentResolutionStatuses.Pinned, step.ContentResolutionStatus);
    }

    [Fact]
    public async Task Update_and_archive_step()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var content = fx.SeedContent(s);
        var id = await fx.SeedPath(s);
        var stepId = (await fx.AddSimpleStep(id, 10, content)).Data;
        var upd = await fx.UpdateStep().Handle(new UpdateKnowledgePathStepCommand(
            id, stepId, 10, "S10", "Renamed", "core-message", content, true), default);
        Assert.True(upd.IsSuccessful);
        Assert.True((await fx.ArchiveStep().Handle(
            new ArchiveKnowledgePathStepCommand(id, stepId), default)).IsSuccessful);
    }

    // ---------------- AC-EMBED-1 / AC-EMBED-2 ----------------

    [Fact]
    public async Task Step_write_goes_to_single_document_and_bumps_path_version()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var content = fx.SeedContent(s);
        var id = await fx.SeedPath(s);
        var before = fx.Paths.Items.Single().Version;
        await fx.AddSimpleStep(id, 10, content);
        var stored = fx.Paths.Items.Single();
        Assert.Single(fx.Paths.Items);           // still one document
        Assert.Single(stored.Steps);             // step embedded in the same document
        Assert.True(stored.Version > before);    // path version bumped by the step write
    }

    [Fact]
    public async Task Archived_step_stays_in_document_and_is_visible_with_includeArchived()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var content = fx.SeedContent(s);
        var id = await fx.SeedPath(s);
        var keep = (await fx.AddSimpleStep(id, 10, content)).Data;
        var drop = (await fx.AddSimpleStep(id, 20, content, required: false)).Data;
        await fx.ArchiveStep().Handle(new ArchiveKnowledgePathStepCommand(id, drop), default);

        var active = await fx.GetSteps().Handle(new GetKnowledgePathStepsQuery(id), default);
        Assert.Single(active.Data!.Items);
        var all = await fx.GetSteps().Handle(new GetKnowledgePathStepsQuery(id, IncludeArchived: true), default);
        Assert.Equal(2, all.Data!.Items.Count);
        Assert.Equal(2, fx.Paths.Items.Single().Steps.Count); // nothing removed from the array
    }

    // ---------------- V-P duplicate / references ----------------

    [Fact]
    public async Task Duplicate_code_and_version_returns_409()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        await fx.SeedPath(s, "DUP", "1.0");
        var second = await fx.CreatePath().Handle(new CreateKnowledgePathCommand(
            "DUP", "Path", s, "Obj", "1.0", Jan1), default);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Same_code_different_version_is_allowed()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        await fx.SeedPath(s, "C", "1.0");
        var second = await fx.CreatePath().Handle(new CreateKnowledgePathCommand(
            "C", "Path", s, "Obj", "2.0", Jan1), default);
        Assert.Equal(201, second.StatusCode);
    }

    [Fact]
    public async Task Archived_subject_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject(archived: true);
        var r = await fx.CreatePath().Handle(new CreateKnowledgePathCommand("P", "Path", s, "Obj", "1.0", Jan1), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Topic_not_in_subject_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var other = fx.SeedSubject();
        var topic = fx.SeedTopic(other);
        var r = await fx.CreatePath().Handle(new CreateKnowledgePathCommand(
            "P", "Path", s, "Obj", "1.0", Jan1, TopicId: topic), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Archived_profile_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var profile = fx.SeedProfile(archived: true);
        var r = await fx.CreatePath().Handle(new CreateKnowledgePathCommand(
            "P", "Path", s, "Obj", "1.0", Jan1, AudienceProfileId: profile), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Effective_to_before_from_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var r = await fx.CreatePath().Handle(new CreateKnowledgePathCommand(
            "P", "Path", s, "Obj", "1.0", Jun1, EffectiveTo: Jan1), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Unknown_status_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var r = await fx.CreatePath().Handle(new CreateKnowledgePathCommand(
            "P", "Path", s, "Obj", "1.0", Jan1, PathStatus: "made-up"), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Update_with_steps_array_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var id = await fx.SeedPath(s);
        var r = await fx.UpdatePath().Handle(new UpdateKnowledgePathCommand(
            id, "Path", s, "Obj", "1.0", Jan1, StepsProvided: true), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Archived_path_update_returns_409()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var id = await fx.SeedPath(s);
        await fx.ArchivePath().Handle(new ArchiveKnowledgePathCommand(id), default);
        var r = await fx.UpdatePath().Handle(new UpdateKnowledgePathCommand(id, "X", s, "Obj", "1.0", Jan1), default);
        Assert.Equal(409, r.StatusCode);
    }

    // ---------------- publish / version (D4/D5) ----------------

    [Fact]
    public async Task Empty_path_publish_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var id = await fx.SeedPath(s);
        var r = await fx.PublishPath().Handle(new PublishKnowledgePathCommand(id), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Update_to_published_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var id = await fx.SeedPath(s);
        var r = await fx.UpdatePath().Handle(new UpdateKnowledgePathCommand(
            id, "Path", s, "Obj", "1.0", Jan1, PathStatus: "published"), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Publish_with_required_step_succeeds_and_freezes()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var content = fx.SeedContent(s);
        var id = await fx.SeedPath(s);
        await fx.AddSimpleStep(id, 10, content, required: true);
        var r = await fx.PublishPath().Handle(new PublishKnowledgePathCommand(id), default);
        Assert.True(r.IsSuccessful);
        Assert.NotNull(fx.Paths.Items.Single().StepSetFrozenAt);
    }

    [Fact]
    public async Task New_version_source_not_published_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var id = await fx.SeedPath(s);
        var r = await fx.NewVersion().Handle(new CreateKnowledgePathVersionCommand(id), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Overlapping_second_published_returns_409()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var content = fx.SeedContent(s);
        var a = await fx.SeedPath(s, "OV", "1.0");
        await fx.AddSimpleStep(a, 10, content, required: true);
        Assert.True((await fx.PublishPath().Handle(new PublishKnowledgePathCommand(a), default)).IsSuccessful);

        var b = await fx.SeedPath(s, "OV", "2.0");
        await fx.AddSimpleStep(b, 10, content, required: true);
        var second = await fx.PublishPath().Handle(new PublishKnowledgePathCommand(b), default);
        Assert.Equal(409, second.StatusCode);
    }

    // ---------------- AC-FREEZE-1 ----------------

    [Fact]
    public async Task Frozen_path_step_add_returns_409()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var content = fx.SeedContent(s);
        var id = await fx.SeedPath(s);
        await fx.AddSimpleStep(id, 10, content, required: true);
        await fx.PublishPath().Handle(new PublishKnowledgePathCommand(id), default);
        var r = await fx.AddSimpleStep(id, 20, content);
        Assert.Equal(409, r.StatusCode);
    }

    [Fact]
    public async Task Frozen_path_step_update_and_archive_return_409()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var content = fx.SeedContent(s);
        var id = await fx.SeedPath(s);
        var stepId = (await fx.AddSimpleStep(id, 10, content, required: true)).Data;
        await fx.PublishPath().Handle(new PublishKnowledgePathCommand(id), default);
        var upd = await fx.UpdateStep().Handle(new UpdateKnowledgePathStepCommand(
            id, stepId, 10, "S10", "X", "core-message", content, true), default);
        Assert.Equal(409, upd.StatusCode);
        var arc = await fx.ArchiveStep().Handle(new ArchiveKnowledgePathStepCommand(id, stepId), default);
        Assert.Equal(409, arc.StatusCode);
    }

    [Fact]
    public async Task New_version_clones_with_new_step_ids_and_provenance()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var content = fx.SeedContent(s);
        var id = await fx.SeedPath(s, "NV", "1.0");
        var sourceStepId = (await fx.AddSimpleStep(id, 10, content, required: true)).Data;
        await fx.PublishPath().Handle(new PublishKnowledgePathCommand(id), default);

        var r = await fx.NewVersion().Handle(new CreateKnowledgePathVersionCommand(id), default);
        Assert.Equal(201, r.StatusCode);
        var clone = fx.Paths.Items.Single(p => p.Id == r.Data);
        Assert.Equal(KnowledgePathStatuses.Draft, clone.PathStatus);
        Assert.Equal(id, clone.SupersedesPathId);
        Assert.NotEqual("1.0", clone.PathVersion);
        Assert.Null(clone.StepSetFrozenAt);
        Assert.NotEqual(sourceStepId, clone.Steps.Single().StepId); // new StepId
        // source unchanged (still published, still frozen)
        Assert.True(fx.Paths.Items.Single(p => p.Id == id).IsPublished());
    }

    // ---------------- step-set rules (V-S03/04/05/06) ----------------

    [Fact]
    public async Task Duplicate_step_order_returns_409_without_db_index()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var content = fx.SeedContent(s);
        var id = await fx.SeedPath(s);
        await fx.AddSimpleStep(id, 10, content);
        var dup = await fx.AddSimpleStep(id, 10, content, required: false, code: "SX");
        Assert.Equal(409, dup.StatusCode);
    }

    [Fact]
    public async Task Duplicate_step_code_returns_409()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var content = fx.SeedContent(s);
        var id = await fx.SeedPath(s);
        await fx.AddSimpleStep(id, 10, content, code: "SAME");
        var dup = await fx.AddSimpleStep(id, 20, content, required: false, code: "SAME");
        Assert.Equal(409, dup.StatusCode);
    }

    [Fact]
    public async Task Step_referencing_archived_content_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var content = fx.SeedContent(s, archived: true);
        var id = await fx.SeedPath(s);
        var r = await fx.AddSimpleStep(id, 10, content);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Step_referencing_unpublished_content_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var content = fx.SeedContent(s, published: false);
        var id = await fx.SeedPath(s);
        var r = await fx.AddSimpleStep(id, 10, content);
        Assert.Equal(400, r.StatusCode);
    }

    // ---------------- V-S07 dirty-check ----------------

    [Fact]
    public async Task Update_step_without_changing_content_succeeds_even_if_content_archives()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var content = fx.SeedContent(s);
        var id = await fx.SeedPath(s);
        var stepId = (await fx.AddSimpleStep(id, 10, content)).Data;
        // Content later archives — an untouched ContentId must not trip a 400 on save.
        fx.Contents.Items.Single(c => c.Id == content).ArchivedAt = Jan1;
        fx.Contents.Items.Single(c => c.Id == content).ContentStatus = KnowledgeContentStatuses.Archived;
        var r = await fx.UpdateStep().Handle(new UpdateKnowledgePathStepCommand(
            id, stepId, 10, "S10", "Edited", "core-message", content, true), default);
        Assert.True(r.IsSuccessful);
    }

    [Fact]
    public async Task Update_step_changing_to_archived_content_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var good = fx.SeedContent(s);
        var bad = fx.SeedContent(s, archived: true);
        var id = await fx.SeedPath(s);
        var stepId = (await fx.AddSimpleStep(id, 10, good)).Data;
        var r = await fx.UpdateStep().Handle(new UpdateKnowledgePathStepCommand(
            id, stepId, 10, "S10", "Edited", "core-message", bad, true), default);
        Assert.Equal(400, r.StatusCode);
    }

    // ---------------- prerequisite (V-S09/S10) ----------------

    [Fact]
    public async Task Prerequisite_forward_order_is_accepted()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var content = fx.SeedContent(s);
        var id = await fx.SeedPath(s);
        var first = (await fx.AddSimpleStep(id, 10, content)).Data;
        var second = await fx.AddSimpleStep(id, 20, content, prereq: first);
        Assert.Equal(201, second.StatusCode);
    }

    [Fact]
    public async Task Prerequisite_pointing_forward_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var content = fx.SeedContent(s);
        var id = await fx.SeedPath(s);
        var later = (await fx.AddSimpleStep(id, 30, content)).Data;
        var r = await fx.AddSimpleStep(id, 10, content, required: false, code: "SE", prereq: later);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Required_step_on_optional_prerequisite_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var content = fx.SeedContent(s);
        var id = await fx.SeedPath(s);
        var optional = (await fx.AddSimpleStep(id, 10, content, required: false)).Data;
        var r = await fx.AddSimpleStep(id, 20, content, required: true, prereq: optional);
        Assert.Equal(400, r.StatusCode);
    }

    // ---------------- duration & assessment (V-S11 / V-S12 = D6) ----------------

    [Fact]
    public async Task Duration_met_without_minutes_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var content = fx.SeedContent(s);
        var id = await fx.SeedPath(s);
        var r = await fx.AddSimpleStep(id, 10, content, rule: "duration-met");
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Assessment_passed_with_quiz_content_is_accepted()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var quiz = fx.SeedContent(s, type: "quiz");
        var id = await fx.SeedPath(s);
        var r = await fx.AddSimpleStep(id, 10, quiz, rule: "assessment-passed", type: "quiz");
        Assert.Equal(201, r.StatusCode);
    }

    [Fact]
    public async Task Assessment_passed_with_non_quiz_content_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var brochure = fx.SeedContent(s, type: "brochure");
        var id = await fx.SeedPath(s);
        var r = await fx.AddSimpleStep(id, 10, brochure, rule: "assessment-passed");
        Assert.Equal(400, r.StatusCode);
    }

    // ---------------- concept node (V-S13) ----------------

    [Fact]
    public async Task Step_with_live_node_is_accepted()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var content = fx.SeedContent(s);
        var node = fx.SeedNode(s);
        var id = await fx.SeedPath(s);
        var r = await fx.AddSimpleStep(id, 10, content, node: node);
        Assert.Equal(201, r.StatusCode);
    }

    [Fact]
    public async Task Step_with_archived_node_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var content = fx.SeedContent(s);
        var node = fx.SeedNode(s, archived: true);
        var id = await fx.SeedPath(s);
        var r = await fx.AddSimpleStep(id, 10, content, node: node);
        Assert.Equal(400, r.StatusCode);
    }

    // ---------------- branch conditions (D7 / V-S14) ----------------

    [Fact]
    public async Task Branch_condition_data_is_echoed_and_never_evaluated()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var content = fx.SeedContent(s);
        var id = await fx.SeedPath(s);
        var branches = new List<KnowledgePathBranchConditionInput>
        {
            new("price-objection", "Handle price pushback", null)
        };
        var stepId = (await fx.AddSimpleStep(id, 10, content, branches: branches)).Data;
        var steps = await fx.GetSteps().Handle(new GetKnowledgePathStepsQuery(id), default);
        var branch = Assert.Single(steps.Data!.Items.Single(x => x.StepId == stepId).BranchConditions);
        Assert.Equal("price-objection", branch.ConditionCode);
    }

    [Fact]
    public async Task Branch_target_in_foreign_path_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var content = fx.SeedContent(s);
        var id = await fx.SeedPath(s);
        var branches = new List<KnowledgePathBranchConditionInput>
        {
            new("cond", null, Guid.NewGuid()) // target not a step in this path
        };
        var r = await fx.AddSimpleStep(id, 10, content, branches: branches);
        Assert.Equal(400, r.StatusCode);
    }

    // ---------------- cross-subject / language visibility (V-S15) ----------------

    [Fact]
    public async Task Cross_subject_step_is_accepted_and_flagged()
    {
        var fx = new Fixture(TenantA);
        var pathSubject = fx.SeedSubject();
        var otherSubject = fx.SeedSubject();
        var content = fx.SeedContent(otherSubject);
        var id = await fx.SeedPath(pathSubject);
        var stepId = (await fx.AddSimpleStep(id, 10, content)).Data;
        var steps = await fx.GetSteps().Handle(new GetKnowledgePathStepsQuery(id), default);
        Assert.True(steps.Data!.Items.Single(x => x.StepId == stepId).IsCrossSubjectStep);
    }

    // ---------------- dangling prerequisite / limits (V-S17 / V-S20) ----------------

    [Fact]
    public async Task Archiving_a_prerequisite_step_returns_409()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var content = fx.SeedContent(s);
        var id = await fx.SeedPath(s);
        var first = (await fx.AddSimpleStep(id, 10, content)).Data;
        await fx.AddSimpleStep(id, 20, content, required: false, prereq: first);
        var r = await fx.ArchiveStep().Handle(new ArchiveKnowledgePathStepCommand(id, first), default);
        Assert.Equal(409, r.StatusCode);
    }

    [Fact]
    public async Task Branch_condition_limit_exceeded_returns_400()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var content = fx.SeedContent(s);
        var id = await fx.SeedPath(s);
        var many = Enumerable.Range(0, 21)
            .Select(i => new KnowledgePathBranchConditionInput("c" + i, null, null))
            .ToList();
        var r = await fx.AddSimpleStep(id, 10, content, branches: many);
        Assert.Equal(400, r.StatusCode);
    }

    // ---------------- AC-PIN-1 content resolution ----------------

    [Fact]
    public async Task Latest_published_resolves_newest_effective_version()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var v1 = fx.SeedContent(s, code: "SHARED", version: "1.0", from: Jan1);
        var id = await fx.SeedPath(s);
        var stepId = (await fx.AddSimpleStep(id, 10, v1, pin: "latest-published")).Data;
        // A newer published version of the same code appears.
        var v2 = fx.SeedContent(s, code: "SHARED", version: "2.0", from: Jun1);
        var steps = await fx.GetSteps().Handle(
            new GetKnowledgePathStepsQuery(id, EffectiveAt: new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero)), default);
        var step = steps.Data!.Items.Single(x => x.StepId == stepId);
        Assert.Equal(v2, step.ResolvedContentId);
        Assert.Equal(KnowledgePathContentResolutionStatuses.ResolvedLatest, step.ContentResolutionStatus);
    }

    [Fact]
    public async Task Pinned_stays_fixed_when_newer_version_appears()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var v1 = fx.SeedContent(s, code: "PIN", version: "1.0", from: Jan1);
        var id = await fx.SeedPath(s);
        var stepId = (await fx.AddSimpleStep(id, 10, v1, pin: "pinned")).Data;
        fx.SeedContent(s, code: "PIN", version: "2.0", from: Jun1);
        var steps = await fx.GetSteps().Handle(new GetKnowledgePathStepsQuery(id), default);
        var step = steps.Data!.Items.Single(x => x.StepId == stepId);
        Assert.Equal(v1, step.ResolvedContentId);
        Assert.Equal(KnowledgePathContentResolutionStatuses.Pinned, step.ContentResolutionStatus);
    }

    [Fact]
    public async Task Latest_published_unresolved_is_visible()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var content = fx.SeedContent(s, code: "GONE", version: "1.0");
        var id = await fx.SeedPath(s);
        var stepId = (await fx.AddSimpleStep(id, 10, content, pin: "latest-published")).Data;
        // The content is withdrawn (archived) — latest-published can no longer resolve.
        var c = fx.Contents.Items.Single(x => x.Id == content);
        c.ArchivedAt = Jan1;
        c.ContentStatus = KnowledgeContentStatuses.Archived;
        var steps = await fx.GetSteps().Handle(new GetKnowledgePathStepsQuery(id), default);
        var step = steps.Data!.Items.Single(x => x.StepId == stepId);
        Assert.Null(step.ResolvedContentId);
        Assert.Equal(KnowledgePathContentResolutionStatuses.Unresolved, step.ContentResolutionStatus);
    }

    // ---------------- concurrency (V-P15 / V-S19) ----------------

    [Fact]
    public async Task Concurrent_step_write_second_returns_409()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var content = fx.SeedContent(s);
        var id = await fx.SeedPath(s);
        var v = fx.Paths.Items.Single().Version;
        var first = await fx.AddSimpleStep(id, 10, content, expectedVersion: v);
        Assert.Equal(201, first.StatusCode);
        var second = await fx.AddSimpleStep(id, 20, content, required: false, code: "SB", expectedVersion: v);
        Assert.Equal(409, second.StatusCode);
    }

    // ---------------- tenant isolation ----------------

    [Fact]
    public async Task Other_tenant_path_is_invisible()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        await fx.SeedPath(s, "A-only");
        var listFromB = await fx.ListPaths(TenantB).Handle(new ListKnowledgePathsQuery(), default);
        Assert.Empty(listFromB.Data!.Items);
    }

    [Fact]
    public async Task Get_other_tenant_path_returns_404()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var id = await fx.SeedPath(s);
        var getB = new GetKnowledgePathHandler(Tenant(TenantB), fx.Paths, fx.Contents, fx.Nodes);
        var r = await getB.Handle(new GetKnowledgePathQuery(id), default);
        Assert.Equal(404, r.StatusCode);
    }

    // ---------------- contract & reader ----------------

    [Fact]
    public async Task Contract_exposes_thirteen_true_flags_and_no_engine_flags()
    {
        var handler = new GetKnowledgePathContractHandler(Tenant(TenantA));
        var r = await handler.Handle(new GetKnowledgePathContractQuery(), default);
        Assert.True(r.IsSuccessful);
        // Exactly the 13 documented capability flags exist — no branch-evaluator / recommendation / completion flag.
        Assert.Equal(13, typeof(KnowledgePathFeatureFlags).GetProperties().Count(p => p.PropertyType == typeof(bool)));
        Assert.Equal(200, r.Data!.Limits.MaxStepsPerPath);
        Assert.Equal(20, r.Data!.Limits.MaxBranchConditionsPerStep);
        var flagNames = typeof(KnowledgePathFeatureFlags).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("SupportsBranchEvaluator", flagNames);
        Assert.DoesNotContain("SupportsRecommendationEngine", flagNames);
        Assert.DoesNotContain("SupportsCompletionTracking", flagNames);
    }

    [Fact]
    public async Task Reader_returns_only_published_effective_active_ordered_steps()
    {
        var fx = new Fixture(TenantA);
        var s = fx.SeedSubject();
        var content = fx.SeedContent(s);

        // A draft path never reaches a consumer.
        var draft = await fx.SeedPath(s, "DRAFT");
        await fx.AddSimpleStep(draft, 10, content, required: true);

        // A published path is visible with its active steps ordered.
        var pub = await fx.SeedPath(s, "PUB");
        await fx.AddSimpleStep(pub, 20, content, required: true, code: "SB");
        await fx.AddSimpleStep(pub, 10, content, required: false, code: "SA");
        await fx.PublishPath().Handle(new PublishKnowledgePathCommand(pub), default);

        var paths = await fx.Reader().ResolvePublishedPathsAsync(new KnowledgePathCriteria(SubjectId: s), default);
        Assert.Single(paths);
        Assert.Equal("PUB", paths.Single().PathCode);

        var steps = await fx.Reader().GetOrderedStepsAsync(pub, DateTimeOffset.UtcNow, default);
        Assert.Equal(new[] { 10, 20 }, steps.Select(x => x.StepOrder).ToArray()); // deterministic order
    }

    [Fact]
    public void RegisterClassMaps_covers_path_and_embedded_types()
    {
        // The embedded types MUST be class-mapped (else their Guid members serialize as binary and filters return
        // nothing). Registering the persistence maps must not throw and must leave all three registered.
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        Assert.True(MongoDB.Bson.Serialization.BsonClassMap.IsClassMapRegistered(typeof(KnowledgePath)));
        Assert.True(MongoDB.Bson.Serialization.BsonClassMap.IsClassMapRegistered(typeof(KnowledgePathStep)));
        Assert.True(MongoDB.Bson.Serialization.BsonClassMap.IsClassMapRegistered(typeof(KnowledgePathBranchCondition)));
    }

    // ============================================================ in-memory fakes

    private sealed class FakePathRepo : IKnowledgePathRepository
    {
        public List<KnowledgePath> Items { get; } = new();
        public Task<KnowledgePath?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));
        public Task<IReadOnlyList<KnowledgePath>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<KnowledgePath>)Items.Where(x => x.TenantId == t && !x.IsDeleted).ToList());
        public Task<IReadOnlyList<KnowledgePath>> ListByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<KnowledgePath>)Items
                .Where(x => x.TenantId == t && !x.IsDeleted && x.PathCode == code).ToList());
        public Task InsertAsync(KnowledgePath e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task<bool> ReplaceAsync(KnowledgePath e, int expectedVersion, CancellationToken ct)
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
            => Task.FromResult((IReadOnlyList<AudienceProfile>)Items.Where(x => x.TenantId == t && !x.IsDeleted).ToList());
        public Task<AudienceProfile?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x =>
                x.TenantId == t && !x.IsDeleted && x.ProfileCode == code && !x.IsArchived()));
        public Task InsertAsync(AudienceProfile e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(AudienceProfile e, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeContentRepo : IKnowledgeContentRepository
    {
        public List<KnowledgeContent> Items { get; } = new();
        public Task<KnowledgeContent?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));
        public Task<IReadOnlyList<KnowledgeContent>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<KnowledgeContent>)Items.Where(x => x.TenantId == t && !x.IsDeleted).ToList());
        public Task<KnowledgeContent?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x =>
                x.TenantId == t && !x.IsDeleted && x.ContentCode == code && !x.IsArchived()));
        public Task InsertAsync(KnowledgeContent e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(KnowledgeContent e, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeNodeRepo : IConceptNodeRepository
    {
        public List<ConceptNode> Items { get; } = new();
        public Task<ConceptNode?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));
        public Task<IReadOnlyList<ConceptNode>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ConceptNode>)Items.Where(x => x.TenantId == t && !x.IsDeleted).ToList());
        public Task<IReadOnlyList<ConceptNode>> ListBySubjectAsync(Guid t, Guid s, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ConceptNode>)Items
                .Where(x => x.TenantId == t && !x.IsDeleted && x.SubjectId == s).ToList());
        public Task<ConceptNode?> GetActiveByCodeAsync(Guid t, Guid s, Guid ty, string code, CancellationToken ct)
            => Task.FromResult<ConceptNode?>(null);
        public Task InsertAsync(ConceptNode e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(ConceptNode e, CancellationToken ct) => Task.CompletedTask;
    }
}
