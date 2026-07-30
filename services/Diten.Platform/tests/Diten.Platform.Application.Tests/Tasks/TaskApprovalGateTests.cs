using System.Reflection;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// MOD-0024 Phase 3 — approval delegated to MOD-0023 (pack §12 K2, charter Binding A).
///
/// <para>The charter's rule is that MOD-0024 owns NO approval engine. These tests hold that line from both
/// directions: the gate must actually stop a transition (and stop it server-side, not merely in the UI), and
/// MOD-0024 must not grow an approval-state field of its own.</para>
/// </summary>
public sealed class TaskApprovalGateTests
{
    // ── The gate actually blocks, and blocks the COMMIT ───────────────────────

    [Fact]
    public async Task An_approval_gated_task_cannot_be_started_while_the_gate_blocks()
    {
        var task = ApprovalTask();
        var tasks = new FakeTaskItemRepository(task);
        var gate = new FakeWorkflowTransitionGate { Blocked = true };

        var result = await Transition(tasks, gate, task.Id, TaskLifecycle.InProgress, task.Version);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        // Nothing was committed — the block is not cosmetic.
        Assert.Equal(TaskLifecycle.Open, tasks.Items.Single().Lifecycle);
    }

    [Fact]
    public async Task Once_approval_allows_it_the_task_starts()
    {
        var task = ApprovalTask();
        var tasks = new FakeTaskItemRepository(task);
        var gate = new FakeWorkflowTransitionGate { Blocked = false };

        var result = await Transition(tasks, gate, task.Id, TaskLifecycle.InProgress, task.Version);

        Assert.True(result.IsSuccessful);
        Assert.Equal(TaskLifecycle.InProgress, tasks.Items.Single().Lifecycle);
    }

    [Fact]
    public async Task A_gate_that_cannot_be_evaluated_BLOCKS_the_transition()
    {
        // Fail-closed is the whole point: a workflow outage must never become an approval bypass.
        var task = ApprovalTask();
        var tasks = new FakeTaskItemRepository(task);
        var gate = new FakeWorkflowTransitionGate { Throws = true };

        var result = await Transition(tasks, gate, task.Id, TaskLifecycle.InProgress, task.Version);

        Assert.False(result.IsSuccessful);
        Assert.Equal(TaskLifecycle.Open, tasks.Items.Single().Lifecycle);
    }

    [Fact]
    public async Task Completion_is_gated_too_not_just_the_start()
    {
        var task = ApprovalTask();
        task.Lifecycle = TaskLifecycle.InProgress;
        var tasks = new FakeTaskItemRepository(task);
        var gate = new FakeWorkflowTransitionGate { Blocked = true };

        var result = await Transition(tasks, gate, task.Id, TaskLifecycle.Done, task.Version);

        Assert.False(result.IsSuccessful);
        Assert.Equal(TaskLifecycle.InProgress, tasks.Items.Single().Lifecycle);
    }

    [Fact]
    public async Task A_task_WITHOUT_approval_never_touches_the_gate()
    {
        // No approval requirement means no cost and no dependency on the workflow engine at all.
        var task = ApprovalTask();
        task.ApprovalRequired = false;
        task.ApprovalManagerUserId = null;
        var tasks = new FakeTaskItemRepository(task);
        var gate = new FakeWorkflowTransitionGate { Blocked = true };

        var result = await Transition(tasks, gate, task.Id, TaskLifecycle.InProgress, task.Version);

        Assert.True(result.IsSuccessful);
        Assert.Empty(gate.Calls);
    }

    [Fact]
    public async Task Cancelling_is_never_gated()
    {
        // Calling work off must not require the approver's blessing, or a rejected task could never be closed.
        var task = ApprovalTask();
        var tasks = new FakeTaskItemRepository(task);
        var gate = new FakeWorkflowTransitionGate { Blocked = true };

        var result = await Transition(tasks, gate, task.Id, TaskLifecycle.Cancelled, task.Version);

        Assert.True(result.IsSuccessful);
        Assert.Empty(gate.Calls);
    }

    [Fact]
    public async Task The_gate_is_asked_about_the_TASK_object_with_the_right_transition()
    {
        var task = ApprovalTask();
        var gate = new FakeWorkflowTransitionGate();

        await Transition(new FakeTaskItemRepository(task), gate, task.Id, TaskLifecycle.InProgress, task.Version);

        var call = Assert.Single(gate.Calls);
        Assert.Equal(TaskApprovalService.ApprovalObjectType, call.ObjectType);
        Assert.Equal(task.Id.ToString(), call.ObjectId);
        Assert.Equal(TaskApprovalService.BuildObjectRef(task.Id), call.ObjectRef);
        Assert.Equal("start", call.RequestedTransition);
    }

