using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// Faz 3b — review is MOD-0023's SECOND decision, asked through the SAME engine.
///
/// <para>The charter's line (Binding A) is that MOD-0024 owns no decision engine. Approval already honours it;
/// review had to be added without quietly growing a small one beside it. These tests hold that from both
/// directions: the review gate must actually refuse a write server-side, and the answer must come from the
/// INSTANCE rather than from any field MOD-0024 keeps for itself.</para>
///
/// <para>The heaviest test here is the approval REGRESSION group. Approval and review are two live decisions on
/// one task, and the gate resolves an instance by "latest for this object reference" — so the failure mode this
/// slice risked was not a missing feature but a silently corrupted approval: each gate reading whichever decision
/// happened to start last.</para>
/// </summary>
public sealed class TaskReviewGateTests
{
    private static readonly Guid ReviewInstance = Guid.Parse("beefbeef-beef-beef-beef-beefbeefbeef");
    private static readonly Guid ApprovalInstance = Guid.Parse("abcdabcd-abcd-abcd-abcd-abcdabcdabcd");

    // ── submitReview: the transition, and the instance behind it ─────────────

    [Fact]
    public async Task Submitting_for_review_moves_the_task_and_opens_a_MOD0023_instance()
    {
        var task = ReviewTask(TaskLifecycle.InProgress);
        var tasks = new FakeTaskItemRepository(task);
        var reviews = new FakeTaskReviewService();

        var result = await Submit(tasks, reviews, task.Id, task.Version);

        Assert.True(result.IsSuccessful);
        var stored = tasks.Items.Single();
        Assert.Equal(TaskLifecycle.PendingReview, stored.Lifecycle);
        // The LINK, which is all MOD-0024 keeps. There is deliberately no review-status field beside it.
        Assert.Equal(ReviewInstance, stored.ReviewWorkflowInstanceId);
        Assert.Equal([task.Id], reviews.Started);
    }

    [Fact]
    public async Task A_task_that_never_asked_for_a_review_cannot_be_submitted_for_one()
    {
        /*
         * The projection simply does not offer the action — but a caller can post straight to the endpoint, and a
         * hidden control is presentation while the refusal is the rule. Without this, any task could be parked in
         * PendingReview behind a review nobody asked for, and `complete` would then be gated by it.
         */
        var task = ReviewTask(TaskLifecycle.InProgress);
        task.ReviewRequired = false;
        var tasks = new FakeTaskItemRepository(task);
        var reviews = new FakeTaskReviewService();

        var result = await Submit(tasks, reviews, task.Id, task.Version);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(TaskReasonCodes.ReviewNotRequired, result.ReasonCode);
        // Nothing committed, and MOD-0023 was never asked.
        Assert.Equal(TaskLifecycle.InProgress, tasks.Items.Single().Lifecycle);
        Assert.Empty(reviews.Started);
    }

    [Fact]
    public async Task A_review_that_cannot_be_started_leaves_the_task_where_it_was()
    {
        /*
         * A task sitting in PendingReview with no instance would be waiting on a reviewer who was never asked, and
         * `complete` would then refuse forever with nothing the holder could press to clear it. So the handoff
         * happens BEFORE the lifecycle moves, and a failed handoff moves nothing.
         */
        var task = ReviewTask(TaskLifecycle.InProgress);
        var tasks = new FakeTaskItemRepository(task);
        var reviews = new FakeTaskReviewService { CannotStart = true };

        var result = await Submit(tasks, reviews, task.Id, task.Version);

        Assert.False(result.IsSuccessful);
        var stored = tasks.Items.Single();
        Assert.Equal(TaskLifecycle.InProgress, stored.Lifecycle);
        Assert.Null(stored.ReviewWorkflowInstanceId);
    }

    // ── complete: gated by the review, and by the RIGHT gate ─────────────────

    [Fact]
    public async Task Completion_is_refused_while_the_reviewer_still_holds_the_work()
    {
        var task = ReviewTask(TaskLifecycle.PendingReview);
        task.ReviewWorkflowInstanceId = ReviewInstance;
        var tasks = new FakeTaskItemRepository(task);
        var gate = new FakeWorkflowTransitionGate();
        gate.BlockedObjectTypes.Add(TaskReviewService.ReviewObjectType);

        var result = await Complete(tasks, gate, task.Id, task.Version);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        // Nothing committed: the refusal is the rule, not a hint.
        Assert.Equal(TaskLifecycle.PendingReview, tasks.Items.Single().Lifecycle);
        // The gate was asked about the REVIEW's own object reference, never the approval's.
        var call = Assert.Single(gate.Calls);
        Assert.Equal(TaskReviewService.ReviewObjectType, call.ObjectType);
        Assert.Equal(TaskReviewService.BuildObjectRef(task.Id), call.ObjectRef);
    }

