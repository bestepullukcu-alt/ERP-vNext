using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Enums.Workflow;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WP-WF-ENGINE-CANDIDATES-01 · D2 fix round 4 (CT-WF-D2-FIX4) — the last edge cases of the orphan paths: a STALE link that
/// still names an earlier round while a later one is live (item 1), a review the SLA sweep timed out (item 2), an adoption
/// that rode in a second write nobody read (item 4), a delete that deleted around a new start and a subtask cascade that
/// gave up after one lost write (item 7), and the rules of rounds 2 and 3 that had no test of their own (item 8).
/// Same real chain as rounds 2 and 3: the real handlers, the real withdraw services, the real gate over in-memory MOD-0023.
/// </summary>
public sealed partial class TaskWorkflowLinkGateTests
{
    // ── item 1 · the withdraw asks the LATEST instance whatever the link says ────────────────────────────────────

    [Fact]
    public async Task A_cancel_withdraws_the_second_review_round_a_lost_resubmission_left_live_while_the_link_names_the_first()
    {
        // The barrier: the resubmission's task write LOSES its race after the engine opened round two.
        var f = new Fixture(TaskLifecycle.PendingReview, review: WorkflowInstanceStatus.Rejected);
        var second = await f.LoseTheResubmissionAsync();

        Assert.Equal(f.ReviewInstance!.Id, f.Stored().ReviewWorkflowInstanceId);       // non-vacuity: the link is STALE …
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, f.Engine.TaskOf(second).Status); // … and round two is live

        var cancelled = await f.MoveAsync(TaskLifecycle.Cancelled);