    // ── MOD-0024 stores no approval state ─────────────────────────────────────

    [Fact]
    public void TaskItem_has_NO_approval_status_field_of_its_own()
    {
        // The charter's line: approval STATE is MOD-0023's. MOD-0024 keeps only the instance link (and the
        // requirement flag), and reflects the OUTCOME in its own lifecycle (rejected → Cancelled).
        var suspicious = typeof(TaskItem)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Where(name =>
                name.Contains("Approval", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Approved", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n)
            .ToArray();

        // Only the requirement flag and the candidate hint — no status, no decision, no approver record.
        Assert.Equal(new[] { "ApprovalManagerUserId", "ApprovalRequired" }, suspicious);
        Assert.DoesNotContain(
            typeof(TaskItem).GetProperties().Select(p => p.Name),
            name => name.Contains("ApprovalStatus", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_only_link_to_the_workflow_is_the_instance_id()
    {
        Assert.NotNull(typeof(TaskItem).GetProperty(nameof(TaskItem.WorkflowInstanceId)));
        Assert.Equal(typeof(Guid?), typeof(TaskItem).GetProperty(nameof(TaskItem.WorkflowInstanceId))!.PropertyType);
    }

    // ── The contract pairing while approval is outstanding ────────────────────

    [Fact]
    public async Task While_approval_is_outstanding_the_item_is_Waiting_with_a_waitingContext_and_stays_Open()
    {
        var task = ApprovalTask();
        var provider = new TaskWorkItemProviderBuilder(task).Build();

        var item = Assert.Single(await provider.GetWorkItemsAsync(
            new Application.Features.WorkAggregation.WorkItemActor(
                TaskTestData.Me, IsPlatformActor: true, new HashSet<string>()),
            CancellationToken.None));

        // The contract enforces Waiting ⇔ waitingContext bidirectionally.
        Assert.Equal("Waiting", item.NormalizedStatus);
        Assert.NotNull(item.WaitingContext);
        Assert.Equal("approval", item.WaitingContext!.Type);
        // The SYSTEM decides this; the native lifecycle is untouched by the approval.
        Assert.Equal(nameof(TaskLifecycle.Open), item.TaskLifecycle);
    }

    [Fact]
    public async Task Start_is_projected_DISABLED_with_a_reason_rather_than_hidden()
    {
        var task = ApprovalTask();
        var provider = new TaskWorkItemProviderBuilder(task).Build();

        var item = Assert.Single(await provider.GetWorkItemsAsync(
            new Application.Features.WorkAggregation.WorkItemActor(
                TaskTestData.Me, IsPlatformActor: true, new HashSet<string>()),
            CancellationToken.None));

        var start = Assert.Single(item.Actions, a => a.Code == "start");
        Assert.False(start.Enabled);
        Assert.NotNull(start.DisabledReasonCode);
        // The browser never derives eligibility; it renders what it is told, with the reason.
        Assert.NotNull(start.DisabledReason);
    }

    // ── Starting the approval on create ───────────────────────────────────────

    [Fact]
    public async Task Creating_an_approval_task_starts_the_workflow_and_stores_the_instance_id()
    {
        var tasks = new FakeTaskItemRepository();
        var approvals = new FakeTaskApprovalService();

        var result = await CreateApprovalTask(tasks, approvals);

        Assert.True(result.IsSuccessful);
        Assert.Single(approvals.Started);
        Assert.Equal(approvals.InstanceId, tasks.Items.Single().WorkflowInstanceId);
    }

    [Fact]
    public async Task When_the_workflow_cannot_start_the_TASK_SURVIVES_and_stays_gated()
    {
        // Throwing away work the user already typed is not an improvement over a task that cannot start yet —
        // and the fail-closed gate means it cannot start un-approved either.
        var tasks = new FakeTaskItemRepository();
        var approvals = new FakeTaskApprovalService { CannotStart = true };

        var result = await CreateApprovalTask(tasks, approvals);

        Assert.True(result.IsSuccessful);
        var stored = tasks.Items.Single();
        Assert.True(stored.ApprovalRequired);
        Assert.Null(stored.WorkflowInstanceId);

        // …and with no instance, the gate still refuses to let it start.
        var blocked = await Transition(
            tasks, new FakeWorkflowTransitionGate { Blocked = true }, stored.Id, TaskLifecycle.InProgress, stored.Version);
        Assert.False(blocked.IsSuccessful);
    }

    [Fact]
    public async Task A_task_without_approval_starts_no_workflow()
    {
        var tasks = new FakeTaskItemRepository();
        var approvals = new FakeTaskApprovalService();

        await CreateApprovalTask(tasks, approvals, approvalRequired: false);

        Assert.Empty(approvals.Started);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Task<Application.Common.Response<Application.Common.NoContent>> Transition(
        FakeTaskItemRepository tasks,
        FakeWorkflowTransitionGate gate,
        Guid id,
        TaskLifecycle target,
        int expectedVersion)
        => new TransitionTaskItemHandler(
                tasks, new TaskLifecycleService(), new FakeCurrentUserContext(TaskTestData.Me),
                new FakeChecklistRunRepository(), new TaskChecklistService(), gate, new FakeTaskDependencyRepository())
            .Handle(
                new TransitionTaskItemCommand(id, target, new TaskTransitionRequest(expectedVersion, null, null), "corr"),
                CancellationToken.None);

    private static Task<Application.Common.Response<Guid>> CreateApprovalTask(
        FakeTaskItemRepository tasks,
        FakeTaskApprovalService approvals,
        bool approvalRequired = true)
    {
        var unitId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var handler = new CreateTaskItemHandler(
            tasks, new FakeTaskAssignmentRepository(), new FakeTaskWatcherRepository(),
            new FakePositionRepository(),
            new FakeOrganizationUnitRepository(new Domain.Entities.Organization.OrganizationUnit
            {
                Id = unitId, TenantId = TaskTestData.Tenant, Code = "ROOT", Name = "Root",
                LegalEntityId = Guid.NewGuid()
            }),
            new FakePositionAssignmentRepository(),
            new TaskFieldDefinitionService(new FakeTaskFieldDefinitionRepository()),
            new TaskLifecycleService(), approvals,
            new FakeChecklistTemplateRepository(), new FakeChecklistRunRepository(), new TaskChecklistService(),
            new NoOpNotificationDispatchAdapter(),
            new FakeCurrentUserContext(TaskTestData.Me), new FakeTenantContext(TaskTestData.Tenant),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CreateTaskItemHandler>.Instance);

        var request = new CreateTaskItemRequest(
            Title: "Needs sign-off", Description: null, Priority: TaskPriority.High,
            AssignmentTarget: TaskAssignmentTarget.SelfAssigned, AssigneeUserId: null, PoolPositionId: null,
            OrganizationUnitId: unitId, DueAt: DateTimeOffset.UtcNow.AddDays(3), StartAt: null, PlannedDate: null,
            EstimateHours: null, Tags: null, ReviewRequired: false,
            ApprovalRequired: approvalRequired,
            ApprovalManagerUserId: approvalRequired ? TaskTestData.Rival : null,
            EmailNotificationsEnabled: false, DelegationAllowed: false, FieldValues: null, Watchers: null);

        return handler.Handle(new CreateTaskItemCommand(request, "corr"), CancellationToken.None);
    }

    private static TaskItem ApprovalTask() => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "Needs sign-off",
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = TaskTestData.Me,
        // A self-assigned task is created by its own assignee; CreateTaskItemHandler always stamps this.
        CreatedByUserId = TaskTestData.Me,
        OrganizationUnitId = Guid.NewGuid(),
        Lifecycle = TaskLifecycle.Open,
        ApprovalRequired = true,
        ApprovalManagerUserId = TaskTestData.Rival,
        WorkflowInstanceId = Guid.Parse("abcdabcd-abcd-abcd-abcd-abcdabcdabcd"),
        Version = 1
    };

    /// <summary>Builds the provider with Phase 1–3 collaborators; only the task under test varies.</summary>
    private sealed class TaskWorkItemProviderBuilder(TaskItem task)
    {
        public Application.Features.Tasks.Providers.TaskWorkItemProvider Build()
            => new(new FakeTaskItemRepository(task), new FakePositionAssignmentRepository(),
                new TaskLifecycleService(), new TaskAssignmentResolver(),
                new FakeUserDisplayNameResolver(), new FakeChecklistRunRepository(), new FakeTaskApprovalService(), new FakeTaskDependencyRepository(), new FakeTaskCommentRepository(), new FakePositionRepository(), new FakeOrganizationUnitRepository(), SlaForTests.Real());
    }
}
