using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// MOD-0024 — pure, deterministic lifecycle semantics (pack §4). No I/O, no clock reads beyond what is handed in,
/// so the same task always projects the same way.
///
/// <para>The normalize map is authoritative and replaces the mock's incorrect pairing (the WorkCenterNext fixture
/// factory emitted <c>taskLifecycle: 'Open'</c> together with <c>normalizedStatus: 'InProgress'</c>, which claims
/// an unstarted task is already in progress).</para>
/// </summary>
public sealed class TaskLifecycleService : ITaskLifecycleService
{
    // Contract normalizedStatus values (fixture-contract.js NORMALIZED_STATUSES).
    private const string Pending = "Pending";
    private const string InProgress = "InProgress";
    private const string Waiting = "Waiting";
    private const string Done = "Done";
    private const string Cancelled = "Cancelled";

    public TaskLifecycle ResolveInitialLifecycle(bool approvalRequired)
        // An approval-gated task is NOT startable yet. It stays Open and is projected as Waiting + an approval
        // waitingContext, so the user cannot "start" work the approver has not released. The user never chooses
        // this — the system does (pack §12 Y2).
        => TaskLifecycle.Open;

    public string ToNormalizedStatus(TaskItem task, bool approvalOutstanding, bool approvalRejected = false)
    {
        ArgumentNullException.ThrowIfNull(task);

        // A REJECTED approval is the one approval outcome that lands in MOD-0024's own lifecycle: the work was
        // refused, so the task is dead (pack §12 K2). It outranks the native lifecycle and the outstanding flag —
        // a refused task is neither waiting nor workable.
        if (approvalRejected && !IsTerminal(task))
        {
            return Cancelled;
        }

        // An approval still OUTSTANDING outranks the native lifecycle: the task is genuinely waiting on someone
        // else. The caller supplies this from MOD-0023's instance state — it is not inferable from the task,
        // because ApprovalRequired only records that approval was asked for, never whether it has been given.
        if (approvalOutstanding)
        {
            return Waiting;
        }

        return task.Lifecycle switch
        {
            // "Backlog" from the prototype maps here — the contract has no Backlog (pack §12 Y2).
            TaskLifecycle.Open => Pending,
            TaskLifecycle.Planned => Pending,
            TaskLifecycle.InProgress => InProgress,
            TaskLifecycle.Waiting => Waiting,
            // The owner still sees a review-pending task, but it is waiting on the reviewer.
            TaskLifecycle.PendingReview => Waiting,
            TaskLifecycle.Done => Done,
            TaskLifecycle.Cancelled => Cancelled,
            _ => throw new InvalidOperationException($"Unmapped TaskLifecycle: {task.Lifecycle}")
        };
    }

    public TaskWaitingContext? ResolveWaitingContext(TaskItem task, bool approvalOutstanding, bool approvalRejected = false)
    {
        ArgumentNullException.ThrowIfNull(task);

        // Rejected reads as Cancelled, and the contract forbids a waitingContext on a non-Waiting item.
        if (approvalRejected && !IsTerminal(task))
        {
            return null;
        }

        if (approvalOutstanding)
        {
            return new TaskWaitingContext(
                TaskWaitingTypes.Approval,
                task.ApprovalManagerUserId?.ToString(),
                task.CreatedAt,
                task.DueAt);
        }

        return task.Lifecycle switch
        {
            TaskLifecycle.PendingReview => new TaskWaitingContext(
                TaskWaitingTypes.Review, null, task.UpdatedAt ?? task.CreatedAt, task.DueAt),
            // WaitingReason is what the holder typed when they parked it (InquireTaskItemHandler makes it
            // mandatory), so the wait says what it is for instead of only that it exists.
            TaskLifecycle.Waiting => new TaskWaitingContext(
                TaskWaitingTypes.ExternalInformation, task.WaitingReason, task.UpdatedAt ?? task.CreatedAt, task.DueAt),
            _ => null
        };
    }

    public decimal? CalculateRemainingHours(TaskItem task)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (task.EstimateHours is null)
        {
            return null;
        }

        var remaining = task.EstimateHours.Value - task.SpentHours;
        return remaining < 0 ? 0 : remaining;
    }

    public bool IsTerminal(TaskItem task)
    {
        ArgumentNullException.ThrowIfNull(task);
        return task.Lifecycle is TaskLifecycle.Done or TaskLifecycle.Cancelled;
    }

    public bool CanTransition(TaskItem task, TaskLifecycle target, out string? reasonCode)
    {
        ArgumentNullException.ThrowIfNull(task);
        reasonCode = null;

        if (IsTerminal(task))
        {
            reasonCode = TaskReasonCodes.InvalidState;
            return false;
        }

        // NOTE — approval is deliberately NOT judged here (Phase 3, pack §12 K2 / charter Binding A).
        //
        // Phase 1 blocked `start` locally whenever ApprovalRequired was set, because no real approval existed yet.
        // That local rule IS a second approval engine, which the charter forbids: it decided from a flag on the
        // task instead of from the workflow's actual state, so it could never tell "pending" from "approved" and
        // an approved task could never start.
        //
        // The decision now belongs to MOD-0023 via IWorkflowTransitionGate, consulted by
        // TransitionTaskItemHandler before it commits — fail-closed, so removing the check here does not open a
        // hole. What remains below are MOD-0024's OWN rules (terminal states, pool claiming, legal transitions).
        //
        // The approval-derived PROJECTION is unchanged: ToNormalizedStatus/ResolveWaitingContext still report
        // Waiting + waitingContext while approval is outstanding. Reporting a state is not deciding a transition.

        // A pool task with no holder cannot progress: it must be claimed first.
        if (target is TaskLifecycle.InProgress or TaskLifecycle.PendingReview or TaskLifecycle.Done
            && task.AssignmentTarget == TaskAssignmentTarget.PositionPool
            && task.AssigneeUserId is null)
        {
            reasonCode = TaskReasonCodes.NotClaimable;
            return false;
        }

        var allowed = task.Lifecycle switch
        {
            TaskLifecycle.Open => target is TaskLifecycle.Planned or TaskLifecycle.InProgress
                or TaskLifecycle.Waiting or TaskLifecycle.Cancelled,
            TaskLifecycle.Planned => target is TaskLifecycle.InProgress or TaskLifecycle.Waiting
                or TaskLifecycle.Open or TaskLifecycle.Cancelled,
            TaskLifecycle.InProgress => target is TaskLifecycle.Waiting or TaskLifecycle.PendingReview
                or TaskLifecycle.Done or TaskLifecycle.Cancelled,
            TaskLifecycle.Waiting => target is TaskLifecycle.InProgress or TaskLifecycle.Cancelled,
            TaskLifecycle.PendingReview => target is TaskLifecycle.InProgress or TaskLifecycle.Done
                or TaskLifecycle.Cancelled,
            _ => false
        };

        if (!allowed)
        {
            reasonCode = TaskReasonCodes.InvalidState;
        }

        return allowed;
    }

}
