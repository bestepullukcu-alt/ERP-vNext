using Diten.Platform.Application.Features.Meetings.RecordLinks;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// MOD-0357 S1 — `relatedRecords` at the projection: AC4 (data-driven capability, capped at 20), AC5 (a
/// missing resolver drops the link rather than inventing a title), and the N+1 proof AC2 needs.
///
/// <para>Every case runs the REAL <c>TaskWorkItemProvider</c> and the REAL <c>ResolveCapabilities</c>/
/// `Project` path — only the repositories are doubles, exactly like every other provider test in this
/// suite.</para>
/// </summary>
public sealed class RelatedRecordsProjectionTests
{
    private static readonly Guid TaskAId = Guid.Parse("aaaaaaaa-0000-0000-0000-00000000000a");
    private static readonly Guid TaskBId = Guid.Parse("bbbbbbbb-0000-0000-0000-00000000000b");
    private static readonly Guid MeetingId = Guid.Parse("cccccccc-0000-0000-0000-00000000000c");
    private static readonly Guid UnresolvableModuleTargetId = Guid.Parse("dddddddd-0000-0000-0000-00000000000d");

    [Fact]
    public async Task A_task_with_NO_link_gets_NO_capability_and_NO_container()
    {
        var item = await ProjectSingle(TaskAId, links: [], resolvers: []);

        Assert.DoesNotContain("relatedRecords", item.WorkItemCapabilities);
        Assert.Null(item.RelatedRecords);
    }

    [Fact]
    public async Task A_task_with_ONE_link_gets_the_capability_and_ONE_populated_row()
    {
        var link = Link(source: (RecordLinkModuleCodes.Meetings, MeetingId), target: (RecordLinkModuleCodes.Tasks, TaskAId));
        var meetingsResolver = new FakeRelatedRecordResolver(
            RecordLinkModuleCodes.Meetings,
            new Dictionary<Guid, RelatedRecordSummary> { [MeetingId] = new("Aylık QA Toplantısı", "/Meetings/" + MeetingId) });

        var item = await ProjectSingle(TaskAId, links: [link], resolvers: [meetingsResolver]);

        Assert.Contains("relatedRecords", item.WorkItemCapabilities);
        var row = Assert.Single(item.RelatedRecords!);
        Assert.Equal(MeetingId.ToString(), row.Id);
        Assert.Equal(RecordLinkModuleCodes.Meetings, row.Type);
        Assert.Equal("Aylık QA Toplantısı", row.Title);
        Assert.Equal("/Meetings/" + MeetingId, row.Link);
    }

    [Fact]
    public async Task The_link_is_found_whichever_side_the_task_is_on()
    {
        // Task as SOURCE this time (a meeting created by/from a task's own agenda flow reads the other way).
        var link = Link(source: (RecordLinkModuleCodes.Tasks, TaskAId), target: (RecordLinkModuleCodes.Meetings, MeetingId));
        var meetingsResolver = new FakeRelatedRecordResolver(
            RecordLinkModuleCodes.Meetings,
            new Dictionary<Guid, RelatedRecordSummary> { [MeetingId] = new("Devam Toplantısı", "/Meetings/" + MeetingId) });

        var item = await ProjectSingle(TaskAId, links: [link], resolvers: [meetingsResolver]);

        Assert.Contains("relatedRecords", item.WorkItemCapabilities);
        Assert.Single(item.RelatedRecords!);
    }

    [Fact]
    public async Task A_link_to_a_module_with_NO_registered_resolver_is_DROPPED_not_invented()
    {
        var link = Link(source: ("governance-bodies", UnresolvableModuleTargetId), target: (RecordLinkModuleCodes.Tasks, TaskAId));

        // No resolver for "governance-bodies" is registered at all.
        var item = await ProjectSingle(TaskAId, links: [link], resolvers: []);

        Assert.DoesNotContain("relatedRecords", item.WorkItemCapabilities);
        Assert.Null(item.RelatedRecords);
    }

    [Fact]
    public async Task A_link_whose_far_record_the_resolver_cannot_find_is_DROPPED()
    {
        // Registered resolver, but it genuinely has nothing for this id (deleted/cross-tenant/retired).
        var link = Link(source: (RecordLinkModuleCodes.Meetings, MeetingId), target: (RecordLinkModuleCodes.Tasks, TaskAId));
        var emptyMeetingsResolver = new FakeRelatedRecordResolver(
            RecordLinkModuleCodes.Meetings, new Dictionary<Guid, RelatedRecordSummary>());

        var item = await ProjectSingle(TaskAId, links: [link], resolvers: [emptyMeetingsResolver]);

        Assert.DoesNotContain("relatedRecords", item.WorkItemCapabilities);
        Assert.Null(item.RelatedRecords);
    }

