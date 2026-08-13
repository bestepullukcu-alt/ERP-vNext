using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// Phase 3 — approval switched on or off by an EDIT (charter Binding A, pack §12 K2).
///
/// <para>Creation was already covered; the edit path was not, so a task whose approval requirement changed after
/// creation either never started a MOD-0023 instance or left one running forever. Both directions are asserted
/// here, together with the two cases where MOD-0023 must NOT be touched at all: a payload that does not mention
/// approval, and an edit that lost its concurrency race.</para>
/// </summary>
public sealed class TaskApprovalToggleTests
{
    private static readonly Guid Manager = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid Unit = Guid.Parse("0f0f0f0f-0f0f-0f0f-0f0f-0f0f0f0f0f0f");
    private static readonly Guid RunningInstance = Guid.Parse("beeff00d-1111-1111-1111-111111111111");

    [Fact]
    public async Task Switching_approval_ON_starts_a_workflow_and_keeps_its_instance_id_as_the_only_link()
    {
        var task = PlainTask();
        var (handler, tasks, approvals) = Handler(task);

        var response = await handler.Handle(Update(task, approvalRequired: true, manager: Manager), CancellationToken.None);

        Assert.Equal(204, response.StatusCode);
        Assert.Equal(task.Id, Assert.Single(approvals.Started));
        var stored = await tasks.GetByIdAsync(task.Id, CancellationToken.None);
        Assert.True(stored!.ApprovalRequired);
        Assert.Equal(Manager, stored.ApprovalManagerUserId);
        Assert.Equal(approvals.InstanceId, stored.WorkflowInstanceId);
    }

    [Fact]
    public async Task Switching_approval_OFF_cancels_the_running_workflow_and_clears_the_link()
    {
        var task = ApprovalTask();
        var (handler, tasks, approvals) = Handler(task);

        var response = await handler.Handle(Update(task, approvalRequired: false), CancellationToken.None);

        Assert.Equal(204, response.StatusCode);
        Assert.Equal(task.Id, Assert.Single(approvals.Cancelled));
        var stored = await tasks.GetByIdAsync(task.Id, CancellationToken.None);
        Assert.False(stored!.ApprovalRequired);
        Assert.Null(stored.WorkflowInstanceId);
        Assert.Empty(approvals.Started);
    }

    [Fact]
    public async Task An_edit_that_does_NOT_mention_approval_leaves_a_running_approval_untouched()
    {
        // The trap this guards: a form that never renders the toggle would post approvalRequired:false and silently
        // cancel a live approval. NULL means "not editing approval".
        var task = ApprovalTask();
        var (handler, tasks, approvals) = Handler(task);

        var response = await handler.Handle(Update(task, approvalRequired: null), CancellationToken.None);

        Assert.Equal(204, response.StatusCode);
        Assert.Empty(approvals.Started);
        Assert.Empty(approvals.Cancelled);
        var stored = await tasks.GetByIdAsync(task.Id, CancellationToken.None);
        Assert.True(stored!.ApprovalRequired);
        Assert.Equal(RunningInstance, stored.WorkflowInstanceId);
    }

    [Fact]
    public async Task Re_sending_the_SAME_value_starts_nothing_and_cancels_nothing()
    {
        var task = ApprovalTask();
        var (handler, _, approvals) = Handler(task);

        await handler.Handle(Update(task, approvalRequired: true), CancellationToken.None);

        // Only a CHANGE is a handoff; an idempotent re-save must not spawn a second instance.
        Assert.Empty(approvals.Started);
        Assert.Empty(approvals.Cancelled);
    }

    [Fact]
    public async Task Switching_ON_without_any_approver_is_refused_before_a_workflow_is_started()
    {
        var task = PlainTask();
        var (handler, tasks, approvals) = Handler(task);

        var response = await handler.Handle(Update(task, approvalRequired: true, manager: null), CancellationToken.None);

        Assert.Equal(400, response.StatusCode);
        Assert.Equal(TaskReasonCodes.ValidationFailed, response.ReasonCode);
        Assert.Empty(approvals.Started);
        // And the edit did not land either — the task is unchanged.
        var stored = await tasks.GetByIdAsync(task.Id, CancellationToken.None);
        Assert.False(stored!.ApprovalRequired);
    }

