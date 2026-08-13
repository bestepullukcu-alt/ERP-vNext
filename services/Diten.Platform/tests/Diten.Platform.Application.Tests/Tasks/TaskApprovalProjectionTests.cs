using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// Phase 3 — the projection reads approval state from MOD-0023 AT REQUEST TIME, batched.
///
/// <para>MOD-0024 stores no approval status: the only link is TaskItem.WorkflowInstanceId. Before this wiring the
/// provider judged approval from the ApprovalRequired FLAG, which cannot distinguish "pending" from "approved" —
/// so an approved task stayed Waiting forever and its `start` action was permanently disabled. These tests pin the
/// released case, which is the one that was broken, and the read shape, which is what makes it affordable.</para>
/// </summary>
public sealed class TaskApprovalProjectionTests
{
    private static readonly Guid Instance = Guid.Parse("beeff00d-0000-0000-0000-000000000001");

    [Fact]
    public async Task An_APPROVED_task_leaves_Waiting_and_its_start_becomes_enabled()
    {
        var task = ApprovalTask(Instance);
        var approvals = new FakeTaskApprovalService().Approved(Instance);

        var item = Assert.Single(await Provider(task, approvals).GetWorkItemsAsync(Actor(), CancellationToken.None));

        // The release is visible: no longer waiting, and no waitingContext (the contract forbids one without Waiting).
        Assert.NotEqual("Waiting", item.NormalizedStatus);
        Assert.Null(item.WaitingContext);

        var start = Assert.Single(item.Actions, a => a.Code == "start");
        Assert.True(start.Enabled, "an approved task must be startable — this is the bug Phase 3 fixes");
        Assert.Null(start.DisabledReasonCode);
    }

    [Fact]
    public async Task A_PENDING_approval_still_shows_start_disabled_with_the_reason()
    {
        var task = ApprovalTask(Instance);
        var approvals = new FakeTaskApprovalService().Pending(Instance);

        var item = Assert.Single(await Provider(task, approvals).GetWorkItemsAsync(Actor(), CancellationToken.None));

        Assert.Equal("Waiting", item.NormalizedStatus);
        Assert.NotNull(item.WaitingContext);
        var start = Assert.Single(item.Actions, a => a.Code == "start");
        Assert.False(start.Enabled);
        Assert.NotNull(start.DisabledReasonCode);
    }

    [Fact]
    public async Task An_UNREADABLE_approval_state_is_treated_as_outstanding_not_as_released()
    {
        // Fail-closed: MOD-0023 reporting nothing for the instance must never read as "approved".
        var task = ApprovalTask(Instance);

        var item = Assert.Single(
            await Provider(task, new FakeTaskApprovalService()).GetWorkItemsAsync(Actor(), CancellationToken.None));

        Assert.Equal("Waiting", item.NormalizedStatus);
        Assert.False(Assert.Single(item.Actions, a => a.Code == "start").Enabled);
    }

    [Fact]
    public async Task A_task_with_no_approval_never_consults_the_workflow_at_all()
    {
        var task = ApprovalTask(Instance);
        task.ApprovalRequired = false;
        task.WorkflowInstanceId = null;
        var approvals = new FakeTaskApprovalService();

        var item = Assert.Single(await Provider(task, approvals).GetWorkItemsAsync(Actor(), CancellationToken.None));

        Assert.True(Assert.Single(item.Actions, a => a.Code == "start").Enabled);
        // One batch call may still be made with an EMPTY id set, but it must carry no ids.
        Assert.All(approvals.StateReadSizes, size => Assert.Equal(0, size));
    }