    [Fact]
    public async Task Review_required_but_never_submitted_still_blocks_completion()
    {
        /*
         * The hole this closes is specific and easy to miss: with no instance the gate answers "no workflow",
         * which the gate CONTRACT treats as allowed. So a review-gated task that was never submitted would have
         * completed straight through the gate, and the requirement would have been decorative.
         */
        var task = ReviewTask(TaskLifecycle.InProgress);
        var tasks = new FakeTaskItemRepository(task);
        // A gate that allows EVERYTHING — so if this test passes, the refusal cannot have come from the gate.
        var gate = new FakeWorkflowTransitionGate { Blocked = false };

        var result = await Complete(tasks, gate, task.Id, task.Version);

        Assert.False(result.IsSuccessful);
        Assert.Equal(TaskReasonCodes.ReviewPending, result.ReasonCode);
        Assert.Equal(TaskLifecycle.InProgress, tasks.Items.Single().Lifecycle);
    }

    [Fact]
    public async Task Once_the_review_is_released_the_task_completes()
    {
        var task = ReviewTask(TaskLifecycle.PendingReview);
        task.ReviewWorkflowInstanceId = ReviewInstance;
        var tasks = new FakeTaskItemRepository(task);
        var gate = new FakeWorkflowTransitionGate { Blocked = false };

        var result = await Complete(tasks, gate, task.Id, task.Version);

        Assert.True(result.IsSuccessful);
        Assert.Equal(TaskLifecycle.Done, tasks.Items.Single().Lifecycle);
    }

    [Fact]
    public async Task A_review_gate_that_cannot_be_evaluated_BLOCKS_completion()
    {
        // Fail-closed, exactly as approval is: a workflow outage must never become a review bypass.
        var task = ReviewTask(TaskLifecycle.PendingReview);
        task.ReviewWorkflowInstanceId = ReviewInstance;
        var tasks = new FakeTaskItemRepository(task);
        var gate = new FakeWorkflowTransitionGate { Throws = true };

        var result = await Complete(tasks, gate, task.Id, task.Version);

        Assert.False(result.IsSuccessful);
        Assert.Equal(TaskLifecycle.PendingReview, tasks.Items.Single().Lifecycle);
    }

    // ── The approval REGRESSION: the most important group in this slice ──────

    [Fact]
    public async Task Approval_alone_still_asks_exactly_the_approval_gate_and_nothing_else()
    {
        /*
         * The shape of the defect this slice could have introduced: a second gate call, or a call carrying the
         * wrong object reference, would make an approval-only task consult a review that does not exist — and the
         * gate answers "no workflow → allowed" for a missing object, so the failure would have been SILENT.
         */
        var task = ReviewTask(TaskLifecycle.InProgress);
        task.ReviewRequired = false;
        task.ApprovalRequired = true;
        task.WorkflowInstanceId = ApprovalInstance;
        var tasks = new FakeTaskItemRepository(task);
        var gate = new FakeWorkflowTransitionGate { Blocked = false };

        var result = await Complete(tasks, gate, task.Id, task.Version);

        Assert.True(result.IsSuccessful);
        var call = Assert.Single(gate.Calls);
        Assert.Equal(TaskApprovalService.ApprovalObjectType, call.ObjectType);
        Assert.Equal(TaskApprovalService.BuildObjectRef(task.Id), call.ObjectRef);
    }

    [Fact]
    public async Task A_blocked_approval_still_blocks_start_with_the_approval_reason()
    {
        // The pre-existing behaviour, restated so this slice cannot quietly change it: approval gates START, and
        // review — which gates COMPLETE — must not appear anywhere in that path.
        var task = ReviewTask(TaskLifecycle.Open);
        task.ApprovalRequired = true;
        task.WorkflowInstanceId = ApprovalInstance;
        var tasks = new FakeTaskItemRepository(task);
        var gate = new FakeWorkflowTransitionGate();
        gate.BlockedObjectTypes.Add(TaskApprovalService.ApprovalObjectType);

        var result = await Transition(tasks, gate, task.Id, TaskLifecycle.InProgress, task.Version);

        Assert.False(result.IsSuccessful);
        Assert.Equal(TaskLifecycle.Open, tasks.Items.Single().Lifecycle);
        var call = Assert.Single(gate.Calls);
        Assert.Equal(TaskApprovalService.ApprovalObjectType, call.ObjectType);
        Assert.Equal("start", call.RequestedTransition);
    }