        Assert.Equal(204, cancelled.StatusCode);
        Assert.Equal(ApprovalTaskStatus.Cancelled, f.Engine.TaskOf(second).Status);    // nobody is left deciding it
        Assert.Equal(second.Id, f.Stored().ReviewWorkflowInstanceId);                  // and the link moved to it
    }

    [Fact]
    public async Task A_delete_withdraws_the_second_review_round_a_lost_resubmission_left_live_while_the_link_names_the_first()
    {
        var f = new Fixture(TaskLifecycle.PendingReview, review: WorkflowInstanceStatus.Rejected);
        var second = await f.LoseTheResubmissionAsync();

        var deleted = await f.DeleteAsync();

        Assert.Equal(204, deleted.StatusCode);
        Assert.True(f.IsDeleted());
        Assert.Equal(ApprovalTaskStatus.Cancelled, f.Engine.TaskOf(second).Status);
    }

    [Fact]
    public async Task A_cancel_withdraws_a_live_approval_the_link_does_not_name_when_the_link_names_a_closed_one()
    {
        var f = new Fixture(TaskLifecycle.Open, approval: WorkflowInstanceStatus.Cancelled);
        var live = f.Engine.Seed(TaskApprovalService.ApprovalObjectType, TaskApprovalService.BuildObjectRef(f.Task.Id),
            f.Task.Id, WorkflowInstanceStatus.Active, startedAt: DateTimeOffset.UtcNow.AddMinutes(1));

        var cancelled = await f.MoveAsync(TaskLifecycle.Cancelled);

        Assert.Equal(204, cancelled.StatusCode);
        Assert.Equal(ApprovalTaskStatus.Cancelled, f.Engine.TaskOf(live).Status);
        Assert.Equal(live.Id, f.Stored().WorkflowInstanceId);
    }

    // ── item 2 · a review the sweep TIMED OUT is closed without a verdict ─────────────────────────────────────────

    [Fact]
    public async Task The_state_read_reports_a_timed_out_review_as_closed_without_a_verdict_and_the_work_as_sent_back()
    {
        var f = new Fixture(TaskLifecycle.PendingReview, review: WorkflowInstanceStatus.TimedOut);

        var states = await f.Approvals.GetStatesAsync([f.ReviewInstance!.Id], CancellationToken.None);
        var state = states[f.ReviewInstance.Id];

        Assert.True(state.IsTimedOut);
        Assert.False(state.IsApproved);
        Assert.False(state.IsRejected);
        Assert.False(state.IsCancelled);
        // Not outstanding — nobody is reviewing it — and sent back: the holder submits again.
        Assert.Equal((false, true), TaskReviewView.Resolve(f.Stored(), states));
        Assert.True(TaskReviewView.Withdrawn(f.Stored(), states));
    }

    [Fact]
    public async Task The_next_holder_submits_again_after_the_review_timed_out_and_a_new_round_opens()
    {
        // Before D2-FIX4: IsPending read true for a timed-out review, so the submission answered REVIEW_PENDING for ever.
        var f = new Fixture(TaskLifecycle.PendingReview, review: WorkflowInstanceStatus.TimedOut, createdBy: TaskTestData.Other);
        f.AllowDelegation();
        Assert.Equal(204, (await f.HandOverAsync("return")).StatusCode);
        Assert.Equal(204, (await f.AcceptAsAsync(TaskTestData.Other)).StatusCode);
        var starter = new FakeTaskReviewService { InstanceId = Guid.NewGuid() };

        var submitted = await f.SubmitAsAsync(TaskTestData.Other, starter);

        Assert.Equal(204, submitted.StatusCode);
        Assert.Single(starter.Started);
        var stored = f.Stored();
        Assert.Equal(TaskLifecycle.PendingReview, stored.Lifecycle);
        Assert.Equal(starter.InstanceId, stored.ReviewWorkflowInstanceId);
    }

    [Fact]
    public async Task The_holder_resubmits_in_place_when_the_review_timed_out_under_a_task_still_in_review()
    {
        var f = new Fixture(TaskLifecycle.PendingReview, review: WorkflowInstanceStatus.TimedOut);
        var starter = new FakeTaskReviewService { InstanceId = Guid.NewGuid() };

        var submitted = await f.SubmitAsAsync(TaskTestData.Me, starter);

        Assert.Equal(204, submitted.StatusCode);
        Assert.Equal(starter.InstanceId, f.Stored().ReviewWorkflowInstanceId);
    }

    [Theory]
    [InlineData("the engine does not list it")]
    [InlineData("the engine cannot be read")]
    public async Task A_review_whose_state_cannot_be_read_refuses_the_submission_as_transient_not_as_a_permanent_pending(string how)
    {
        var f = new Fixture(TaskLifecycle.InProgress, review: WorkflowInstanceStatus.Active);
        if (how.Contains("list", StringComparison.Ordinal))
        {
            f.Engine.Instances.Items.Clear();
        }
        else
        {
            f.Engine.Instances.ThrowOnList = true;
        }

        var starter = new FakeTaskReviewService { InstanceId = Guid.NewGuid() };

        var refused = await f.SubmitAsAsync(TaskTestData.Me, starter);

        Assert.Equal(503, refused.StatusCode);                                   // a moment's outage, not a verdict
        Assert.Equal(TaskReasonCodes.ReviewStartFailed, refused.ReasonCode);     // the existing "unchanged, try again" code
        Assert.Empty(starter.Started);                                           // and no second round on a guess
        Assert.Equal(f.ReviewInstance!.Id, f.Stored().ReviewWorkflowInstanceId);
    }

    // ── item 4 · the adopted link rides in the SAME conditional write as the flag change ──────────────────────

    [Fact]
    public async Task Switching_review_off_stores_the_adopted_link_in_the_same_write_as_the_flag_change()
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        var live = f.SeedDroppedReview();
        f.Engine.Down = true;

        var refused = await f.EditAsync(approvalRequired: null, expectedVersion: f.Task.Version, reviewRequired: false);

        Assert.Equal(409, refused.StatusCode);
        Assert.Equal(TaskReasonCodes.ReviewCancelFailed, refused.ReasonCode);
        // The edit's OWN write (the first one the store was asked for) already carried the link …
        var edit = f.Repo.WriteAttempts[0];
        Assert.False(edit.ReviewRequired);
        Assert.Equal(live.Id, edit.ReviewWorkflowInstanceId);
        // … so the retry has a link to follow even if every later write were lost.
        Assert.Equal(live.Id, f.Stored().ReviewWorkflowInstanceId);

        f.Engine.Down = false;
        Assert.Equal(204, (await f.EditAsync(approvalRequired: null, expectedVersion: refused.Data!.Version, reviewRequired: false)).StatusCode);
        Assert.Equal(ApprovalTaskStatus.Cancelled, f.Engine.TaskOf(live).Status);
        Assert.Null(f.Stored().ReviewWorkflowInstanceId);
    }

    [Fact]
    public async Task Switching_approval_off_stores_the_adopted_link_in_the_same_write_as_the_flag_change()
    {
        var f = new Fixture(TaskLifecycle.Open);
        var live = f.SeedDroppedApproval();
        f.Engine.Down = true;

        var refused = await f.EditAsync(approvalRequired: false, expectedVersion: f.Task.Version);

        Assert.Equal(409, refused.StatusCode);
        Assert.Equal(TaskReasonCodes.ApprovalCancelFailed, refused.ReasonCode);
        var edit = f.Repo.WriteAttempts[0];
        Assert.False(edit.ApprovalRequired);
        Assert.Equal(live.Id, edit.WorkflowInstanceId);
        Assert.Equal(live.Id, f.Stored().WorkflowInstanceId);
    }

    [Fact]
    public async Task An_edit_whose_write_loses_its_race_touches_nothing_in_the_engine_though_a_live_review_waits()
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        f.SeedDroppedReview();
        f.Repo.ForcedUpdateConflicts = 1;

        var lost = await f.EditAsync(approvalRequired: null, expectedVersion: f.Task.Version, reviewRequired: false);

        Assert.Equal(409, lost.StatusCode);
        Assert.Equal(TaskReasonCodes.ConcurrencyConflict, lost.ReasonCode);
        Assert.Empty(f.Engine.CancelCalls);                      // the lookup is read-only: nothing was withdrawn
        Assert.True(f.Stored().ReviewRequired);                  // and nothing was written
    }

    [Fact]
    public async Task An_edit_that_cannot_look_up_the_running_review_is_refused_and_writes_nothing()
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        f.SeedDroppedReview();
        f.Engine.Instances.ThrowOnLatest = true;

        var refused = await f.EditAsync(approvalRequired: null, expectedVersion: f.Task.Version, reviewRequired: false);

        // The requirement is not switched off on a guess: off with no link, nothing would ever look for it again.
        Assert.Equal(409, refused.StatusCode);
        Assert.Equal(TaskReasonCodes.ConcurrencyConflict, refused.ReasonCode);
        Assert.Empty(f.Repo.WriteAttempts);
        Assert.True(f.Stored().ReviewRequired);
    }

    [Fact]
    public async Task A_review_that_started_after_the_pre_write_lookup_and_whose_link_cannot_be_stored_is_refused_loudly()
    {
        // The narrow race: the lookup before the write found nothing; the review starts; the withdraw after the write finds
        // it, cannot withdraw it, and the write that would store its link loses. The flag is already off — a silent drop
        // here would leave a live review nothing ever looks for again.
        var f = new Fixture(TaskLifecycle.InProgress);
        f.SeedDroppedReviewRequirementOnly();
        f.Engine.Instances.AfterFirstLatest = () => f.Engine.Seed(TaskReviewService.ReviewObjectType,
            TaskReviewService.BuildObjectRef(f.Task.Id), f.Task.Id, WorkflowInstanceStatus.Active);
        f.Engine.Down = true;
        f.Repo.WritesBeforeForcedConflicts = 1;     // the edit's own write lands …
        f.Repo.ForcedUpdateConflicts = 1;           // … the write that would store the late link loses

        var refused = await f.EditAsync(approvalRequired: null, expectedVersion: f.Task.Version, reviewRequired: false);

        Assert.Equal(409, refused.StatusCode);
        Assert.Equal(TaskReasonCodes.ConcurrencyConflict, refused.ReasonCode);   // not "the edit is saved, try again"
    }

    // ── item 7 · a delete is conditional on the version it read; a subtask's cancel is read again and tried again ──

    [Fact]
    public async Task A_delete_whose_task_moved_during_the_withdraw_is_refused_and_a_retry_deletes_it()
    {
        var f = new Fixture(TaskLifecycle.Open, approval: WorkflowInstanceStatus.Active);
        f.Engine.WhileCancelling = f.MoveUnderneath;       // a start (an edit) lands between the withdraw and the delete

        var refused = await f.DeleteAsync();

        Assert.Equal(409, refused.StatusCode);
        Assert.Equal(TaskReasonCodes.ConcurrencyConflict, refused.ReasonCode);
        Assert.False(f.IsDeleted());                       // not deleted around whatever started in between

        f.Engine.WhileCancelling = null;
        Assert.Equal(204, (await f.DeleteAsync()).StatusCode);
        Assert.True(f.IsDeleted());
    }

    [Fact]
    public async Task A_bulk_delete_leaves_out_a_task_that_moved_during_its_withdraw_and_answers_alike()
    {
        var f = new Fixture(TaskLifecycle.Open, approval: WorkflowInstanceStatus.Active);
        var plain = f.AddPlainTask();
        f.Engine.WhileCancelling = f.MoveUnderneath;

        var response = await f.BulkDeleteAsync(f.Task.Id, plain.Id);

        Assert.Equal(204, response.StatusCode);
        Assert.False(f.IsDeleted());
        Assert.True(f.IsDeleted(plain.Id));
    }

    [Fact]
    public async Task A_subtask_whose_cancel_write_loses_its_race_is_read_again_and_cancelled()
    {
        var f = new Fixture(TaskLifecycle.Open);
        var child = f.AddSubtaskWithApproval();
        f.Repo.WritesBeforeForcedConflicts = 1;    // the parent's own cancel lands
        f.Repo.ForcedUpdateConflicts = 1;          // the child's first write loses

        var cancelled = await f.MoveAsync(TaskLifecycle.Cancelled);

        Assert.Equal(204, cancelled.StatusCode);
        Assert.Equal(TaskLifecycle.Cancelled, f.Stored(child.Task.Id).Lifecycle);          // not left open, approval cancelled
        Assert.Equal(ApprovalTaskStatus.Cancelled, f.Engine.TaskOf(child.Instance).Status);
    }

    [Fact]
    public async Task A_subtask_whose_cancel_write_keeps_losing_is_tried_three_times_and_then_left_open()
    {
        var f = new Fixture(TaskLifecycle.Open);
        var child = f.AddSubtaskWithApproval();
        f.Repo.WritesBeforeForcedConflicts = 1;
        f.Repo.ForcedUpdateConflicts = 99;

        var cancelled = await f.MoveAsync(TaskLifecycle.Cancelled);

        Assert.Equal(204, cancelled.StatusCode);                                            // the parent's cancel stands
        Assert.Equal(TaskLifecycle.Open, f.Stored(child.Task.Id).Lifecycle);
        Assert.Equal(3, f.Repo.WriteAttempts.Count(w => w.Id == child.Task.Id));            // bounded: three tries, no more
    }

    // ── item 8 · rules of rounds 2 and 3 that had no test of their own ───────────────────────────────────────────

    [Theory]
    [InlineData(false, false, false)]   // nothing to withdraw: the engine is not asked
    [InlineData(true, false, true)]     // requirement on, no link: a live review may have lost its link (D2-FIX3 item 8)
    [InlineData(false, true, true)]     // requirement already off, link held: a withdraw that failed earlier is retried
    public async Task A_review_switched_off_asks_the_engine_only_when_there_is_something_to_withdraw(
        bool required, bool linked, bool asked)
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        f.SetReviewFacts(required, linked);

        var edit = await f.EditAsync(approvalRequired: null, expectedVersion: f.Task.Version, reviewRequired: false);

        Assert.Equal(204, edit.StatusCode);
        Assert.Equal(asked, f.Engine.Instances.LatestReads > 0);
        Assert.False(f.Stored().ReviewRequired);
        Assert.Null(f.Stored().ReviewWorkflowInstanceId);
    }

    [Fact]
    public async Task Cancelling_a_task_whose_review_link_was_never_stored_withdraws_the_review_found_by_its_reference()
    {
        // TransH:704 — MayHoldReview's second operand: the requirement is on and a withdraw service is there.
        var f = new Fixture(TaskLifecycle.InProgress);
        var dropped = f.SeedDroppedReview();

        var cancelled = await f.MoveAsync(TaskLifecycle.Cancelled);

        Assert.Equal(204, cancelled.StatusCode);
        Assert.Equal(ApprovalTaskStatus.Cancelled, f.Engine.TaskOf(dropped).Status);
    }

    [Fact]
    public async Task A_review_closed_by_the_engine_and_found_by_reference_is_not_adopted_as_the_link_but_an_open_one_is()
    {
        // TRS:295 — `pending.Count > 0`: only a live review becomes the link; a closed one was never this task's.
        var closed = new Fixture(TaskLifecycle.InProgress);
        closed.Engine.Seed(TaskReviewService.ReviewObjectType, TaskReviewService.BuildObjectRef(closed.Task.Id),
            closed.Task.Id, WorkflowInstanceStatus.Cancelled);
        var closedTask = closed.Stored();

        Assert.True(await closed.Reviews.CancelReviewAsync(closedTask, CancellationToken.None));
        Assert.Null(closedTask.ReviewWorkflowInstanceId);

        var open = new Fixture(TaskLifecycle.InProgress);
        var live = open.Engine.Seed(TaskReviewService.ReviewObjectType, TaskReviewService.BuildObjectRef(open.Task.Id),
            open.Task.Id, WorkflowInstanceStatus.Active);
        var openTask = open.Stored();

        Assert.True(await open.Reviews.CancelReviewAsync(openTask, CancellationToken.None));
        Assert.Equal(live.Id, openTask.ReviewWorkflowInstanceId);
    }

    [Fact]
    public async Task An_approval_closed_by_the_engine_and_found_by_reference_is_not_adopted_as_the_link_but_an_open_one_is()
    {
        // TAS:240 — the same rule on the approval side.
        var closed = new Fixture(TaskLifecycle.Open);
        closed.Engine.Seed(TaskApprovalService.ApprovalObjectType, TaskApprovalService.BuildObjectRef(closed.Task.Id),
            closed.Task.Id, WorkflowInstanceStatus.Cancelled);
        var closedTask = closed.Stored();

        Assert.True(await closed.Approvals.CancelApprovalAsync(closedTask, CancellationToken.None));
        Assert.Null(closedTask.WorkflowInstanceId);

        var open = new Fixture(TaskLifecycle.Open);
        var live = open.Engine.Seed(TaskApprovalService.ApprovalObjectType, TaskApprovalService.BuildObjectRef(open.Task.Id),
            open.Task.Id, WorkflowInstanceStatus.Active);
        var openTask = open.Stored();

        Assert.True(await open.Approvals.CancelApprovalAsync(openTask, CancellationToken.None));
        Assert.Equal(live.Id, openTask.WorkflowInstanceId);
    }

    // ── fixture, round 4 ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A review starter that opens ROUND TWO in the engine (it is live there) — the half of a resubmission that lands before
    /// its task write loses its race.
    /// </summary>
    private sealed class RoundTwoStarter(Engine engine, Guid taskId) : ITaskReviewService
    {
        public List<WorkflowInstance> Opened { get; } = [];

        public Task<Guid?> TryStartReviewAsync(TaskItem task, CancellationToken ct)
        {
            var opened = engine.Seed(TaskReviewService.ReviewObjectType, TaskReviewService.BuildObjectRef(taskId), taskId,
                WorkflowInstanceStatus.Active, startedAt: DateTimeOffset.UtcNow.AddMinutes(1));
            Opened.Add(opened);
            return System.Threading.Tasks.Task.FromResult<Guid?>(opened.Id);
        }

        public async Task<TaskReviewStartResult> StartReviewAsync(TaskItem task, CancellationToken ct) =>
            new(await TryStartReviewAsync(task, ct), null);

        public Task<bool> CancelReviewAsync(TaskItem task, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed partial class Fixture
    {
        public FakeTaskItemRepository Repo => _tasks;

        /// <summary>A resubmission after a refusal whose task write loses its race: round two is open in MOD-0023, the task's
        /// link still names round one.</summary>
        public async Task<WorkflowInstance> LoseTheResubmissionAsync()
        {
            var starter = new RoundTwoStarter(Engine, Task.Id);
            _tasks.ForcedUpdateConflicts = 1;

            var lost = await SubmitAsAsync(TaskTestData.Me, starter);

            Assert.Equal(409, lost.StatusCode);
            Assert.Equal(TaskReasonCodes.ConcurrencyConflict, lost.ReasonCode);
            return Assert.Single(starter.Opened);
        }

        /// <summary>Somebody else writes the task while a withdraw is under way: its version moves.</summary>
        public void MoveUnderneath() => Raw().Version++;

        /// <summary>The requirement is on and the task has no review link, and NOTHING is running in the engine.</summary>
        public void SeedDroppedReviewRequirementOnly()
        {
            var stored = Raw();
            stored.ReviewRequired = true;
            stored.ReviewerCandidateUserId = TaskTestData.Rival;
            stored.ReviewWorkflowInstanceId = null;
        }

        /// <summary>The review facts a task carries: its requirement flag, and a link (to an instance the engine does not list).</summary>
        public void SetReviewFacts(bool required, bool linked)
        {
            var stored = Raw();
            stored.ReviewRequired = required;
            stored.ReviewerCandidateUserId = required ? TaskTestData.Rival : null;
            stored.ReviewWorkflowInstanceId = linked ? Guid.NewGuid() : null;
        }
    }
}