    [Fact]
    public async Task More_than_20_links_are_capped_at_the_contract_limit()
    {
        var meetingIds = Enumerable.Range(0, 25).Select(_ => Guid.NewGuid()).ToList();
        var links = meetingIds
            .Select(id => Link(source: (RecordLinkModuleCodes.Meetings, id), target: (RecordLinkModuleCodes.Tasks, TaskAId)))
            .ToList();
        var known = meetingIds.ToDictionary(id => id, id => new RelatedRecordSummary("T-" + id, "/Meetings/" + id));
        var resolver = new FakeRelatedRecordResolver(RecordLinkModuleCodes.Meetings, known);

        var item = await ProjectSingle(TaskAId, links, [resolver]);

        Assert.Equal(WorkItemContract.MaxRelatedRecords, item.RelatedRecords!.Count);
    }

    [Fact]
    public async Task Ten_linked_tasks_resolve_through_ONE_batched_call_never_ten()
    {
        // AC2's own words: "toplu çağrı N görev için tek sorgu". Ten DIFFERENT tasks, each linked to its own
        // meeting, on the SAME page read — the resolver must be asked once, with all ten ids, not ten times.
        var meetingIds = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToList();
        var taskIds = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToList();
        var links = taskIds.Zip(meetingIds, (taskId, meetingId) =>
                Link(source: (RecordLinkModuleCodes.Meetings, meetingId), target: (RecordLinkModuleCodes.Tasks, taskId)))
            .ToList();
        var known = meetingIds.ToDictionary(id => id, id => new RelatedRecordSummary("T", "/Meetings/" + id));
        var resolver = new FakeRelatedRecordResolver(RecordLinkModuleCodes.Meetings, known);

        var tasks = taskIds.Select(id => MakeTask(id)).ToList();
        var provider = BuildProvider(tasks, links, [resolver]);
        var items = await provider.GetWorkItemsAsync(Actor(), CancellationToken.None);

        Assert.Equal(10, items.Count);
        Assert.All(items, i => Assert.Contains("relatedRecords", i.WorkItemCapabilities));
        Assert.Single(resolver.Calls);
        Assert.Equal(10, resolver.Calls[0].Count);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static RecordLink Link((string ModuleCode, Guid Id) source, (string ModuleCode, Guid Id) target)
        => new()
        {
            TenantId = TaskTestData.Tenant,
            SourceModuleCode = source.ModuleCode,
            SourceRecordId = source.Id,
            TargetModuleCode = target.ModuleCode,
            TargetRecordId = target.Id,
            LinkType = RecordLinkTypes.Agenda,
            CreatedByUserId = TaskTestData.Me,
            CreatedBy = "test"
        };

    private static TaskItem MakeTask(Guid id) => new()
    {
        Id = id,
        TenantId = TaskTestData.Tenant,
        Title = "İlişkili görev",
        AssignmentTarget = TaskAssignmentTarget.Person,
        AssigneeUserId = TaskTestData.Me,
        CreatedByUserId = TaskTestData.Me,
        OrganizationUnitId = Guid.NewGuid(),
        Lifecycle = TaskLifecycle.Open
    };

    private static async Task<WorkItemProjectionDto> ProjectSingle(
        Guid taskId, IReadOnlyList<RecordLink> links, IReadOnlyList<IRelatedRecordResolver> resolvers)
    {
        var provider = BuildProvider([MakeTask(taskId)], links, resolvers);
        var items = await provider.GetWorkItemsAsync(Actor(), CancellationToken.None);
        return Assert.Single(items);
    }

    private static TaskWorkItemProvider BuildProvider(
        IReadOnlyList<TaskItem> tasks, IReadOnlyList<RecordLink> links, IReadOnlyList<IRelatedRecordResolver> resolvers)
        => new(
            new FakeTaskItemRepository([.. tasks]),
            new FakePositionAssignmentRepository(),
            new TaskLifecycleService(),
            new TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver(),
            new FakeChecklistRunRepository(),
            new FakeTaskApprovalService(),
            new FakeTaskDependencyRepository(),
            new FakeTaskCommentRepository(),
            new FakeTaskTransitionRepository(),
            new FakeTaskPersonalOverlayRepository(),
            new FakeTaskWatcherRepository(),
            TaskActors.PermitAll(),
            new FakePositionRepository(),
            new FakeOrganizationUnitRepository(),
            SlaForTests.Real(),
            new FakeTaskFieldDefinitionRepository(),
            new FakeTaskTypeRepository(),
            teamResolver: null,
            recordLinks: new RecordLinkService(
                new FakeRecordLinkRepository([.. links]),
                new FakeTenantContext(TaskTestData.Tenant),
                new FakeCurrentUserContext(TaskTestData.Me)),
            relatedRecordResolvers: new FakeRelatedRecordResolverRegistry([.. resolvers]));

    private static WorkItemActor Actor() => new(TaskTestData.Me, IsPlatformActor: true, new HashSet<string>());
}
