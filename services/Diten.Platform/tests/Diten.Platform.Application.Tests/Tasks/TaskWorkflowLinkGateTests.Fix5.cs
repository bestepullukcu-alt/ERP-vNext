using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Enums.Workflow;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WP-WF-ENGINE-CANDIDATES-01 · D2 fix round 5 (CT-WF-D2-FIX5) — the last paths on which MOD-0023 holds a live approval or
/// review that nothing in MOD-0024 reads: a start that lost its task write to a rival writer who no longer wants it (item 1),
/// an instance adopted although it never got a task (item 2), an approval started again under a key that replays a closed
/// round (item 3), a bulk delete that skipped without saying so (item 4), and the rules of rounds 3 and 4 that had no test of
/// their own (item 6). Same real chain as the earlier rounds: the real handlers, the real withdraw and start services, the real
/// gate over in-memory MOD-0023 — this time with the engine's START command answered too, so the real start is what runs.
/// </summary>
public sealed partial class TaskWorkflowLinkGateTests
{
    private const string RequirementOff = "the requirement was switched off";
    private const string TaskCancelled = "the task was cancelled";
    private const string TaskDeleted = "the task was deleted";

    private static string FirstApprovalKey(Guid taskId) => $"task-approval:{TaskTestData.Tenant}:{taskId}";

    // ── item 1 · a start that loses its task write withdraws what it started when nobody wants it ──────────────────

    [Theory]
    [InlineData(RequirementOff)]
    [InlineData(TaskCancelled)]
    [InlineData(TaskDeleted)]
    public async Task A_submission_that_loses_its_race_withdraws_the_review_it_just_started_when_nobody_wants_it_any_more(string rival)
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        f.SeedDroppedReviewRequirementOnly();
        // The barrier: the rival's write lands AFTER the engine opened the round and BEFORE the submission's own task write.
        f.Engine.AfterStart = () => f.RivalWrites(rival, review: true);

        var lost = await f.SubmitAsAsync(TaskTestData.Me, f.Reviews);