    [Fact]
    public async Task Switching_ON_reuses_the_approver_already_on_the_task()
    {
        var task = PlainTask();
        task.ApprovalManagerUserId = Manager;   // chosen in an earlier round, then approval was turned off
        var (handler, _, approvals) = Handler(task);

        var response = await handler.Handle(Update(task, approvalRequired: true, manager: null), CancellationToken.None);

        Assert.Equal(204, response.StatusCode);
        Assert.Single(approvals.Started);
    }

    [Fact]
    public async Task A_workflow_that_cannot_start_keeps_the_edit_and_leaves_the_task_BLOCKED()
    {
        var task = PlainTask();
        var (handler, tasks, approvals) = Handler(task);
        approvals.CannotStart = true;

        var response = await handler.Handle(
            Update(task, approvalRequired: true, manager: Manager, title: "Edited while MOD-0023 was down"),
            CancellationToken.None);

        // The user's work survives; the requirement stays true with NO instance id, so the fail-closed gate keeps
        // `start` shut until the approval is retried. Reporting success while silently dropping approval would be
        // the dangerous outcome.
        Assert.Equal(204, response.StatusCode);
        var stored = await tasks.GetByIdAsync(task.Id, CancellationToken.None);
        Assert.Equal("Edited while MOD-0023 was down", stored!.Title);
        Assert.True(stored.ApprovalRequired);
        Assert.Null(stored.WorkflowInstanceId);
    }

    [Fact]
    public async Task An_edit_that_loses_the_concurrency_race_touches_MOD_0023_at_all()
    {
        var task = ApprovalTask();
        var (handler, _, approvals) = Handler(task);

        var response = await handler.Handle(
            Update(task, approvalRequired: false, expectedVersion: task.Version + 7), CancellationToken.None);

        Assert.Equal(409, response.StatusCode);
        Assert.Equal(TaskReasonCodes.ConcurrencyConflict, response.ReasonCode);
        // Nothing was started or cancelled: the handoff happens only after the write is known to have landed.
        Assert.Empty(approvals.Started);
        Assert.Empty(approvals.Cancelled);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static (UpdateTaskItemHandler Handler, FakeTaskItemRepository Tasks, FakeTaskApprovalService Approvals)
        Handler(TaskItem task)
    {
        var tasks = new FakeTaskItemRepository(task);
        var approvals = new FakeTaskApprovalService();
        var handler = new UpdateTaskItemHandler(
            tasks,
            new FakeOrganizationUnitRepository(new OrganizationUnit
            {
                Id = Unit,
                TenantId = TaskTestData.Tenant,
                Name = "HQ",
                Code = "HQ",
                LegalEntityId = Guid.NewGuid()
            }),
            new TaskFieldDefinitionService(new FakeTaskFieldDefinitionRepository(), TaskRecordSourceDoubles.None, TaskActors.PermitAll()),
            new FakeCurrentUserContext(TaskTestData.Me),
            approvals,
            new FakeTaskReviewService(),
            NullLogger<UpdateTaskItemHandler>.Instance);
        return (handler, tasks, approvals);
    }

    private static UpdateTaskItemCommand Update(
        TaskItem task,
        bool? approvalRequired,
        Guid? manager = null,
        string title = "Edited title",
        int? expectedVersion = null)
        => new(
            task.Id,
            new UpdateTaskItemRequest(
                Title: title,
                Description: null,
                Priority: TaskPriority.Medium,
                OrganizationUnitId: null,
                DueAt: null,
                StartAt: null,
                PlannedDate: null,
                EstimateHours: null,
                Tags: null,
                ReviewRequired: false,
                EmailNotificationsEnabled: false,
                DelegationAllowed: false,
                FieldValues: null,
                ExpectedVersion: expectedVersion ?? task.Version,
                ApprovalRequired: approvalRequired,
                ApprovalManagerUserId: manager),
            Guid.NewGuid().ToString());

    private static TaskItem PlainTask() => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "Write the report",
        Lifecycle = TaskLifecycle.Open,
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = TaskTestData.Me,
        OrganizationUnitId = Unit,
        Version = 3
    };

    private static TaskItem ApprovalTask()
    {
        var task = PlainTask();
        task.ApprovalRequired = true;
        task.ApprovalManagerUserId = Manager;
        task.WorkflowInstanceId = RunningInstance;
        return task;
    }
}
