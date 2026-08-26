using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// What the projection REPORTS about a task's governance gates and its subtasks' real state.
///
/// <para>Both existed as data the server already held and the screen could not see: three cancelled subtasks
/// read as "not started", and the only way to learn a task was waiting on an approver was to press a button and
/// be refused.</para>
/// </summary>
public sealed class TaskGatesAndSubtaskStatusTests
{
    private static readonly Guid InstanceId = Guid.Parse("abcdabcd-abcd-abcd-abcd-abcdabcdabcd");

    /// <summary>The REVIEW instance — distinct from the approval one, because a task can carry both at once.</summary>
    private static readonly Guid ReviewInstanceId = Guid.Parse("beefbeef-beef-beef-beef-beefbeefbeef");

    // ── Subtask status ──────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(TaskLifecycle.Open, "not-started")]
    [InlineData(TaskLifecycle.Planned, "not-started")]
    [InlineData(TaskLifecycle.InProgress, "in-progress")]
    [InlineData(TaskLifecycle.Waiting, "in-progress")]
    [InlineData(TaskLifecycle.PendingReview, "in-progress")]
    [InlineData(TaskLifecycle.Done, "done")]
    // The defect: Cancelled fell into the default and was reported as work nobody had started yet.
    [InlineData(TaskLifecycle.Cancelled, "cancelled")]
    public async Task A_subtask_reports_the_state_it_is_actually_in(TaskLifecycle lifecycle, string expected)
    {
        var parent = ParentTask();
        var child = SubtaskOf(parent.Id, lifecycle);

        var item = await ProjectAsync(new FakeTaskItemRepository(parent, child), parent.Id);

        var subtask = Assert.Single(item.Subtasks!.Items);
        Assert.Equal(expected, subtask.Status);
    }

    [Fact]
    public async Task Cancelled_is_distinguishable_from_not_started()
    {
        var parent = ParentTask();
        var cancelled = SubtaskOf(parent.Id, TaskLifecycle.Cancelled);
        var fresh = SubtaskOf(parent.Id, TaskLifecycle.Open);

        var item = await ProjectAsync(new FakeTaskItemRepository(parent, cancelled, fresh), parent.Id);

        var statuses = item.Subtasks!.Items.Select(s => s.Status).ToList();
        // BL-035's "a cancelled subtask does not gate its parent" rule needs these to be different values.
        Assert.Contains("cancelled", statuses);
        Assert.Contains("not-started", statuses);
    }

    /*
     * Whether a row may offer "cancel". Sent per subtask because the shell cannot work it out: a subtask's
     * requester is its OWN, not the parent's, and a row must never offer an action the server will refuse.
     */
    [Fact]
    public async Task A_subtask_the_actor_requested_may_be_cancelled()
    {
        var parent = ParentTask();
        var child = SubtaskOf(parent.Id, TaskLifecycle.InProgress);

        var item = await ProjectAsync(new FakeTaskItemRepository(parent, child), parent.Id);

        Assert.True(Assert.Single(item.Subtasks!.Items).CanCancel);
    }

    [Fact]
    public async Task A_subtask_somebody_else_requested_may_not()
    {
        var parent = ParentTask();
        var child = SubtaskOf(parent.Id, TaskLifecycle.InProgress);
        child.CreatedByUserId = TaskTestData.Rival;

        var item = await ProjectAsync(new FakeTaskItemRepository(parent, child), parent.Id, actorIsPlatform: false);

        Assert.False(Assert.Single(item.Subtasks!.Items).CanCancel);
    }

    [Theory]
    [InlineData(TaskLifecycle.Done)]
    [InlineData(TaskLifecycle.Cancelled)]
    public async Task Work_that_has_already_stopped_cannot_be_called_off_again(TaskLifecycle lifecycle)
    {
        var parent = ParentTask();
        var child = SubtaskOf(parent.Id, lifecycle);

        var item = await ProjectAsync(new FakeTaskItemRepository(parent, child), parent.Id);

        Assert.False(Assert.Single(item.Subtasks!.Items).CanCancel);
    }

    // ── Gates ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_task_with_no_governance_reports_both_gates_as_not_required()
    {
        var item = await ProjectAsync(new FakeTaskItemRepository(ParentTask()), ParentTask().Id, single: true);

        Assert.NotNull(item.Gates);
        Assert.False(item.Gates!.Approval.Required);
        Assert.Equal("notRequired", item.Gates.Approval.Status);
        Assert.False(item.Gates.Review.Required);
        Assert.Equal("notRequired", item.Gates.Review.Status);
    }

    [Fact]
    public async Task An_outstanding_approval_reports_pending_and_names_the_candidate_approver()
    {
        var task = ParentTask();
        task.ApprovalRequired = true;
        task.ApprovalManagerUserId = TaskTestData.Rival;
        task.WorkflowInstanceId = InstanceId;

        var item = await ProjectAsync(
            new FakeTaskItemRepository(task), task.Id, single: true,
            approvals: new FakeTaskApprovalService().Pending(InstanceId));

        Assert.True(item.Gates!.Approval.Required);
        Assert.Equal("pending", item.Gates.Approval.Status);
        // A typed identity, never a guessed name.
        Assert.Equal(TaskTestData.Rival.ToString(), item.Gates.Approval.Decider!.Id);
    }