    /// <summary>
    /// The N+1 guard. Twelve approval-gated tasks must cost ONE state read carrying twelve ids — not twelve reads.
    /// This is the test that was missing: without it, a per-task read looks identical from the outside.
    /// </summary>
    [Fact]
    public async Task Twelve_approval_gated_tasks_cost_ONE_batched_read()
    {
        var tasks = Enumerable.Range(1, 12).Select(i =>
        {
            var instanceId = Guid.Parse($"beeff00d-0000-0000-0000-0000000000{i:D2}");
            var task = ApprovalTask(instanceId);
            task.Title = $"Approval task {i}";
            return task;
        }).ToArray();

        var approvals = new FakeTaskApprovalService();
        foreach (var task in tasks)
        {
            approvals.Approved(task.WorkflowInstanceId!.Value);
        }

        var items = await Provider(tasks, approvals).GetWorkItemsAsync(Actor(), CancellationToken.None);

        Assert.Equal(12, items.Count);
        var read = Assert.Single(approvals.StateReadSizes);
        Assert.Equal(12, read);
        // And the batch actually answered every one of them.
        Assert.All(items, item => Assert.NotEqual("Waiting", item.NormalizedStatus));
    }

    [Fact]
    public async Task Repeated_instance_ids_are_deduplicated_before_the_read()
    {
        // Two tasks sharing one approval instance must not ask for it twice.
        var first = ApprovalTask(Instance);
        var second = ApprovalTask(Instance);
        second.Title = "Second task on the same approval";
        var approvals = new FakeTaskApprovalService().Approved(Instance);

        await Provider([first, second], approvals).GetWorkItemsAsync(Actor(), CancellationToken.None);

        Assert.Equal(1, Assert.Single(approvals.StateReadSizes));
    }

    // ── The two reasons the projection used to get wrong ─────────────────────

    [Fact]
    public async Task An_IN_PROGRESS_task_with_an_outstanding_approval_cannot_be_COMPLETED_either()
    {
        // The gate blocks Done as well as InProgress, so offering an enabled `complete` was a projection that
        // promised something the server answers with 409.
        var task = ApprovalTask(Instance);
        task.Lifecycle = TaskLifecycle.InProgress;
        var approvals = new FakeTaskApprovalService().Pending(Instance);

        var item = Assert.Single(await Provider(task, approvals).GetWorkItemsAsync(Actor(), CancellationToken.None));

        var complete = Assert.Single(item.Actions, a => a.Code == "complete");
        Assert.False(complete.Enabled);
        // Its own wording: "cannot be started" is nonsense on a task already in progress.
        Assert.Equal("WorkAggregation_ActionDisabled_ApprovalPendingComplete", complete.DisabledReason!.Key);
        Assert.Equal(Application.Features.Tasks.TaskReasonCodes.ApprovalPending, complete.DisabledReasonCode);
    }

    [Fact]
    public async Task An_approval_that_NEVER_STARTED_says_so_instead_of_blaming_an_approver()
    {
        // ApprovalRequired with no instance means the handoff never reached MOD-0023. Telling the user "waiting for
        // approval" points at someone who was never asked; this is the case they can act on by retrying the save.
        var task = ApprovalTask(Instance);
        task.WorkflowInstanceId = null;

        var item = Assert.Single(
            await Provider(task, new FakeTaskApprovalService()).GetWorkItemsAsync(Actor(), CancellationToken.None));

        var start = Assert.Single(item.Actions, a => a.Code == "start");
        Assert.False(start.Enabled);
        Assert.Equal("WorkAggregation_ApprovalError_StartFailed", start.DisabledReason!.Key);
    }

    [Fact]
    public async Task A_RUNNING_approval_still_says_it_is_waiting_on_the_approver()
    {
        var task = ApprovalTask(Instance);
        var approvals = new FakeTaskApprovalService().Pending(Instance);

        var item = Assert.Single(await Provider(task, approvals).GetWorkItemsAsync(Actor(), CancellationToken.None));

        // The two states must not collapse into one message — that is the distinction this pair exists to prove.
        Assert.Equal("WorkAggregation_ActionDisabled_ApprovalPending",
            Assert.Single(item.Actions, a => a.Code == "start").DisabledReason!.Key);
    }

    // ── Item 3: the rejection outcome ────────────────────────────────────────