    [Fact]
    public async Task An_open_review_does_not_block_a_task_whose_APPROVAL_is_the_one_that_was_released()
    {
        /*
         * The collision, stated as behaviour rather than as a repository property. Both decisions are live; the
         * REVIEW is blocked and the APPROVAL is not. If the two shared an object reference the approval gate would
         * read the review's block and refuse `start` — an approved task that cannot begin.
         */
        var task = ReviewTask(TaskLifecycle.Open);
        task.ApprovalRequired = true;
        task.WorkflowInstanceId = ApprovalInstance;
        task.ReviewWorkflowInstanceId = ReviewInstance;
        var tasks = new FakeTaskItemRepository(task);
        var gate = new FakeWorkflowTransitionGate();
        gate.BlockedObjectTypes.Add(TaskReviewService.ReviewObjectType);

        var result = await Transition(tasks, gate, task.Id, TaskLifecycle.InProgress, task.Version);

        Assert.True(result.IsSuccessful);
        Assert.Equal(TaskLifecycle.InProgress, tasks.Items.Single().Lifecycle);
    }

    [Fact]
    public async Task Both_gates_are_asked_on_completion_and_each_gets_its_OWN_reference()
    {
        var task = ReviewTask(TaskLifecycle.PendingReview);
        task.ApprovalRequired = true;
        task.WorkflowInstanceId = ApprovalInstance;
        task.ReviewWorkflowInstanceId = ReviewInstance;
        var tasks = new FakeTaskItemRepository(task);
        var gate = new FakeWorkflowTransitionGate { Blocked = false };

        await Complete(tasks, gate, task.Id, task.Version);

        Assert.Equal(2, gate.Calls.Count);
        var refs = gate.Calls.Select(c => c.ObjectRef).ToList();
        Assert.Contains(TaskApprovalService.BuildObjectRef(task.Id), refs);
        Assert.Contains(TaskReviewService.BuildObjectRef(task.Id), refs);
        // Non-vacuity: two calls that named the same object would prove nothing about separation.
        Assert.Equal(2, refs.Distinct().Count());
    }

    [Fact]
    public async Task A_refused_REVIEW_reports_the_review_reason_not_the_approval_one()
    {
        /*
         * The two gates clear differently and are cleared by different people. Telling a holder "waiting for
         * approval" while a reviewer is holding their work would send them to the wrong person entirely.
         *
         * This is not a theoretical distinction: MOD-0023 answers in its OWN vocabulary and reports a blocked
         * instance as WORKFLOW_PENDING_APPROVAL whichever question it was asked. This test failed the first time
         * it ran, because the handler forwarded that code unchanged.
         */
        var task = ReviewTask(TaskLifecycle.PendingReview);
        task.ReviewWorkflowInstanceId = ReviewInstance;
        var tasks = new FakeTaskItemRepository(task);
        var gate = new FakeWorkflowTransitionGate();
        gate.BlockedObjectTypes.Add(TaskReviewService.ReviewObjectType);

        var result = await Complete(tasks, gate, task.Id, task.Version);

        Assert.NotEqual(TaskReasonCodes.ApprovalPending, result.ReasonCode);
        Assert.Equal(TaskReasonCodes.ReviewPending, result.ReasonCode);
    }

    // ── Resubmission: a refused review is not a dead end ─────────────────────

    [Fact]
    public async Task Work_refused_by_its_reviewer_can_be_submitted_again()
    {
        /*
         * Deliberately unlike approval, whose refusal kills the request. A refused review hands the WORK back to
         * the person holding it — they still have it, and it is still theirs to fix — so a second round has to be
         * possible or the task would come back with nothing to press.
         *
         * The lifecycle matrix has no PendingReview → PendingReview edge, so this path exists only because the
         * REFUSAL was read from MOD-0023.
         */
        var task = ReviewTask(TaskLifecycle.PendingReview);
        task.ReviewWorkflowInstanceId = ReviewInstance;
        var tasks = new FakeTaskItemRepository(task);
        var reviews = new FakeTaskReviewService { InstanceId = Guid.Parse("55555555-5555-5555-5555-555555555555") };
        var states = new FakeTaskApprovalService();
        states.States[ReviewInstance] = new TaskApprovalState(IsPending: false, IsApproved: false, IsRejected: true);

        var result = await Submit(tasks, reviews, task.Id, task.Version, states);

        Assert.True(result.IsSuccessful);
        Assert.Equal([task.Id], reviews.Started);
        // A NEW instance replaces the refused one — the second round is genuinely a second decision.
        Assert.NotEqual(ReviewInstance, tasks.Items.Single().ReviewWorkflowInstanceId);
    }

