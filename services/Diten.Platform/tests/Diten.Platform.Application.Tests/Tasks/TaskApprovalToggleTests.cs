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

    // ── D2-FIX1 A1 · a withdraw that did not happen keeps the link, and says so ─────────────────────────────

    [Fact]
    public async Task When_the_approval_cannot_be_withdrawn_the_link_is_kept_and_the_answer_is_coded()
    {
        var task = ApprovalTask();
        var (handler, tasks, approvals, _) = HandlerWithReview(task);
        approvals.CannotCancel = true;

        var response = await handler.Handle(Update(task, approvalRequired: false), CancellationToken.None);

        Assert.Equal(409, response.StatusCode);
        Assert.Equal(TaskReasonCodes.ApprovalCancelFailed, response.ReasonCode);
        var stored = await tasks.GetByIdAsync(task.Id, CancellationToken.None);
        // The requirement change is saved; the link is NOT cleared, so the still-open approval is not orphaned.
        Assert.False(stored!.ApprovalRequired);
        Assert.Equal(RunningInstance, stored.WorkflowInstanceId);
        // D2-FIX2 item 2 — and the refusal says at which version the edit landed.
        Assert.Equal(stored.Version, response.Data!.Version);
    }

    [Theory]
    [InlineData("approval")]
    [InlineData("review")]
    public async Task Saving_again_with_the_version_the_refusal_carried_retries_the_withdraw(string which)
    {
        // D2-FIX2 item 2 — exactly what the form sends after the 409: the version in the refusal, not a fresh read.
        var task = which == "approval" ? ApprovalTask() : PlainTask();
        if (which == "review")
        {
            task.ReviewRequired = true;
            task.ReviewerCandidateUserId = Manager;
            task.ReviewWorkflowInstanceId = RunningInstance;
        }

        var (handler, tasks, approvals, reviews) = HandlerWithReview(task);
        approvals.CannotCancel = reviews.CannotCancel = true;
        var refused = await handler.Handle(
            Update(task, approvalRequired: which == "approval" ? false : null), CancellationToken.None);
        Assert.Equal(409, refused.StatusCode);
        approvals.CannotCancel = reviews.CannotCancel = false;

        var again = await handler.Handle(
            Update(task, approvalRequired: which == "approval" ? false : null, expectedVersion: refused.Data!.Version),
            CancellationToken.None);

        Assert.Equal(204, again.StatusCode);
        var stored = (await tasks.GetByIdAsync(task.Id, CancellationToken.None))!;
        Assert.Null(which == "approval" ? stored.WorkflowInstanceId : stored.ReviewWorkflowInstanceId);
    }

    [Fact]
    public async Task A_later_edit_retries_an_approval_withdraw_that_failed()
    {
        var task = ApprovalTask();
        var (handler, tasks, approvals, _) = HandlerWithReview(task);
        approvals.CannotCancel = true;
        await handler.Handle(Update(task, approvalRequired: false), CancellationToken.None);
        approvals.CannotCancel = false;
        var stored = (await tasks.GetByIdAsync(task.Id, CancellationToken.None))!;

        var retry = await handler.Handle(Update(stored, approvalRequired: false), CancellationToken.None);

        Assert.Equal(204, retry.StatusCode);
        Assert.Equal(2, approvals.Cancelled.Count);
        Assert.Null((await tasks.GetByIdAsync(task.Id, CancellationToken.None))!.WorkflowInstanceId);
    }

    [Fact]
    public async Task When_the_review_cannot_be_withdrawn_the_link_is_kept_and_the_answer_is_coded()
    {
        var task = PlainTask();
        task.ReviewRequired = true;
        task.ReviewerCandidateUserId = Manager;
        task.ReviewWorkflowInstanceId = RunningInstance;
        var (handler, tasks, _, reviews) = HandlerWithReview(task);
        reviews.CannotCancel = true;

        var response = await handler.Handle(Update(task, approvalRequired: null), CancellationToken.None);

        Assert.Equal(409, response.StatusCode);
        Assert.Equal(TaskReasonCodes.ReviewCancelFailed, response.ReasonCode);
        Assert.Equal(RunningInstance, (await tasks.GetByIdAsync(task.Id, CancellationToken.None))!.ReviewWorkflowInstanceId);
        Assert.Equal((await tasks.GetByIdAsync(task.Id, CancellationToken.None))!.Version, response.Data!.Version);

        reviews.CannotCancel = false;
        var retry = await handler.Handle(
            Update((await tasks.GetByIdAsync(task.Id, CancellationToken.None))!, approvalRequired: null), CancellationToken.None);
        Assert.Equal(204, retry.StatusCode);
        Assert.Null((await tasks.GetByIdAsync(task.Id, CancellationToken.None))!.ReviewWorkflowInstanceId);
    }

    // ── D2-FIX3 item 8 · the refusal's version counts EVERY write that landed before it (WriteH landedVersion) ─────

    [Fact]
    public async Task A_refused_review_withdraw_after_an_approval_start_carries_the_version_the_link_write_landed_at()
    {
        // One save switches approval ON (its link is written: version + 2) and review OFF (that withdraw fails).
        var task = PlainTask();
        task.ReviewRequired = true;
        task.ReviewerCandidateUserId = Manager;
        task.ReviewWorkflowInstanceId = RunningReview;
        var (handler, tasks, approvals, reviews) = HandlerWithReview(task);
        reviews.CannotCancel = true;

        var refused = await handler.Handle(Update(task, approvalRequired: true, manager: Manager), CancellationToken.None);

        Assert.Equal(409, refused.StatusCode);
        Assert.Equal(TaskReasonCodes.ReviewCancelFailed, refused.ReasonCode);
        var stored = (await tasks.GetByIdAsync(task.Id, CancellationToken.None))!;
        Assert.Equal(approvals.InstanceId, stored.WorkflowInstanceId);  // non-vacuity: the link write did land
        Assert.Equal(stored.Version, refused.Data!.Version);             // … and the refusal counts it
    }

    [Fact]
    public async Task A_refused_approval_withdraw_after_a_review_withdraw_carries_the_version_the_review_write_landed_at()
    {
        // One save switches review OFF (withdrawn, its link cleared: version + 2) and approval OFF (that withdraw fails).
        var task = ApprovalTask();
        task.ReviewRequired = true;
        task.ReviewerCandidateUserId = Manager;
        task.ReviewWorkflowInstanceId = RunningReview;
        var (handler, tasks, approvals, _) = HandlerWithReview(task);
        approvals.CannotCancel = true;

        var refused = await handler.Handle(Update(task, approvalRequired: false), CancellationToken.None);

        Assert.Equal(409, refused.StatusCode);
        Assert.Equal(TaskReasonCodes.ApprovalCancelFailed, refused.ReasonCode);
        var stored = (await tasks.GetByIdAsync(task.Id, CancellationToken.None))!;
        Assert.Null(stored.ReviewWorkflowInstanceId);                    // non-vacuity: the review write did land
        Assert.Equal(stored.Version, refused.Data!.Version);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static readonly Guid RunningReview = Guid.Parse("beeff00d-2222-2222-2222-222222222222");

    private static (UpdateTaskItemHandler Handler, FakeTaskItemRepository Tasks, FakeTaskApprovalService Approvals)
        Handler(TaskItem task)
    {
        var (handler, tasks, approvals, _) = HandlerWithReview(task);
        return (handler, tasks, approvals);
    }

    private static (UpdateTaskItemHandler Handler, FakeTaskItemRepository Tasks, FakeTaskApprovalService Approvals,
        FakeTaskReviewService Reviews) HandlerWithReview(TaskItem task)
    {
        var tasks = new FakeTaskItemRepository(task);
        var approvals = new FakeTaskApprovalService();
        var reviews = new FakeTaskReviewService();
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
            reviews,
            NullLogger<UpdateTaskItemHandler>.Instance,
                TaskDocumentFreezerDoubles.OverAnEmptyRegister(), writeGate: TaskWriteGateDoubles.Open);
        return (handler, tasks, approvals, reviews);
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