    [Fact]
    public async Task A_REJECTED_approval_makes_the_task_read_as_Cancelled_with_no_action_left()
    {
        var task = ApprovalTask(Instance);
        var approvals = new FakeTaskApprovalService().Rejected(Instance);

        var item = Assert.Single(await Provider(task, approvals).GetWorkItemsAsync(Actor(), CancellationToken.None));

        // Refused work is dead: Cancelled, no waitingContext (nobody is owed anything), and no action to take.
        Assert.Equal("Cancelled", item.NormalizedStatus);
        Assert.Null(item.WaitingContext);
        Assert.Empty(item.Actions);
        Assert.Null(item.PrimaryActionCode);
    }

    [Fact]
    public async Task Rejection_comes_from_the_SAME_batched_read_as_everything_else()
    {
        // No second mechanism, no stored copy: one read answers approved / pending / rejected for the whole page.
        var approved = ApprovalTask(Guid.Parse("beeff00d-0000-0000-0000-00000000aaaa"));
        var rejected = ApprovalTask(Guid.Parse("beeff00d-0000-0000-0000-00000000bbbb"));
        rejected.Title = "Refused task";
        var approvals = new FakeTaskApprovalService()
            .Approved(approved.WorkflowInstanceId!.Value)
            .Rejected(rejected.WorkflowInstanceId!.Value);

        var items = await Provider([approved, rejected], approvals).GetWorkItemsAsync(Actor(), CancellationToken.None);

        Assert.Equal(2, Assert.Single(approvals.StateReadSizes));
        Assert.Equal("Cancelled", Assert.Single(items, i => i.Title.Text == "Refused task").NormalizedStatus);
        Assert.DoesNotContain("Cancelled", items.Where(i => i.Title.Text != "Refused task").Select(i => i.NormalizedStatus));
    }

    [Fact]
    public async Task A_task_the_user_already_closed_is_not_re_labelled_by_a_late_rejection()
    {
        // The task's own terminal state wins: a Done task must not flip to Cancelled because the approval was
        // refused afterwards.
        var task = ApprovalTask(Instance);
        task.Lifecycle = TaskLifecycle.Done;
        task.CompletedAt = DateTimeOffset.UtcNow;
        var approvals = new FakeTaskApprovalService().Rejected(Instance);

        var item = Assert.Single(await Provider(task, approvals).GetWorkItemsAsync(Actor(), CancellationToken.None));

        Assert.Equal("Done", item.NormalizedStatus);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static TaskWorkItemProvider Provider(TaskItem task, FakeTaskApprovalService approvals)
        => Provider([task], approvals);

    private static TaskWorkItemProvider Provider(TaskItem[] tasks, FakeTaskApprovalService approvals)
        => new(
            new FakeTaskItemRepository(tasks),
            new FakePositionAssignmentRepository(),
            new TaskLifecycleService(),
            new TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver(),
            new FakeChecklistRunRepository(),
            approvals, new FakeTaskDependencyRepository(), new FakeTaskCommentRepository(), new FakeTaskTransitionRepository(), TaskActors.PermitAll(), new FakePositionRepository(), new FakeOrganizationUnitRepository(), SlaForTests.Real(), new FakeTaskFieldDefinitionRepository());

    // Platform actor: permissions are covered by ProviderActionPermissionTests, and bypassing them here keeps
    // approval the only variable.
    private static WorkItemActor Actor()
        => new(TaskTestData.Me, IsPlatformActor: true, new HashSet<string>());

    private static TaskItem ApprovalTask(Guid workflowInstanceId) => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "Needs a manager's approval",
        Lifecycle = TaskLifecycle.Open,
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = TaskTestData.Me,
        OrganizationUnitId = Guid.NewGuid(),
        ApprovalRequired = true,
        ApprovalManagerUserId = TaskTestData.Rival,
        WorkflowInstanceId = workflowInstanceId,
        Version = 1
    };
}