    /*
     * Status is read from what MOD-0023 says about the instance, never from ApprovalRequired — that flag records
     * only that approval was ASKED FOR. Deriving from it once left an approved task still showing as waiting.
     */
    [Fact]
    public async Task An_approved_task_reports_approved_even_though_the_flag_is_still_set()
    {
        var task = ParentTask();
        task.ApprovalRequired = true;
        task.WorkflowInstanceId = InstanceId;

        var item = await ProjectAsync(
            new FakeTaskItemRepository(task), task.Id, single: true,
            approvals: new FakeTaskApprovalService().Approved(InstanceId));

        Assert.Equal("approved", item.Gates!.Approval.Status);
    }

    [Fact]
    public async Task A_refused_approval_reports_rejected()
    {
        var task = ParentTask();
        task.ApprovalRequired = true;
        task.WorkflowInstanceId = InstanceId;

        var item = await ProjectAsync(
            new FakeTaskItemRepository(task), task.Id, single: true,
            approvals: new FakeTaskApprovalService().Rejected(InstanceId));

        Assert.Equal("rejected", item.Gates!.Approval.Status);
    }

    [Fact]
    public async Task A_review_that_is_declared_but_not_yet_reached_reports_required()
    {
        var task = ParentTask();
        task.ReviewRequired = true;

        var item = await ProjectAsync(new FakeTaskItemRepository(task), task.Id, single: true);

        Assert.True(item.Gates!.Review.Required);
        Assert.Equal("required", item.Gates.Review.Status);
        // MOD-0024 records THAT a review is required, never who reviews — so no name is invented.
        Assert.Null(item.Gates.Review.Decider);
    }

    [Fact]
    public async Task A_task_sitting_with_its_reviewer_reports_pending()
    {
        /*
         * The instance is what makes this "pending" (Faz 3b). The lifecycle alone used to decide it, which records
         * only that the work was HANDED OVER and never what came back — so a released review reported itself as
         * still waiting, forever.
         */
        var task = ParentTask();
        task.ReviewRequired = true;
        task.Lifecycle = TaskLifecycle.PendingReview;
        task.ReviewWorkflowInstanceId = ReviewInstanceId;

        var item = await ProjectAsync(new FakeTaskItemRepository(task), task.Id, single: true);

        Assert.Equal("pending", item.Gates!.Review.Status);
    }

    [Fact]
    public async Task The_SAME_task_reports_approved_once_MOD0023_releases_the_review()
    {
        // Non-vacuity for the test above: identical task, identical lifecycle, and only MOD-0023's answer differs.
        var task = ParentTask();
        task.ReviewRequired = true;
        task.Lifecycle = TaskLifecycle.PendingReview;
        task.ReviewWorkflowInstanceId = ReviewInstanceId;

        var item = await ProjectAsync(
            new FakeTaskItemRepository(task), task.Id, single: true,
            approvals: new FakeTaskApprovalService().Approved(ReviewInstanceId));

        Assert.Equal("approved", item.Gates!.Review.Status);
    }

    /*
     * BOUNDARY. The aggregator reports gate state; it never offers a way to change it. MOD-0024 was already
     * caught growing a second approval engine once (charter Binding A), so this is asserted rather than assumed.
     */
    [Fact]
    public async Task The_projection_offers_no_way_to_decide_a_gate()
    {
        var task = ParentTask();
        task.ApprovalRequired = true;
        task.WorkflowInstanceId = InstanceId;

        var item = await ProjectAsync(
            new FakeTaskItemRepository(task), task.Id, single: true,
            approvals: new FakeTaskApprovalService().Pending(InstanceId));

        var codes = item.Actions.Select(a => a.Code).ToList();
        Assert.DoesNotContain("approve", codes);
        Assert.DoesNotContain("reject", codes);
        Assert.DoesNotContain("signoff", codes);
    }

    // ── harness ─────────────────────────────────────────────────────────────────────────────────────────────

    private static async Task<WorkItemProjectionDto> ProjectAsync(
        FakeTaskItemRepository tasks,
        Guid parentId,
        bool single = false,
        FakeTaskApprovalService? approvals = null,
        bool actorIsPlatform = true)
    {
        var provider = new TaskWorkItemProvider(
            tasks,
            new FakePositionAssignmentRepository(),
            new TaskLifecycleService(),
            new TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver(),
            new FakeChecklistRunRepository(),
            approvals ?? new FakeTaskApprovalService(), new FakeTaskDependencyRepository(), new FakeTaskCommentRepository(), new FakeTaskTransitionRepository(), new FakeTaskPersonalOverlayRepository(), new FakeTaskWatcherRepository(), TaskActors.PermitAll(), new FakePositionRepository(), new FakeOrganizationUnitRepository(), SlaForTests.Real(), new FakeTaskFieldDefinitionRepository(), new FakeTaskTypeRepository());

        var items = await provider.GetWorkItemsAsync(
            new WorkItemActor(TaskTestData.Me, actorIsPlatform, new HashSet<string>()),
            CancellationToken.None);

        return single ? Assert.Single(items) : items.Single(i => i.Id == parentId.ToString());
    }

    private static TaskItem ParentTask() => new()
    {
        Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
        TenantId = TaskTestData.Tenant,
        Title = "Parent work",
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = TaskTestData.Me,
        CreatedByUserId = TaskTestData.Me,
        OrganizationUnitId = Guid.NewGuid(),
        Lifecycle = TaskLifecycle.InProgress,
        Version = 1
    };

    private static TaskItem SubtaskOf(Guid parentId, TaskLifecycle lifecycle) => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "Child work",
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = TaskTestData.Me,
        CreatedByUserId = TaskTestData.Me,
        OrganizationUnitId = Guid.NewGuid(),
        ParentTaskItemId = parentId,
        Lifecycle = lifecycle,
        Version = 1
    };
}
