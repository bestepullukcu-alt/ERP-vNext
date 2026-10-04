using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Services;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Enums.Workflow;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WP-WF-ENGINE-CANDIDATES-01 · D2 fix round 3 (CT-WF-D2-FIX3) — the orphan paths: a live approval or review the task
/// stops pointing at, or leaves behind, while MOD-0023 still lets somebody decide it. Same real chain as round 2 (the real
/// withdraw services, the real gate over in-memory MOD-0023 stores); only MOD-0023's cancel command is answered here.
/// </summary>
public sealed partial class TaskWorkflowLinkGateTests
{
    // ── item 1 · a delete withdraws first; a failed withdraw refuses it (bulk: skips it) ──────────────────────────

    [Fact]
    public async Task Deleting_a_task_withdraws_its_running_approval_and_a_withdraw_that_fails_refuses_the_delete()
    {
        var f = new Fixture(TaskLifecycle.Open, approval: WorkflowInstanceStatus.Active);
        f.Engine.Down = true;

        var refused = await f.DeleteAsync();

        Assert.Equal(409, refused.StatusCode);
        Assert.Equal(TaskReasonCodes.ApprovalCancelFailed, refused.ReasonCode);
        Assert.False(f.IsDeleted());
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, f.Engine.TaskOf(f.ApprovalInstance!).Status);

