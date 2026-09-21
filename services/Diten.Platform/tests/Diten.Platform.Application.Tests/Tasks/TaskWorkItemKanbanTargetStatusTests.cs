using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WP-WCN-KANBAN-01 — the A-table (path:line traced against TransitionTaskItemHandler / AcceptTaskItemHandler /
/// SubmitTaskForReviewHandler / InquireTaskItemHandler and TaskLifecycleService.ToNormalizedStatus) made
/// executable. Each fact below is that table's one row: given the action code and the task state that offers it,
/// <c>WorkItemActionDto.TargetStatus</c> is the exact normalizedStatus the Kanban board would move the card to,
/// or null if the action must never be reachable by dragging a card (CT decision 2026-09-17): it changes who
/// holds the work (claim/release/return/reassign) or only a personal date (plan), never advances it.
/// </summary>
public sealed class TaskWorkItemKanbanTargetStatusTests
{
    private static readonly Guid PositionId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Start_from_Open_targets_InProgress()
    {
        var task = SelfTask();
        task.Lifecycle = TaskLifecycle.Open;

        var action = await ActionFor(task, "start");

        Assert.Equal(TaskLifecycleService.InProgress, action.TargetStatus);
    }

    [Fact]
    public async Task Resume_from_Waiting_uses_the_start_code_and_targets_InProgress()
    {
        var task = SelfTask();
        task.Lifecycle = TaskLifecycle.Waiting;

        var action = await ActionFor(task, "start");

        Assert.Equal(ActionResumeKeyLabel, action.Label.Key);
        Assert.Equal(TaskLifecycleService.InProgress, action.TargetStatus);
    }

    [Fact]
    public async Task Accept_from_Open_targets_InProgress()
    {
        var task = SelfTask();
        task.AssignmentTarget = TaskAssignmentTarget.Person;
        task.Lifecycle = TaskLifecycle.Open;

        var action = await ActionFor(task, "accept");

        Assert.Equal(TaskLifecycleService.InProgress, action.TargetStatus);
    }

    [Fact]
    public async Task Accept_from_Planned_has_no_target_the_card_does_not_move()
    {
        // AcceptTaskItemHandler only promotes Open → InProgress; a Planned task's lifecycle is untouched by
        // accept, so its Kanban card (already in the Pending column, since Planned normalizes to Pending) must
        // not be draggable via this action. This mirrors the handler; it is not a guess (CT decision 2026-09-17).
        var task = SelfTask();
        task.AssignmentTarget = TaskAssignmentTarget.Person;
        task.Lifecycle = TaskLifecycle.Planned;

        var action = await ActionFor(task, "accept");

        Assert.Null(action.TargetStatus);
    }

    [Fact]
    public async Task SubmitReview_targets_Waiting()
    {
        var task = SelfTask();
        task.Lifecycle = TaskLifecycle.InProgress;
        task.ReviewRequired = true;

        var action = await ActionFor(task, "submitReview");

        Assert.Equal(TaskLifecycleService.Waiting, action.TargetStatus);
    }

    [Fact]
    public async Task Complete_targets_Done()
    {
        var task = SelfTask();
        task.Lifecycle = TaskLifecycle.InProgress;

        var action = await ActionFor(task, "complete");

        Assert.Equal(TaskLifecycleService.Done, action.TargetStatus);
    }

    [Fact]
    public async Task Inquire_targets_Waiting()
    {
        var task = SelfTask();
        task.Lifecycle = TaskLifecycle.InProgress;

        var action = await ActionFor(task, "inquire");

        Assert.Equal(TaskLifecycleService.Waiting, action.TargetStatus);
    }

    [Fact]
    public async Task Cancel_targets_Cancelled()
    {
        var task = SelfTask();
        task.AssignmentTarget = TaskAssignmentTarget.Person;
        task.CreatedByUserId = TaskTestData.Me;
        task.Lifecycle = TaskLifecycle.Open;

        var action = await ActionFor(task, "cancel");

        Assert.Equal(TaskLifecycleService.Cancelled, action.TargetStatus);
    }

    // ── CT decision 2026-09-17: these five change WHO holds the work or a personal date, never the Kanban
    // column, so dragging a card must never be able to trigger them — targetStatus is always null. ────────────

    [Fact]
    public async Task Claim_has_no_target()
    {
        var task = PoolTask();

        var action = await ActionForWithPool(task, "claim");

        Assert.Null(action.TargetStatus);
    }

    [Fact]
    public async Task Release_has_no_target()
    {
        var task = SelfTask();
        task.AssignmentTarget = TaskAssignmentTarget.PositionPool;
        task.PoolPositionId = PositionId;
        task.Lifecycle = TaskLifecycle.Open;

        var action = await ActionForWithPool(task, "release");

        Assert.Null(action.TargetStatus);
    }

    [Fact]
    public async Task Plan_has_no_target()
    {
        var task = SelfTask();
        task.Lifecycle = TaskLifecycle.Open;

        var action = await ActionFor(task, "plan");

        Assert.Null(action.TargetStatus);
    }

    [Fact]
    public async Task Return_has_no_target()
    {
        // `return` is offered only when the holder and requester are two different people.
        var task = SelfTask();
        task.AssignmentTarget = TaskAssignmentTarget.Person;
        task.CreatedByUserId = TaskTestData.Rival;
        task.Lifecycle = TaskLifecycle.InProgress;

        var action = await ActionFor(task, "return");

        Assert.Null(action.TargetStatus);
    }

    [Fact]
    public async Task Reassign_has_no_target()
    {
        var task = SelfTask();
        task.AssignmentTarget = TaskAssignmentTarget.Person;
        task.DelegationAllowed = true;
        task.Lifecycle = TaskLifecycle.Open;

        var action = await ActionFor(task, "reassign");

        Assert.Null(action.TargetStatus);
    }

    // ── WP-WCN-KANBAN-01 Dilim 3a — a DISABLED action still carries its target. It is never a drop TARGET (the
    // board only offers columns an ENABLED action reaches), but the board needs to know which column WOULD have
    // received it, to grey that column and show the disabledReason as its hint. ─────────────────────────────────

    [Fact]
    public async Task Permission_denied_start_is_disabled_but_still_targets_InProgress()
    {
        var task = SelfTask();
        task.Lifecycle = TaskLifecycle.Open;

        var items = await Provider(new FakeTaskItemRepository(task))
            .GetWorkItemsAsync(ActorWithoutPermissions(), CancellationToken.None);
        var action = Assert.Single(items).Actions.Single(a => a.Code == "start");

        Assert.False(action.Enabled);
        Assert.Equal(WorkAggregationReasonCodes.PermissionDenied, action.DisabledReasonCode);
        Assert.Equal(TaskLifecycleService.InProgress, action.TargetStatus);
    }

    [Fact]
    public async Task Approval_pending_start_is_disabled_but_still_targets_InProgress()
    {
        var task = SelfTask();
        task.Lifecycle = TaskLifecycle.Open;
        task.ApprovalRequired = true;
        task.ApprovalManagerUserId = TaskTestData.Rival;

        var action = await ActionFor(task, "start");

        Assert.False(action.Enabled);
        Assert.Equal(TaskReasonCodes.ApprovalPending, action.DisabledReasonCode);
        Assert.Equal(TaskLifecycleService.InProgress, action.TargetStatus);
    }

    [Fact]
    public async Task Checklist_incomplete_complete_is_disabled_but_still_targets_Done()
    {
        var task = SelfTask();
        task.Lifecycle = TaskLifecycle.InProgress;
        var checklist = new ChecklistRun { TenantId = TaskTestData.Tenant, TaskItemId = task.Id, Version = 1 };
        checklist.Items.Add(new ChecklistRunItem
        {
            Code = "i0", LabelResourceKey = "WorkAggregation_Check_Sample",
            Requirement = ChecklistItemRequirement.Blocking, SortOrder = 0, Completed = false
        });

        var items = await Provider(
                new FakeTaskItemRepository(task), checklistRuns: new FakeChecklistRunRepository(checklist))
            .GetWorkItemsAsync(Actor(), CancellationToken.None);
        var action = Assert.Single(items).Actions.Single(a => a.Code == "complete");

        Assert.False(action.Enabled);
        Assert.Equal(TaskReasonCodes.ChecklistIncomplete, action.DisabledReasonCode);
        Assert.Equal(TaskLifecycleService.Done, action.TargetStatus);
    }

    [Fact]
    public async Task Review_meeting_required_submitReview_is_disabled_but_still_targets_Waiting()
    {
        var task = SelfTask();
        task.Lifecycle = TaskLifecycle.InProgress;
        task.ReviewRequired = true;
        task.TaskTypeId = Guid.NewGuid();
        var taskType = new TaskType
        {
            Id = task.TaskTypeId.Value, TenantId = TaskTestData.Tenant, Code = "REV", Name = "Review",
            ReviewMeetingRequirement = TaskReviewMeetingRequirement.Required
        };

        var provider = new TaskWorkItemProvider(
            new FakeTaskItemRepository(task), new FakePositionAssignmentRepository(),
            new TaskLifecycleService(), new TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver(), new FakeChecklistRunRepository(),
            new FakeTaskApprovalService(), new FakeTaskDependencyRepository(),
            new FakeTaskCommentRepository(), new FakeTaskTransitionRepository(),
            new FakeTaskPersonalOverlayRepository(), new FakeTaskWatcherRepository(),
            TaskActors.PermitAll(), new FakePositionRepository(), new FakeOrganizationUnitRepository(),
            SlaForTests.Real(), new FakeTaskFieldDefinitionRepository(), new FakeTaskTypeRepository(taskType),
            teamResolver: null, recordLinks: null, relatedRecordResolvers: null, attachments: null,
            logger: null, meetingRepository: null, reviewMeetingGate: new FakeReviewMeetingGateReader(false));

        var items = await provider.GetWorkItemsAsync(Actor(), CancellationToken.None);
        var action = Assert.Single(items).Actions.Single(a => a.Code == "submitReview");

        Assert.False(action.Enabled);
        Assert.Equal(TaskReasonCodes.ReviewMeetingRequired, action.DisabledReasonCode);
        Assert.Equal(TaskLifecycleService.Waiting, action.TargetStatus);
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private const string ActionResumeKeyLabel = "WorkAggregation_Action_Resume";

    private static WorkItemActor ActorWithoutPermissions() => new(
        TaskTestData.Me,
        IsPlatformActor: false,
        new HashSet<string>());

    private static async Task<WorkItemActionDto> ActionFor(TaskItem task, string code)
    {
        var items = await Provider(new FakeTaskItemRepository(task))
            .GetWorkItemsAsync(Actor(), CancellationToken.None);
        return Assert.Single(items).Actions.Single(a => a.Code == code);
    }

    private static async Task<WorkItemActionDto> ActionForWithPool(TaskItem task, string code)
    {
        var items = await Provider(
                new FakeTaskItemRepository(task),
                new FakePositionAssignmentRepository(Holder(TaskTestData.Me)))
            .GetWorkItemsAsync(Actor(), CancellationToken.None);
        return Assert.Single(items).Actions.Single(a => a.Code == code);
    }

    private static TaskWorkItemProvider Provider(
        FakeTaskItemRepository tasks,
        FakePositionAssignmentRepository? positionAssignments = null,
        FakeChecklistRunRepository? checklistRuns = null)
        => new(tasks,
            positionAssignments ?? new FakePositionAssignmentRepository(),
            new TaskLifecycleService(),
            new TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver(),
            checklistRuns ?? new FakeChecklistRunRepository(), new FakeTaskApprovalService(), new FakeTaskDependencyRepository(),
            new FakeTaskCommentRepository(), new FakeTaskTransitionRepository(),
            new FakeTaskPersonalOverlayRepository(), new FakeTaskWatcherRepository(), TaskActors.PermitAll(),
            new FakePositionRepository(), new FakeOrganizationUnitRepository(), SlaForTests.Real(),
            new FakeTaskFieldDefinitionRepository(), new FakeTaskTypeRepository());

    private static WorkItemActor Actor() => new(
        TaskTestData.Me,
        IsPlatformActor: true,
        new HashSet<string>());

    private static TaskItem SelfTask() => new()
    {
        DelegationAllowed = true,
        TenantId = TaskTestData.Tenant,
        Title = "Kanban target-status probe",
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = TaskTestData.Me,
        OrganizationUnitId = Guid.NewGuid(),
        Lifecycle = TaskLifecycle.Open,
        Version = 1
    };

    private static TaskItem PoolTask() => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "Pooled Kanban probe",
        AssignmentTarget = TaskAssignmentTarget.PositionPool,
        PoolPositionId = PositionId,
        AssigneeUserId = null,
        OrganizationUnitId = Guid.NewGuid(),
        Lifecycle = TaskLifecycle.Open,
        Version = 1
    };

    private static PositionAssignment Holder(Guid userId) => new()
    {
        TenantId = TaskTestData.Tenant,
        PositionId = PositionId,
        UserId = userId,
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1)
    };
}
