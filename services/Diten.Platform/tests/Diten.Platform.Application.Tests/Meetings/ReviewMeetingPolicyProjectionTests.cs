using Diten.Platform.Application.Features.Meetings.RecordLinks;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// MOD-0357 S4, K3 — the receiving side of <c>scheduleReviewMeeting</c>, at the projection. Every case runs the
/// REAL <c>TaskWorkItemProvider</c>/<c>Project</c> path, mirroring <see cref="RelatedRecordsProjectionTests"/>'s
/// own harness shape.
/// </summary>
public sealed class ReviewMeetingPolicyProjectionTests
{
    private static readonly Guid TaskId = Guid.Parse("a1a1a1a1-0000-0000-0000-0000000000a1");
    private static readonly Guid MeetingId = Guid.Parse("b2b2b2b2-0000-0000-0000-0000000000b2");
    private static readonly DateTimeOffset StartAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Every_task_gets_the_policy_fixed_at_optional_this_slice()
    {
        var task = MakeTask(holder: TaskTestData.Me, requester: TaskTestData.Me);
        var item = await ProjectSingle(task, actorId: TaskTestData.Me, links: [], meetings: []);

        Assert.NotNull(item.ReviewMeetingPolicy);
        Assert.Equal("optional", item.ReviewMeetingPolicy!.Requirement);
        Assert.Null(item.ReviewMeetingPolicy.MeetingId);
        Assert.Null(item.ReviewMeetingPolicy.ScheduledAt);
    }

    [Fact]
    public async Task Holder_gets_the_scheduleReviewMeeting_action_when_nothing_is_scheduled_yet()
    {
        var task = MakeTask(holder: TaskTestData.Me, requester: TaskTestData.Rival);
        var item = await ProjectSingle(task, actorId: TaskTestData.Me, links: [], meetings: []);

        Assert.Contains(item.Actions, a => a.Code == "scheduleReviewMeeting" && a.Enabled);
    }

    [Fact]
    public async Task Requester_who_is_not_the_holder_also_gets_the_action()
    {
        var task = MakeTask(holder: TaskTestData.Rival, requester: TaskTestData.Me);
        var item = await ProjectSingle(task, actorId: TaskTestData.Me, links: [], meetings: []);

        Assert.Contains(item.Actions, a => a.Code == "scheduleReviewMeeting");
    }

    [Fact]
    public async Task Neither_holder_nor_requester_does_NOT_get_the_action()
    {
        // Whether the provider's own scope filtering drops this row entirely (the actor has no relationship to
        // it) or returns it with the action withheld, "scheduleReviewMeeting" must not reach this actor either
        // way — the same either/or K9 already accepts for a caller unrelated to a record.
        var task = MakeTask(holder: TaskTestData.Rival, requester: TaskTestData.Other);
        var items = await ProjectAll(task, actorId: TaskTestData.Me, links: [], meetings: []);

        Assert.DoesNotContain(items.SelectMany(i => i.Actions), a => a.Code == "scheduleReviewMeeting");
    }

    [Fact]
    public async Task Once_scheduled_the_policy_carries_the_meeting_and_the_action_disappears()
    {
        var link = ReviewLink();
        var meeting = new Meeting
        {
            Id = MeetingId,
            TenantId = TaskTestData.Tenant,
            Title = "Review: something",
            MeetingTypeId = Guid.NewGuid(),
            StartAt = StartAt,
            EndAt = StartAt.AddHours(1),
            OrganizerUserId = TaskTestData.Me,
            IdempotencyKey = "irrelevant",
            CreatedBy = "test"
        };
        var task = MakeTask(holder: TaskTestData.Me, requester: TaskTestData.Me);

        var item = await ProjectSingle(task, actorId: TaskTestData.Me, links: [link], meetings: [meeting]);

        Assert.Equal(MeetingId.ToString(), item.ReviewMeetingPolicy!.MeetingId);
        Assert.Equal(StartAt, item.ReviewMeetingPolicy.ScheduledAt);
        Assert.DoesNotContain(item.Actions, a => a.Code == "scheduleReviewMeeting");
    }