        f.Engine.Down = false;
        Assert.Equal(204, (await f.DeleteAsync()).StatusCode);
        Assert.True(f.IsDeleted());
        Assert.Equal(ApprovalTaskStatus.Cancelled, f.Engine.TaskOf(f.ApprovalInstance!).Status);
    }

    [Fact]
    public async Task Deleting_a_task_under_review_withdraws_the_review_and_a_withdraw_that_fails_refuses_the_delete()
    {
        var f = new Fixture(TaskLifecycle.PendingReview, review: WorkflowInstanceStatus.Active);
        f.Engine.Down = true;

        var refused = await f.DeleteAsync();

        Assert.Equal(409, refused.StatusCode);
        Assert.Equal(TaskReasonCodes.ReviewCancelFailed, refused.ReasonCode);
        Assert.False(f.IsDeleted());

        f.Engine.Down = false;
        Assert.Equal(204, (await f.DeleteAsync()).StatusCode);
        Assert.Equal(ApprovalTaskStatus.Cancelled, f.Engine.TaskOf(f.ReviewInstance!).Status);
    }

    [Fact]
    public async Task A_bulk_delete_leaves_out_a_task_whose_withdraw_failed_deletes_the_rest_and_answers_alike()
    {
        var f = new Fixture(TaskLifecycle.Open, approval: WorkflowInstanceStatus.Active);
        var plain = f.AddPlainTask();
        f.Engine.Down = true;

        var response = await f.BulkDeleteAsync(f.Task.Id, plain.Id, Guid.NewGuid());

        Assert.Equal(204, response.StatusCode);              // the same answer whatever was left out — nothing leaks
        Assert.False(f.IsDeleted());                          // its approval is still decidable: not deleted around it
        Assert.True(f.IsDeleted(plain.Id));
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, f.Engine.TaskOf(f.ApprovalInstance!).Status);
    }

    [Fact]
    public async Task A_delete_handler_built_without_the_withdraw_services_refuses_to_delete_a_linked_task()
    {
        var linked = new Fixture(TaskLifecycle.Open, approval: WorkflowInstanceStatus.Active, withWithdrawServices: false);
        var plain = new Fixture(TaskLifecycle.Open, withWithdrawServices: false);

        Assert.Equal(409, (await linked.DeleteAsync()).StatusCode);
        Assert.False(linked.IsDeleted());
        Assert.Equal(204, (await plain.DeleteAsync()).StatusCode);
    }

    // ── item 2 · return / reassign / release withdraw the review; submit reads a held link in every lifecycle ────

    [Theory]
    [InlineData("return")]
    [InlineData("reassign")]
    [InlineData("release")]
    public async Task A_hand_over_from_review_withdraws_the_review_and_a_withdraw_that_fails_refuses_it(string handover)
    {
        var f = new Fixture(TaskLifecycle.PendingReview, review: WorkflowInstanceStatus.Active, createdBy: TaskTestData.Other);
        if (handover == "release")
        {
            f.MakePooled();
        }

        f.AllowDelegation();

        f.Engine.Down = true;

        var refused = await f.HandOverAsync(handover);

        Assert.Equal(409, refused.StatusCode);
        Assert.Equal(TaskReasonCodes.ReviewCancelFailed, refused.ReasonCode);
        var untouched = f.Stored();
        Assert.Equal(TaskLifecycle.PendingReview, untouched.Lifecycle);
        Assert.Equal(TaskTestData.Me, untouched.AssigneeUserId);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, f.Engine.TaskOf(f.ReviewInstance!).Status);

        f.Engine.Down = false;
        var handed = await f.HandOverAsync(handover);

        Assert.Equal(204, handed.StatusCode);
        Assert.Equal(ApprovalTaskStatus.Cancelled, f.Engine.TaskOf(f.ReviewInstance!).Status);
        var stored = f.Stored();
        Assert.Equal(TaskLifecycle.Open, stored.Lifecycle);
        Assert.NotEqual(TaskTestData.Me, stored.AssigneeUserId);
        // The link is KEPT, on the withdrawn review: the next submission reads it instead of replaying round one's key.
        Assert.Equal(f.ReviewInstance!.Id, stored.ReviewWorkflowInstanceId);
    }

    [Fact]
    public async Task A_hand_over_on_a_stale_version_withdraws_nothing()
    {
        var f = new Fixture(TaskLifecycle.PendingReview, review: WorkflowInstanceStatus.Active, createdBy: TaskTestData.Other);

        var stale = await f.HandOverAsync("return", expectedVersion: f.Task.Version - 1);

        Assert.Equal(409, stale.StatusCode);
        Assert.Equal(TaskReasonCodes.ConcurrencyConflict, stale.ReasonCode);
        Assert.Empty(f.Engine.CancelCalls);
    }

    [Fact]
    public async Task A_return_handler_built_without_the_review_service_refuses_to_hand_over_a_task_under_review()
    {
        var f = new Fixture(TaskLifecycle.PendingReview, review: WorkflowInstanceStatus.Active, createdBy: TaskTestData.Other,
            withWithdrawServices: false);

        var refused = await f.HandOverAsync("return");

        Assert.Equal(409, refused.StatusCode);
        Assert.Equal(TaskReasonCodes.ReviewCancelFailed, refused.ReasonCode);
        Assert.Equal(TaskTestData.Me, f.Stored().AssigneeUserId);
    }

    [Fact]
    public async Task The_next_holder_resubmits_a_returned_task_in_a_new_round_and_no_review_is_left_open()
    {
        var f = new Fixture(TaskLifecycle.PendingReview, review: WorkflowInstanceStatus.Active, createdBy: TaskTestData.Other);
        Assert.Equal(204, (await f.HandOverAsync("return")).StatusCode);
        Assert.Equal(204, (await f.AcceptAsAsync(TaskTestData.Other)).StatusCode);
        var starter = new FakeTaskReviewService { InstanceId = Guid.NewGuid() };

        var submitted = await f.SubmitAsAsync(TaskTestData.Other, starter);

        Assert.Equal(204, submitted.StatusCode);
        Assert.Single(starter.Started);
        var stored = f.Stored();
        Assert.Equal(TaskLifecycle.PendingReview, stored.Lifecycle);
        Assert.Equal(starter.InstanceId, stored.ReviewWorkflowInstanceId);
        // The first round is closed, not overwritten while still decidable.
        Assert.Equal(ApprovalTaskStatus.Cancelled, f.Engine.TaskOf(f.ReviewInstance!).Status);
    }

    [Theory]
    [InlineData(TaskLifecycle.InProgress)]
    [InlineData(TaskLifecycle.Open)]
    public async Task Submitting_while_a_held_review_is_still_open_is_refused_in_any_lifecycle(TaskLifecycle lifecycle)
    {
        // The state a hand-over left before D2-FIX3: the lifecycle rewound, the first review still decidable.
        var f = new Fixture(lifecycle, review: WorkflowInstanceStatus.Active);
        var starter = new FakeTaskReviewService { InstanceId = Guid.NewGuid() };

        var refused = await f.SubmitAsAsync(TaskTestData.Me, starter);

        Assert.Equal(409, refused.StatusCode);
        Assert.Equal(TaskReasonCodes.ReviewPending, refused.ReasonCode);
        Assert.Empty(starter.Started);                        // no second instance
        Assert.Equal(f.ReviewInstance!.Id, f.Stored().ReviewWorkflowInstanceId);
    }

    // ── item 6 · review first, approval last; the code says how far it got ───────────────────────────────────────

    [Fact]
    public async Task A_cancel_whose_approval_withdraw_fails_after_the_review_was_withdrawn_says_so_and_keeps_both_links()
    {
        var f = new Fixture(TaskLifecycle.PendingReview, approval: WorkflowInstanceStatus.Active, review: WorkflowInstanceStatus.Active);
        f.Engine.FailFor.Add(f.ApprovalInstance!.Id);

        var refused = await f.MoveAsync(TaskLifecycle.Cancelled);

        Assert.Equal(409, refused.StatusCode);
        Assert.Equal(TaskReasonCodes.ApprovalCancelFailed, refused.ReasonCode);   // the approval is the one still open
        Assert.Equal(ApprovalTaskStatus.Cancelled, f.Engine.TaskOf(f.ReviewInstance!).Status);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, f.Engine.TaskOf(f.ApprovalInstance!).Status);
        var stored = f.Stored();
        Assert.Null(stored.CancelledAt);
        Assert.Equal(f.ApprovalInstance.Id, stored.WorkflowInstanceId);          // still gating the task
        Assert.Equal(f.ReviewInstance.Id, stored.ReviewWorkflowInstanceId);      // read by the next submission

        f.Engine.FailFor.Clear();
        Assert.Equal(204, (await f.MoveAsync(TaskLifecycle.Cancelled)).StatusCode);
        Assert.Equal(ApprovalTaskStatus.Cancelled, f.Engine.TaskOf(f.ApprovalInstance).Status);
    }

    [Fact]
    public async Task A_cancel_whose_review_withdraw_fails_never_touches_the_approval()
    {
        var f = new Fixture(TaskLifecycle.PendingReview, approval: WorkflowInstanceStatus.Active, review: WorkflowInstanceStatus.Active);
        f.Engine.FailFor.Add(f.ReviewInstance!.Id);

        var refused = await f.MoveAsync(TaskLifecycle.Cancelled);

        Assert.Equal(409, refused.StatusCode);
        Assert.Equal(TaskReasonCodes.ReviewCancelFailed, refused.ReasonCode);
        // The approval cannot be redone once withdrawn (its key is the task's own), so it goes last — never asked here.
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, f.Engine.TaskOf(f.ApprovalInstance!).Status);
        Assert.DoesNotContain(f.Engine.CancelCalls, c => c.TaskId == f.Engine.TaskOf(f.ApprovalInstance).Id);
    }

    // ── item 8 · a dropped link is found by the object reference; the round-2 rules that had no test ────────────

    [Fact]
    public async Task Cancelling_a_task_whose_approval_link_was_never_stored_withdraws_the_approval_found_by_its_reference()
    {
        var f = new Fixture(TaskLifecycle.Open);
        var dropped = f.SeedDroppedApproval();

        Assert.Equal(204, (await f.MoveAsync(TaskLifecycle.Cancelled)).StatusCode);

        Assert.Equal(ApprovalTaskStatus.Cancelled, f.Engine.TaskOf(dropped).Status);
    }

    [Fact]
    public async Task Switching_approval_off_withdraws_an_approval_whose_link_was_never_stored_and_a_failed_withdraw_stores_the_link()
    {
        var f = new Fixture(TaskLifecycle.Open);
        var dropped = f.SeedDroppedApproval();
        f.Engine.Down = true;

        var refused = await f.EditAsync(approvalRequired: false, expectedVersion: f.Task.Version);

        Assert.Equal(409, refused.StatusCode);
        Assert.Equal(TaskReasonCodes.ApprovalCancelFailed, refused.ReasonCode);
        Assert.Equal(dropped.Id, f.Stored().WorkflowInstanceId);             // adopted: the retry has a link to follow
        Assert.Equal(f.Stored().Version, refused.Data!.Version);

        f.Engine.Down = false;
        Assert.Equal(204, (await f.EditAsync(approvalRequired: false, expectedVersion: refused.Data.Version)).StatusCode);
        Assert.Equal(ApprovalTaskStatus.Cancelled, f.Engine.TaskOf(dropped).Status);
        Assert.Null(f.Stored().WorkflowInstanceId);
    }

    [Fact]
    public async Task A_hand_over_withdraws_a_review_whose_link_was_never_stored_and_keeps_it_as_the_link()
    {
        var f = new Fixture(TaskLifecycle.InProgress, createdBy: TaskTestData.Other);
        var dropped = f.SeedDroppedReview();

        Assert.Equal(204, (await f.HandOverAsync("return")).StatusCode);

        Assert.Equal(ApprovalTaskStatus.Cancelled, f.Engine.TaskOf(dropped).Status);
        Assert.Equal(dropped.Id, f.Stored().ReviewWorkflowInstanceId);
    }

    [Fact]
    public async Task A_handler_built_without_the_withdraw_services_refuses_to_cancel_a_task_holding_a_review_link()
    {
        // The review half of round 2's fail-closed rule (TransH:789-790 had no test of its own).
        var linked = new Fixture(TaskLifecycle.PendingReview, review: WorkflowInstanceStatus.Active, withWithdrawServices: false);

        var refused = await linked.MoveAsync(TaskLifecycle.Cancelled);

        Assert.Equal(409, refused.StatusCode);
        Assert.Equal(TaskReasonCodes.ReviewCancelFailed, refused.ReasonCode);
        Assert.Null(linked.Stored().CancelledAt);
    }

    [Fact]
    public async Task Cancelling_a_parent_leaves_open_a_subtask_whose_review_could_not_be_withdrawn()
    {
        // TransH:845's review clause: a subtask is not closed around a review that is still decidable.
        var f = new Fixture(TaskLifecycle.Open);
        var child = f.AddSubtaskWithReview();
        f.Engine.Down = true;

        Assert.Equal(204, (await f.MoveAsync(TaskLifecycle.Cancelled)).StatusCode);

        Assert.Equal(TaskLifecycle.PendingReview, f.Stored(child.Task.Id).Lifecycle);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, f.Engine.TaskOf(child.Instance).Status);
    }

    [Theory]
    [InlineData(WorkflowInstanceStatus.Cancelled)]
    [InlineData(WorkflowInstanceStatus.Rejected)]
    public async Task A_review_link_to_a_closed_review_holds_nothing_once_the_requirement_is_off(WorkflowInstanceStatus closed)
    {
        // TransH:491 — Holds(reviewGate, task.ReviewRequired): withdrawn requirement + closed review → completion goes.
        var withdrawn = new Fixture(TaskLifecycle.PendingReview, review: closed, reviewRequired: false);
        Assert.Equal(204, (await withdrawn.MoveAsync(TaskLifecycle.Done)).StatusCode);

        // Required, the same closed review still holds it.
        var required = new Fixture(TaskLifecycle.PendingReview, review: closed, reviewRequired: true);
        var refused = await required.MoveAsync(TaskLifecycle.Done);
        Assert.Equal(409, refused.StatusCode);
        Assert.Equal(TaskReasonCodes.ReviewPending, refused.ReasonCode);
    }

    // ── fixture, round 3 ──────────────────────────────────────────────────────────────────────────────────────────

    private sealed partial class Fixture
    {
        public FakeTenantContext Tenant { get; }
        public FakeCurrentUserContext CurrentUser { get; }
        public TaskApprovalService Approvals { get; }
        public TaskReviewService Reviews { get; }
        public TaskFieldDefinitionService Fields { get; }
        public bool WithdrawServices { get; }

        private TaskApprovalService? WithdrawApprovals => WithdrawServices ? Approvals : null;
        private TaskReviewService? WithdrawReviews => WithdrawServices ? Reviews : null;

        public bool IsDeleted(Guid? id = null) => Raw(id).IsDeleted;

        /// <summary>The STORED document itself (Stored() hands out a detached copy): what a seed step has to change.</summary>
        private TaskItem Raw(Guid? id = null) => _tasks.Items.Single(t => t.Id == (id ?? Task.Id));

        public TaskItem AddPlainTask()
        {
            var plain = new TaskItem
            {
                TenantId = TaskTestData.Tenant,
                Title = "Nothing in MOD-0023",
                Lifecycle = TaskLifecycle.Open,
                AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
                AssigneeUserId = TaskTestData.Me,
                CreatedByUserId = TaskTestData.Me,
                OrganizationUnitId = Unit
            };
            _tasks.CreateAsync(plain).GetAwaiter().GetResult();
            return plain;
        }

        public void MakePooled()
        {
            var stored = Raw();
            stored.AssignmentTarget = TaskAssignmentTarget.PositionPool;
            stored.PoolPositionId = Guid.NewGuid();
        }

        /// <summary>The holder may pass it on (a requester other than the holder created it).</summary>
        public void AllowDelegation() => Raw().DelegationAllowed = true;

        /// <summary>A started approval whose link write fell over: requirement on, instance live, no link on the task.</summary>
        public WorkflowInstance SeedDroppedApproval()
        {
            var instance = Engine.Seed(TaskApprovalService.ApprovalObjectType,
                TaskApprovalService.BuildObjectRef(Task.Id), Task.Id, WorkflowInstanceStatus.Active);
            var stored = Raw();
            stored.ApprovalRequired = true;
            stored.ApprovalManagerUserId = TaskTestData.Rival;
            stored.WorkflowInstanceId = null;
            return instance;
        }

        /// <summary>A submission that lost its race after the engine started the review: requirement on, no link.</summary>
        public WorkflowInstance SeedDroppedReview()
        {
            var instance = Engine.Seed(TaskReviewService.ReviewObjectType,
                TaskReviewService.BuildObjectRef(Task.Id), Task.Id, WorkflowInstanceStatus.Active);
            var stored = Raw();
            stored.ReviewRequired = true;
            stored.ReviewerCandidateUserId = TaskTestData.Rival;
            stored.ReviewWorkflowInstanceId = null;
            return instance;
        }

        public (TaskItem Task, WorkflowInstance Instance) AddSubtaskWithReview()
        {
            var child = new TaskItem
            {
                TenantId = TaskTestData.Tenant,
                Title = "Subtask under review",
                Lifecycle = TaskLifecycle.PendingReview,
                AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
                AssigneeUserId = TaskTestData.Me,
                CreatedByUserId = TaskTestData.Me,
                OrganizationUnitId = Unit,
                ParentTaskItemId = Task.Id,
                ReviewRequired = true,
                ReviewerCandidateUserId = TaskTestData.Rival
            };
            var instance = Engine.Seed(TaskReviewService.ReviewObjectType,
                TaskReviewService.BuildObjectRef(child.Id), child.Id, WorkflowInstanceStatus.Active);
            child.ReviewWorkflowInstanceId = instance.Id;
            _tasks.CreateAsync(child).GetAwaiter().GetResult();
            return (child, instance);
        }

        public Task<Response<NoContent>> DeleteAsync() =>
            new DeleteTaskItemHandler(_tasks, WithdrawApprovals, WithdrawReviews)
                .Handle(new DeleteTaskItemCommand(Task.Id, "corr-delete"), CancellationToken.None);

        public Task<Response<NoContent>> BulkDeleteAsync(params Guid[] ids) =>
            new BulkDeleteTaskItemHandler(_tasks, WithdrawApprovals, WithdrawReviews,
                    NullLogger<BulkDeleteTaskItemHandler>.Instance)
                .Handle(new BulkDeleteTaskItemCommand(new BulkDeleteTaskItemRequest(ids), "corr-bulk"), CancellationToken.None);

        public Task<Response<NoContent>> HandOverAsync(string handover, int? expectedVersion = null)
        {
            var version = expectedVersion ?? Stored().Version;
            return handover switch
            {
                "return" => new ReturnTaskItemHandler(_tasks, new FakeTaskAssignmentRepository(), CurrentUser, Tenant,
                        WithdrawReviews)
                    .Handle(new ReturnTaskItemCommand(Task.Id, new ReturnTaskItemRequest(version, "Not mine"), "corr-return"),
                        CancellationToken.None),
                "reassign" => new ReassignTaskItemHandler(_tasks, new FakeTaskAssignmentRepository(),
                        TaskAssignmentGuards.AdmitAll(), CurrentUser, Tenant, WithdrawReviews)
                    .Handle(new ReassignTaskItemCommand(Task.Id,
                        new ReassignTaskItemRequest(version, TaskTestData.Other, "Their skill"), "corr-reassign"),
                        CancellationToken.None),
                "release" => new ReleaseTaskItemHandler(_tasks, new FakeTaskAssignmentRepository(), CurrentUser, Tenant,
                        WithdrawReviews)
                    .Handle(new ReleaseTaskItemCommand(Task.Id, new TaskTransitionRequest(version, null, null), "corr-release"),
                        CancellationToken.None),
                _ => throw new ArgumentOutOfRangeException(nameof(handover))
            };
        }

        public Task<Response<NoContent>> AcceptAsAsync(Guid user) =>
            new AcceptTaskItemHandler(_tasks, new FakeTaskAssignmentRepository(), new FakeCurrentUserContext(user), Tenant)
                .Handle(new AcceptTaskItemCommand(Task.Id, new TaskTransitionRequest(Stored().Version, null, null), "corr-accept"),
                    CancellationToken.None);

        /// <summary>Submit for review as <paramref name="user"/>: the held link is read through the REAL state read
        /// (MOD-0023's instance as stored), the new round is opened by <paramref name="starter"/>.</summary>
        public Task<Response<NoContent>> SubmitAsAsync(Guid user, ITaskReviewService starter) =>
            new SubmitTaskForReviewHandler(_tasks, new TaskLifecycleService(), new FakeCurrentUserContext(user), starter,
                    Approvals, NullLogger<SubmitTaskForReviewHandler>.Instance)
                .Handle(new SubmitTaskForReviewCommand(Task.Id, new TaskTransitionRequest(Stored().Version, null, null), "corr-submit"),
                    CancellationToken.None);
    }
}