        Assert.Equal(409, lost.StatusCode);
        Assert.Equal(TaskReasonCodes.ConcurrencyConflict, lost.ReasonCode);
        var round = Assert.Single(f.Engine.Started);                                    // non-vacuity: the engine DID open it …
        Assert.Equal(ApprovalTaskStatus.Cancelled, f.Engine.TaskOf(round).Status);      // … and nobody is left deciding it
        Assert.Equal(WorkflowInstanceStatus.Cancelled, round.Status);
        Assert.Single(f.Engine.CancelCalls);
    }

    [Fact]
    public async Task A_submission_that_loses_its_race_to_an_unrelated_write_keeps_its_round_and_the_retry_adopts_it()
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        f.SeedDroppedReviewRequirementOnly();
        f.Engine.AfterStart = f.BumpVersion;          // somebody saved the title: the requirement and the task are as they were

        var lost = await f.SubmitAsAsync(TaskTestData.Me, f.Reviews);

        Assert.Equal(409, lost.StatusCode);
        var round = Assert.Single(f.Engine.Started);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, f.Engine.TaskOf(round).Status);   // wanted: it is NOT withdrawn
        Assert.Empty(f.Engine.CancelCalls);

        f.Engine.AfterStart = null;
        var retry = await f.SubmitAsAsync(TaskTestData.Me, f.Reviews);

        Assert.Equal(204, retry.StatusCode);
        Assert.Single(f.Engine.Started);                                                   // adopted, not started a second time
        var stored = f.Stored();
        Assert.Equal(round.Id, stored.ReviewWorkflowInstanceId);
        Assert.Equal(TaskLifecycle.PendingReview, stored.Lifecycle);
    }

    [Fact]
    public async Task A_withdraw_that_fails_after_a_lost_submission_stores_the_link_so_a_later_edit_retries_it()
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        f.SeedDroppedReviewRequirementOnly();
        var log = new CapturingLogger<SubmitTaskForReviewHandler>();
        f.Engine.Down = true;                                                     // MOD-0023 refuses the withdraw (the start is not asked)
        f.Engine.AfterStart = () => f.RivalWrites(RequirementOff, review: true);

        var lost = await f.SubmitAsAsync(TaskTestData.Me, f.Reviews, log);

        Assert.Equal(409, lost.StatusCode);
        var round = Assert.Single(f.Engine.Started);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, f.Engine.TaskOf(round).Status);   // still decidable …
        var stored = f.Stored();
        Assert.False(stored.ReviewRequired);
        Assert.Equal(round.Id, stored.ReviewWorkflowInstanceId);                           // … but the task now names it
        Assert.Contains(log.Entries, e => e.Level >= LogLevel.Warning && e.Message.Contains(round.Id.ToString(), StringComparison.Ordinal));

        // The next edit sees a held link while the requirement is off and withdraws it (D2-FIX1 A1).
        f.Engine.Down = false;
        var retried = await f.EditAsync(approvalRequired: null, expectedVersion: stored.Version, reviewRequired: false);

        Assert.Equal(204, retried.StatusCode);
        Assert.Equal(ApprovalTaskStatus.Cancelled, f.Engine.TaskOf(round).Status);
        Assert.Null(f.Stored().ReviewWorkflowInstanceId);
    }

    [Fact]
    public async Task A_lost_submission_whose_withdraw_and_link_write_both_fail_tries_the_link_three_times_and_says_so()
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        f.SeedDroppedReviewRequirementOnly();
        var log = new CapturingLogger<SubmitTaskForReviewHandler>();
        f.Engine.Down = true;
        f.Engine.AfterStart = () => f.RivalWrites(RequirementOff, review: true);
        f.Repo.ForcedUpdateConflicts = 99;                                        // every task write loses from here on

        var lost = await f.SubmitAsAsync(TaskTestData.Me, f.Reviews, log);

        Assert.Equal(409, lost.StatusCode);
        var round = Assert.Single(f.Engine.Started);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, f.Engine.TaskOf(round).Status);
        Assert.Equal(1 + 3, f.Repo.WriteAttempts.Count);                          // the submission's own write, then three tries at the link
        Assert.Null(f.Stored().ReviewWorkflowInstanceId);
        Assert.Contains(log.Entries, e => e.Level == LogLevel.Error && e.Message.Contains(round.Id.ToString(), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(RequirementOff)]
    [InlineData(TaskCancelled)]
    [InlineData(TaskDeleted)]
    public async Task An_edit_switching_approval_on_withdraws_the_approval_it_just_started_when_nobody_wants_it_any_more(string rival)
    {
        var f = new Fixture(TaskLifecycle.Open);
        f.SeedApprovalOff();
        f.Engine.AfterStart = () => f.RivalWrites(rival, review: false);

        var edit = await f.EditAsync(approvalRequired: true, expectedVersion: f.Task.Version);

        Assert.Equal(204, edit.StatusCode);                                                // the edit itself landed; only the link write lost
        var round = Assert.Single(f.Engine.Started);
        Assert.Equal(ApprovalTaskStatus.Cancelled, f.Engine.TaskOf(round).Status);
        Assert.Equal(WorkflowInstanceStatus.Cancelled, round.Status);
    }

    [Fact]
    public async Task An_edit_whose_approval_start_loses_its_link_write_to_an_unrelated_write_keeps_the_approval()
    {
        var f = new Fixture(TaskLifecycle.Open);
        f.SeedApprovalOff();
        f.Engine.AfterStart = f.BumpVersion;

        var edit = await f.EditAsync(approvalRequired: true, expectedVersion: f.Task.Version);

        Assert.Equal(204, edit.StatusCode);
        var round = Assert.Single(f.Engine.Started);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, f.Engine.TaskOf(round).Status);   // the requirement is still on: not withdrawn
        Assert.Empty(f.Engine.CancelCalls);
        Assert.True(f.Stored().ApprovalRequired);
    }

    [Fact]
    public async Task A_withdraw_that_fails_after_a_lost_approval_link_stores_the_link_so_a_later_edit_retries_it()
    {
        var f = new Fixture(TaskLifecycle.Open);
        f.SeedApprovalOff();
        f.Engine.Down = true;
        f.Engine.AfterStart = () => f.RivalWrites(RequirementOff, review: false);

        var edit = await f.EditAsync(approvalRequired: true, expectedVersion: f.Task.Version);

        Assert.Equal(204, edit.StatusCode);
        var round = Assert.Single(f.Engine.Started);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, f.Engine.TaskOf(round).Status);
        var stored = f.Stored();
        Assert.False(stored.ApprovalRequired);
        Assert.Equal(round.Id, stored.WorkflowInstanceId);                                 // held link, requirement off: the retry's signal

        f.Engine.Down = false;
        var retried = await f.EditAsync(approvalRequired: false, expectedVersion: stored.Version);

        Assert.Equal(204, retried.StatusCode);
        Assert.Equal(ApprovalTaskStatus.Cancelled, f.Engine.TaskOf(round).Status);
        Assert.Null(f.Stored().WorkflowInstanceId);
    }

    // ── item 2 · a live review is adopted only when the engine holds an open task for it ─────────────────────────

    [Fact]
    public async Task A_review_start_adopts_a_live_round_that_has_an_open_task_and_starts_nothing()
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        var live = f.SeedDroppedReview();

        var started = await f.Reviews.StartReviewAsync(f.Stored(), CancellationToken.None);

        Assert.Equal(live.Id, started.InstanceId);
        Assert.Empty(f.Engine.StartCalls);
    }

    [Fact]
    public async Task A_review_start_does_not_adopt_a_live_instance_that_never_got_its_task_and_the_submission_is_refused_until_it_has_one()
    {
        // The generic start writes instance, then task, then snapshot: a crash after the first leaves an Active instance nobody
        // will ever review. Adopting it moved the task to PendingReview behind it, for good.
        var f = new Fixture(TaskLifecycle.InProgress);
        var half = f.SeedDroppedReview();
        f.Engine.Tasks.Items.RemoveAll(t => t.WorkflowInstanceId == half.Id);

        var started = await f.Reviews.StartReviewAsync(f.Stored(), CancellationToken.None);

        Assert.Null(started.InstanceId);
        Assert.Equal(WorkflowReasonCodes.WorkflowInstanceStartConflict, started.RefusalReasonCode);
        Assert.Empty(f.Engine.StartCalls);                                                  // and no second instance beside it

        var refused = await f.SubmitAsAsync(TaskTestData.Me, f.Reviews);
        Assert.Equal(409, refused.StatusCode);
        Assert.Equal(TaskReasonCodes.ReviewStartFailed, refused.ReasonCode);
        var untouched = f.Stored();
        Assert.Equal(TaskLifecycle.InProgress, untouched.Lifecycle);
        Assert.Null(untouched.ReviewWorkflowInstanceId);

        // Once the engine holds a task for it (its start completed), the same submission adopts it.
        f.Engine.Tasks.Items.Add(new ApprovalTask
        {
            TenantId = TaskTestData.Tenant, WorkflowInstanceId = half.Id, Status = ApprovalTaskStatus.WaitingApproval
        });
        Assert.Equal(204, (await f.SubmitAsAsync(TaskTestData.Me, f.Reviews)).StatusCode);
        Assert.Equal(half.Id, f.Stored().ReviewWorkflowInstanceId);
    }

    // ── item 3 · the approval key comes from the latest CLOSED instance; the first approval's key never changes ───────

    [Fact]
    public async Task The_first_approval_is_started_under_the_key_it_always_had()
    {
        var f = new Fixture(TaskLifecycle.Open);
        f.SeedApprovalOff();

        var instanceId = await f.Approvals.TryStartApprovalAsync(f.Stored(), CancellationToken.None);

        var call = Assert.Single(f.Engine.StartCalls);
        Assert.Equal(FirstApprovalKey(f.Task.Id), call.Request.IdempotencyKey);             // no suffix: instances already stored stay reachable
        Assert.Equal(f.Engine.Started.Single().Id, instanceId);
    }

    [Theory]
    [InlineData(WorkflowInstanceStatus.Cancelled)]
    [InlineData(WorkflowInstanceStatus.TimedOut)]
    [InlineData(WorkflowInstanceStatus.Completed)]
    public async Task Switching_approval_off_and_on_again_opens_a_new_round_under_a_key_from_the_closed_one_and_the_first_key_stays(
        WorkflowInstanceStatus closed)
    {
        var f = new Fixture(TaskLifecycle.Open, approval: closed);
        var first = f.ApprovalInstance!;
        first.IdempotencyKey = FirstApprovalKey(f.Task.Id);                               // how the first approval was stored

        Assert.Equal(204, (await f.EditAsync(approvalRequired: false, expectedVersion: f.Stored().Version)).StatusCode);
        Assert.Null(f.Stored().WorkflowInstanceId);
        Assert.Equal(204, (await f.EditAsync(approvalRequired: true, expectedVersion: f.Stored().Version)).StatusCode);

        // Before: the key replayed the closed first round, and the task was "successfully" linked to it for ever.
        var call = Assert.Single(f.Engine.StartCalls);
        Assert.Equal($"{FirstApprovalKey(f.Task.Id)}:{first.Id}", call.Request.IdempotencyKey);
        var round = Assert.Single(f.Engine.Started);
        Assert.NotEqual(first.Id, round.Id);
        Assert.Equal(round.Id, f.Stored().WorkflowInstanceId);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, f.Engine.TaskOf(round).Status);
        Assert.Equal(FirstApprovalKey(f.Task.Id), first.IdempotencyKey);                  // the stored instance is not re-keyed
        Assert.Equal(closed, first.Status);
        var states = await f.Approvals.GetStatesAsync([round.Id], CancellationToken.None);
        Assert.Equal((true, false), TaskApprovalView.Resolve(f.Stored(), states));       // waiting for a live decision, not stuck
    }

    [Fact]
    public async Task An_approval_start_that_answers_with_a_closed_instance_is_not_linked()
    {
        var f = new Fixture(TaskLifecycle.Open);
        f.SeedApprovalOff();
        // A closed instance that the object-reference lookup does not list but the key replays.
        f.Engine.Seed(TaskApprovalService.ApprovalObjectType, "elsewhere", f.Task.Id, WorkflowInstanceStatus.Cancelled,
            idempotencyKey: FirstApprovalKey(f.Task.Id));

        var instanceId = await f.Approvals.TryStartApprovalAsync(f.Stored(), CancellationToken.None);

        Assert.Single(f.Engine.StartCalls);                                                // asked, and answered with the dead round
        Assert.Null(instanceId);
    }

    [Fact]
    public async Task An_approval_start_adopts_a_live_instance_that_has_an_open_task_and_refuses_one_that_never_got_it()
    {
        var f = new Fixture(TaskLifecycle.Open);
        var live = f.SeedDroppedApproval();

        Assert.Equal(live.Id, await f.Approvals.TryStartApprovalAsync(f.Stored(), CancellationToken.None));
        Assert.Empty(f.Engine.StartCalls);

        f.Engine.Tasks.Items.RemoveAll(t => t.WorkflowInstanceId == live.Id);
        Assert.Null(await f.Approvals.TryStartApprovalAsync(f.Stored(), CancellationToken.None));
        Assert.Empty(f.Engine.StartCalls);
    }

    // ── item 4 · a bulk delete retries a task that moved, and says which ids it left out ─────────────────────────

    [Fact]
    public async Task A_bulk_delete_whose_task_moved_is_read_again_and_deleted_and_nothing_is_reported_skipped()
    {
        var f = new Fixture(TaskLifecycle.Open, approval: WorkflowInstanceStatus.Active);
        f.Engine.WhileCancelling = f.MoveUnderneath;       // an edit lands between the withdraw and the delete

        var response = await f.BulkDeleteAsync(f.Task.Id);

        Assert.Equal(204, response.StatusCode);
        Assert.Null(response.Data);
        Assert.True(f.IsDeleted());
        Assert.Equal(ApprovalTaskStatus.Cancelled, f.Engine.TaskOf(f.ApprovalInstance!).Status);
    }

    [Fact]
    public async Task A_bulk_delete_that_keeps_losing_tries_three_times_and_names_the_skipped_ids_in_a_200()
    {
        var f = new Fixture(TaskLifecycle.Open);
        var stubborn = f.AddPlainTask();
        var plain = f.AddPlainTask();
        f.Repo.DeleteConflicts[stubborn.Id] = 99;

        var response = await f.BulkDeleteAsync(stubborn.Id, plain.Id, Guid.NewGuid());

        Assert.Equal(200, response.StatusCode);
        // Only a task of THIS tenant that could not be deleted is named; an unknown id is answered like a delete (no leak).
        Assert.Equal([stubborn.Id], response.Data!.SkippedIds);
        Assert.False(f.IsDeleted(stubborn.Id));
        Assert.True(f.IsDeleted(plain.Id));
        Assert.Equal(3, f.Repo.DeleteAttempts.Count(id => id == stubborn.Id));             // bounded: three reads, three tries
    }

    // ── item 6 · rules of rounds 3 and 4 that had no test of their own ───────────────────────────────────────────

    [Theory]
    [InlineData("review")]
    [InlineData("approval")]
    public async Task A_withdraw_that_cannot_reach_the_second_round_after_the_first_was_cancelled_is_refused_and_keeps_the_link(string kind)
    {
        var review = kind == "review";
        var f = review
            ? new Fixture(TaskLifecycle.PendingReview, review: WorkflowInstanceStatus.Active)
            : new Fixture(TaskLifecycle.Open, approval: WorkflowInstanceStatus.Active);
        var first = review ? f.ReviewInstance! : f.ApprovalInstance!;
        var second = review
            ? f.Engine.Seed(TaskReviewService.ReviewObjectType, TaskReviewService.BuildObjectRef(f.Task.Id), f.Task.Id,
                WorkflowInstanceStatus.Active, startedAt: DateTimeOffset.UtcNow.AddMinutes(5))
            : f.Engine.Seed(TaskApprovalService.ApprovalObjectType, TaskApprovalService.BuildObjectRef(f.Task.Id), f.Task.Id,
                WorkflowInstanceStatus.Active, startedAt: DateTimeOffset.UtcNow.AddMinutes(5));
        f.Engine.FailFor.Add(second.Id);                    // the engine refuses the SECOND round, after the first went through

        var refused = await f.MoveAsync(TaskLifecycle.Cancelled);

        Assert.Equal(409, refused.StatusCode);
        Assert.Equal(review ? TaskReasonCodes.ReviewCancelFailed : TaskReasonCodes.ApprovalCancelFailed, refused.ReasonCode);
        Assert.Equal(ApprovalTaskStatus.Cancelled, f.Engine.TaskOf(first).Status);          // the first round is already withdrawn …
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, f.Engine.TaskOf(second).Status);   // … the second is not, and the task is not closed
        var stored = f.Stored();
        Assert.Null(stored.CancelledAt);
        Assert.Equal(first.Id, review ? stored.ReviewWorkflowInstanceId : stored.WorkflowInstanceId);   // the link was kept

        f.Engine.FailFor.Clear();
        Assert.Equal(204, (await f.MoveAsync(TaskLifecycle.Cancelled)).StatusCode);
        Assert.Equal(ApprovalTaskStatus.Cancelled, f.Engine.TaskOf(second).Status);
    }

    [Theory]
    [InlineData("return")]
    [InlineData("reassign")]
    [InlineData("release")]
    public async Task A_hand_over_withdraws_the_live_second_round_a_stale_link_does_not_name(string handover)
    {
        var f = new Fixture(TaskLifecycle.PendingReview, review: WorkflowInstanceStatus.Rejected, createdBy: TaskTestData.Other);
        if (handover == "release")
        {
            f.MakePooled();
        }

        f.AllowDelegation();
        var second = await f.LoseTheResubmissionAsync();
        Assert.Equal(f.ReviewInstance!.Id, f.Stored().ReviewWorkflowInstanceId);            // non-vacuity: the link is STALE

        var handed = await f.HandOverAsync(handover);

        Assert.Equal(204, handed.StatusCode);
        Assert.Equal(ApprovalTaskStatus.Cancelled, f.Engine.TaskOf(second).Status);         // the next holder does not inherit round two
        var stored = f.Stored();
        Assert.Equal(TaskLifecycle.Open, stored.Lifecycle);
        Assert.Equal(second.Id, stored.ReviewWorkflowInstanceId);                           // the link moved to the withdrawn round
    }

    [Fact]
    public async Task A_resubmission_adopts_the_live_second_round_a_lost_resubmission_left_and_opens_no_third()
    {
        var f = new Fixture(TaskLifecycle.PendingReview, review: WorkflowInstanceStatus.Rejected);
        var second = await f.LoseTheResubmissionAsync();
        Assert.Equal(f.ReviewInstance!.Id, f.Stored().ReviewWorkflowInstanceId);

        // The real service this time: the handler reads the stale link (round one, refused), the service finds round two live.
        var resubmitted = await f.SubmitAsAsync(TaskTestData.Me, f.Reviews);

        Assert.Equal(204, resubmitted.StatusCode);
        Assert.Empty(f.Engine.StartCalls);                                                  // no third round
        var stored = f.Stored();
        Assert.Equal(second.Id, stored.ReviewWorkflowInstanceId);
        Assert.Equal(TaskLifecycle.PendingReview, stored.Lifecycle);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, f.Engine.TaskOf(second).Status);   // the live round is the one under review
    }

    [Fact]
    public async Task An_approval_that_started_after_the_pre_write_lookup_and_whose_link_cannot_be_stored_is_refused_loudly()
    {
        // The approval twin of the review case (WriteH:438-447): the lookup before the write found nothing; the approval
        // starts; the withdraw after the write finds it, cannot withdraw it, and the write that would store its link loses.
        var f = new Fixture(TaskLifecycle.Open);
        f.SeedDroppedApprovalRequirementOnly();
        f.Engine.Instances.AfterFirstLatest = () => f.Engine.Seed(TaskApprovalService.ApprovalObjectType,
            TaskApprovalService.BuildObjectRef(f.Task.Id), f.Task.Id, WorkflowInstanceStatus.Active);
        f.Engine.Down = true;
        f.Repo.WritesBeforeForcedConflicts = 1;             // the edit's own write lands …
        f.Repo.ForcedUpdateConflicts = 1;                   // … the write that would store the late link loses

        var refused = await f.EditAsync(approvalRequired: false, expectedVersion: f.Task.Version);

        Assert.Equal(409, refused.StatusCode);
        Assert.Equal(TaskReasonCodes.ConcurrencyConflict, refused.ReasonCode);               // not "the edit is saved, try again"
        Assert.Equal(2, f.Repo.WriteAttempts.Count);
    }

    // ── fixture, round 5 ──────────────────────────────────────────────────────────────────────────────────────────

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }

    private sealed partial class Fixture
    {
        /// <summary>The task wants no approval yet, but has an approval manager (so an edit can switch it on); nothing runs.</summary>
        public void SeedApprovalOff()
        {
            var stored = Raw();
            stored.ApprovalRequired = false;
            stored.ApprovalManagerUserId = TaskTestData.Rival;
            stored.WorkflowInstanceId = null;
        }

        /// <summary>The approval requirement is on, with no link and NOTHING running in the engine.</summary>
        public void SeedDroppedApprovalRequirementOnly()
        {
            var stored = Raw();
            stored.ApprovalRequired = true;
            stored.ApprovalManagerUserId = TaskTestData.Rival;
            stored.WorkflowInstanceId = null;
        }

        /// <summary>Somebody saves the task: its version moves and nothing the handlers read changes.</summary>
        public void BumpVersion() => Raw().Version++;

        /// <summary>
        /// A rival writer's save, landing between the engine's start and the caller's own task write: it switched the requirement
        /// off, closed the task, or deleted it — each bumps the version, so the caller's conditional write loses.
        /// </summary>
        public void RivalWrites(string rival, bool review)
        {
            var stored = Raw();
            switch (rival)
            {
                case RequirementOff when review:
                    stored.ReviewRequired = false;
                    stored.ReviewerCandidateUserId = null;
                    break;
                case RequirementOff:
                    stored.ApprovalRequired = false;
                    break;
                case TaskCancelled:
                    stored.Lifecycle = TaskLifecycle.Cancelled;
                    stored.CancelledAt = DateTimeOffset.UtcNow;
                    break;
                case TaskDeleted:
                    stored.IsDeleted = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(rival));
            }

            stored.Version++;
        }
    }
}
