using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

// MOD-0024 — the lifecycle→normalized map is the contract-facing heart of the engine: if it drifts, the Task
// Center renders a lie. Every lifecycle value is asserted, plus the Waiting ⇔ waitingContext pairing the
// executable contract enforces bidirectionally.
public sealed class TaskLifecycleServiceTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Me = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private readonly TaskLifecycleService _sut = new();

    [Theory]
    [InlineData(TaskLifecycle.Open, "Pending")]        // "Backlog" in the prototype maps here
    [InlineData(TaskLifecycle.Planned, "Pending")]
    [InlineData(TaskLifecycle.InProgress, "InProgress")]
    [InlineData(TaskLifecycle.Waiting, "Waiting")]
    // PendingReview maps by REVIEW STATE, not by the lifecycle alone (Faz 3b) — the theory passes no review
    // flags, so this row is the "review already answered" case: the work is back with its holder. The
    // reviewer-is-holding-it case is asserted explicitly below, where the flag can be supplied.
    [InlineData(TaskLifecycle.PendingReview, "InProgress")]
    [InlineData(TaskLifecycle.Done, "Done")]
    [InlineData(TaskLifecycle.Cancelled, "Cancelled")]
    public void Maps_every_lifecycle_value_to_a_contract_normalized_status(TaskLifecycle lifecycle, string expected)
    {
        var task = MakeTask(lifecycle);
        Assert.Equal(expected, _sut.ToNormalizedStatus(task, approvalOutstanding: false));
    }

    [Fact]
    public void An_unstarted_task_is_Pending_not_InProgress()
    {
        // The existing WorkCenterNext mock emitted Open + InProgress together, claiming an unstarted task was
        // already being worked. That pairing is wrong and must not come back.
        var task = MakeTask(TaskLifecycle.Open);
        Assert.Equal("Pending", _sut.ToNormalizedStatus(task, approvalOutstanding: false));
    }

    [Theory]
    [InlineData(TaskLifecycle.Open)]
    [InlineData(TaskLifecycle.Planned)]
    [InlineData(TaskLifecycle.InProgress)]
    [InlineData(TaskLifecycle.Waiting)]
    [InlineData(TaskLifecycle.PendingReview)]
    [InlineData(TaskLifecycle.Done)]
    [InlineData(TaskLifecycle.Cancelled)]
    public void Waiting_status_and_waitingContext_are_bidirectional(TaskLifecycle lifecycle)
    {
        var task = MakeTask(lifecycle);
        var isWaiting = _sut.ToNormalizedStatus(task, approvalOutstanding: false) == "Waiting";
        var context = _sut.ResolveWaitingContext(task, approvalOutstanding: false);

        // The contract rejects a Waiting item without a context, and a context on a non-Waiting item.
        Assert.Equal(isWaiting, context is not null);
    }

    [Fact]
    public void An_approval_gated_task_waits_on_the_approval_not_on_its_own_lifecycle()
    {
        var task = MakeTask(TaskLifecycle.Open);
        task.ApprovalRequired = true;
        task.ApprovalManagerUserId = Me;

        Assert.Equal("Waiting", _sut.ToNormalizedStatus(task, approvalOutstanding: true));
        var context = _sut.ResolveWaitingContext(task, approvalOutstanding: true);
        Assert.NotNull(context);
        Assert.Equal(TaskWaitingTypes.Approval, context!.Type);
    }

    [Fact]
    public void The_lifecycle_service_does_NOT_judge_approval_the_workflow_gate_does()
    {
        // Phase 3 moved this decision to MOD-0023 (pack §12 K2 / charter Binding A). Deciding it here — from a
        // FLAG on the task rather than the workflow's real state — was a second approval engine: it could not
        // tell "pending" from "approved", so an approved task could never have started.
        //
        // Removing it opens no hole: TransitionTaskItemHandler consults IWorkflowTransitionGate before committing
        // and is fail-closed. TaskApprovalGateTests covers blocked / allowed / evaluation-failed.
        var task = MakeTask(TaskLifecycle.Open);
        task.ApprovalRequired = true;

        Assert.True(_sut.CanTransition(task, TaskLifecycle.InProgress, out var reason));
        Assert.Null(reason);
    }

    [Fact]
    public void Approval_still_drives_the_PROJECTION_even_though_it_no_longer_drives_the_transition()
    {
        // Reporting a state is not deciding a transition: the Task Center must still show Waiting + waitingContext.
        var task = MakeTask(TaskLifecycle.Open);
        task.ApprovalRequired = true;

        Assert.Equal("Waiting", _sut.ToNormalizedStatus(task, approvalOutstanding: true));
        Assert.Equal(TaskWaitingTypes.Approval, _sut.ResolveWaitingContext(task, approvalOutstanding: true)!.Type);
    }

    [Fact]
    public void PendingReview_waits_on_the_reviewer()
    {
        var task = MakeTask(TaskLifecycle.PendingReview);
        var context = _sut.ResolveWaitingContext(task, approvalOutstanding: false, reviewOutstanding: true);
        Assert.Equal(TaskWaitingTypes.Review, context!.Type);
        Assert.Equal("Waiting", _sut.ToNormalizedStatus(task, approvalOutstanding: false, reviewOutstanding: true));
    }

    [Fact]
    public void A_review_that_has_ANSWERED_stops_the_task_waiting_on_it()
    {
        /*
         * Faz 3b. The lifecycle stays PendingReview — nothing writes it back — so the answer can only come from
         * MOD-0023's instance. Before this, a released review and a refused one were both reported as "waiting on
         * the reviewer", and neither could ever be told from the other.
         *
         * A refused review reads InProgress rather than Cancelled: unlike a refused approval, which kills the
         * request, it hands the WORK back to the person holding it.
         */
        var task = MakeTask(TaskLifecycle.PendingReview);

        Assert.Equal("InProgress", _sut.ToNormalizedStatus(
            task, approvalOutstanding: false, reviewOutstanding: false));
        Assert.Equal("InProgress", _sut.ToNormalizedStatus(
            task, approvalOutstanding: false, approvalRejected: false,
            reviewOutstanding: false, reviewRejected: true));

        // The contract pairs Waiting with a waitingContext in BOTH directions, so a task that no longer reads
        // Waiting must not keep one.
        Assert.Null(_sut.ResolveWaitingContext(task, approvalOutstanding: false, reviewOutstanding: false));
        Assert.Null(_sut.ResolveWaitingContext(
            task, approvalOutstanding: false, approvalRejected: false,
            reviewOutstanding: true, reviewRejected: true));
    }

    [Theory]
    [InlineData(TaskLifecycle.Done)]
    [InlineData(TaskLifecycle.Cancelled)]
    public void Terminal_tasks_are_read_only(TaskLifecycle lifecycle)
    {
        var task = MakeTask(lifecycle);
        Assert.True(_sut.IsTerminal(task));
        Assert.False(_sut.CanTransition(task, TaskLifecycle.InProgress, out _));
    }

    [Fact]
    public void An_unclaimed_pool_task_cannot_progress_before_it_is_claimed()
    {
        var task = MakeTask(TaskLifecycle.Open);
        task.AssignmentTarget = TaskAssignmentTarget.PositionPool;
        task.PoolPositionId = Guid.NewGuid();
        task.AssigneeUserId = null;

        Assert.False(_sut.CanTransition(task, TaskLifecycle.InProgress, out var reason));
        Assert.Equal("TASK_NOT_CLAIMABLE", reason);
    }

    [Fact]
    public void Remaining_hours_are_derived_and_floored_at_zero()
    {
        var task = MakeTask(TaskLifecycle.InProgress);

        task.EstimateHours = null;
        Assert.Null(_sut.CalculateRemainingHours(task));

        task.EstimateHours = 8m;
        task.SpentHours = 3m;
        Assert.Equal(5m, _sut.CalculateRemainingHours(task));

        // Over-spend must never surface as a negative remaining.
        task.SpentHours = 11m;
        Assert.Equal(0m, _sut.CalculateRemainingHours(task));
    }

    [Fact]
    public void The_initial_lifecycle_is_system_decided_and_never_startable_by_default()
    {
        Assert.Equal(TaskLifecycle.Open, _sut.ResolveInitialLifecycle(approvalRequired: false));
        Assert.Equal(TaskLifecycle.Open, _sut.ResolveInitialLifecycle(approvalRequired: true));
    }

    private static TaskItem MakeTask(TaskLifecycle lifecycle) => new()
    {
        TenantId = Tenant,
        Title = "Sample",
        Lifecycle = lifecycle,
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = Me,
        OrganizationUnitId = Guid.NewGuid(),
        CompletedAt = lifecycle == TaskLifecycle.Done ? DateTimeOffset.UtcNow : null,
        CancelledAt = lifecycle == TaskLifecycle.Cancelled ? DateTimeOffset.UtcNow : null
    };
}