    [Fact]
    public async Task A_terminal_task_never_offers_the_action_even_unscheduled()
    {
        var task = MakeTask(holder: TaskTestData.Me, requester: TaskTestData.Me);
        task.Lifecycle = TaskLifecycle.Done;
        task.CompletedAt = DateTimeOffset.UtcNow;

        var item = await ProjectSingle(task, actorId: TaskTestData.Me, links: [], meetings: []);

        Assert.DoesNotContain(item.Actions, a => a.Code == "scheduleReviewMeeting");
    }

    [Fact]
    public async Task A_link_of_a_DIFFERENT_type_to_the_same_task_is_not_read_as_a_scheduled_review()
    {
        // "agenda"/"preparation"/"bornFromMeeting" links must never be mistaken for a reviewMeeting link.
        var link = new RecordLink
        {
            TenantId = TaskTestData.Tenant,
            SourceModuleCode = RecordLinkModuleCodes.Meetings,
            SourceRecordId = MeetingId,
            TargetModuleCode = RecordLinkModuleCodes.Tasks,
            TargetRecordId = TaskId,
            LinkType = RecordLinkTypes.Agenda,
            CreatedByUserId = TaskTestData.Me,
            CreatedBy = "test"
        };
        var task = MakeTask(holder: TaskTestData.Me, requester: TaskTestData.Me);

        var item = await ProjectSingle(task, actorId: TaskTestData.Me, links: [link], meetings: []);

        Assert.Null(item.ReviewMeetingPolicy!.MeetingId);
        Assert.Contains(item.Actions, a => a.Code == "scheduleReviewMeeting");
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static RecordLink ReviewLink() => new()
    {
        TenantId = TaskTestData.Tenant,
        SourceModuleCode = RecordLinkModuleCodes.Meetings,
        SourceRecordId = MeetingId,
        TargetModuleCode = RecordLinkModuleCodes.Tasks,
        TargetRecordId = TaskId,
        LinkType = RecordLinkTypes.ReviewMeeting,
        CreatedByUserId = TaskTestData.Me,
        CreatedBy = "test"
    };

    private static TaskItem MakeTask(Guid holder, Guid requester) => new()
    {
        Id = TaskId,
        TenantId = TaskTestData.Tenant,
        Title = "Bir görev",
        AssignmentTarget = TaskAssignmentTarget.Person,
        AssigneeUserId = holder,
        CreatedByUserId = requester,
        OrganizationUnitId = Guid.NewGuid(),
        Lifecycle = TaskLifecycle.Open
    };

    private static async Task<WorkItemProjectionDto> ProjectSingle(
        TaskItem task, Guid actorId, IReadOnlyList<RecordLink> links, IReadOnlyList<Meeting> meetings)
        => Assert.Single(await ProjectAll(task, actorId, links, meetings));

    private static async Task<IReadOnlyList<WorkItemProjectionDto>> ProjectAll(
        TaskItem task, Guid actorId, IReadOnlyList<RecordLink> links, IReadOnlyList<Meeting> meetings)
    {
        var meetingRepository = new FakeMeetingRepository { Tenant = TaskTestData.Tenant };
        foreach (var meeting in meetings)
        {
            meetingRepository.Seed(meeting);
        }

        var provider = new TaskWorkItemProvider(
            new FakeTaskItemRepository([task]),
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
            relatedRecordResolvers: new FakeRelatedRecordResolverRegistry([]),
            logger: null,
            meetingRepository: meetingRepository);

        var actor = new WorkItemActor(actorId, IsPlatformActor: true, new HashSet<string>());
        return await provider.GetWorkItemsAsync(actor, CancellationToken.None);
    }
}