    [Fact]
    public async Task Work_a_reviewer_is_STILL_holding_cannot_be_submitted_again()
    {
        /*
         * Non-vacuity for the test above: if the resubmission path keyed off the LIFECYCLE rather than the
         * verdict, this would pass too — and resubmitting would silently replace a review someone is in the
         * middle of.
         */
        var task = ReviewTask(TaskLifecycle.PendingReview);
        task.ReviewWorkflowInstanceId = ReviewInstance;
        var tasks = new FakeTaskItemRepository(task);
        var reviews = new FakeTaskReviewService();
        var states = new FakeTaskApprovalService();
        states.States[ReviewInstance] = new TaskApprovalState(IsPending: true, IsApproved: false, IsRejected: false);

        var result = await Submit(tasks, reviews, task.Id, task.Version, states);

        Assert.False(result.IsSuccessful);
        Assert.Empty(reviews.Started);
        Assert.Equal(ReviewInstance, tasks.Items.Single().ReviewWorkflowInstanceId);
    }

    // ── MOD-0024 grew no review engine ──────────────────────────────────────

    [Fact]
    public void TaskItem_carries_a_LINK_to_the_review_and_no_verdict_of_its_own()
    {
        /*
         * The charter check, as a property of the type rather than a promise in a comment. A field holding the
         * outcome, the reviewer who decided, or a review status would be a second source of truth that goes stale
         * the moment MOD-0023 records a verdict — and MOD-0024 would then be answering a question it does not own.
         */
        var names = typeof(TaskItem).GetProperties().Select(p => p.Name).ToList();

        Assert.Contains(nameof(TaskItem.ReviewWorkflowInstanceId), names);
        Assert.DoesNotContain("ReviewStatus", names);
        Assert.DoesNotContain("ReviewedByUserId", names);
        Assert.DoesNotContain("ReviewedAt", names);
        Assert.DoesNotContain("ReviewOutcome", names);
        Assert.DoesNotContain("ReviewDecision", names);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static Task<Response<NoContent>> Submit(
        FakeTaskItemRepository tasks,
        FakeTaskReviewService reviews,
        Guid id,
        int expectedVersion,
        FakeTaskApprovalService? states = null)
        => new SubmitTaskForReviewHandler(
                tasks,
                new TaskLifecycleService(),
                new FakeCurrentUserContext(TaskTestData.Me),
                reviews,
                states ?? new FakeTaskApprovalService(),
                NullLogger<SubmitTaskForReviewHandler>.Instance)
            .Handle(
                new SubmitTaskForReviewCommand(id, new TaskTransitionRequest(expectedVersion, null, null), "corr"),
                CancellationToken.None);

    private static Task<Response<NoContent>> Complete(
        FakeTaskItemRepository tasks, FakeWorkflowTransitionGate gate, Guid id, int expectedVersion)
        => Transition(tasks, gate, id, TaskLifecycle.Done, expectedVersion);

    private static Task<Response<NoContent>> Transition(
        FakeTaskItemRepository tasks,
        FakeWorkflowTransitionGate gate,
        Guid id,
        TaskLifecycle target,
        int expectedVersion)
        => new TransitionTaskItemHandler(
                tasks, new TaskLifecycleService(), new FakeCurrentUserContext(TaskTestData.Me),
                new FakeChecklistRunRepository(), new TaskChecklistService(), gate,
                new FakeTaskDependencyRepository())
            .Handle(
                new TransitionTaskItemCommand(id, target, new TaskTransitionRequest(expectedVersion, null, null), "corr"),
                CancellationToken.None);

    private static TaskItem ReviewTask(TaskLifecycle lifecycle) => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "İncelenecek iş",
        AssignmentTarget = TaskAssignmentTarget.Person,
        AssigneeUserId = TaskTestData.Me,
        CreatedByUserId = TaskTestData.Me,
        OrganizationUnitId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
        Lifecycle = lifecycle,
        ReviewRequired = true,
        Version = 1
    };
}
