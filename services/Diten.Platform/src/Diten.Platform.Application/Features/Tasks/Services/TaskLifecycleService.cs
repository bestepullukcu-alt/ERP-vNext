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

    public string ToNormalizedStatus(TaskItem task)
    {
        ArgumentNullException.ThrowIfNull(task);

        // An approval still pending outranks the native lifecycle: the task is genuinely waiting on someone else.
        if (IsAwaitingApproval(task))
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

    public TaskWaitingContext? ResolveWaitingContext(TaskItem task)
    {
        ArgumentNullException.ThrowIfNull(task);

        if (IsAwaitingApproval(task))
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
            TaskLifecycle.Waiting => new TaskWaitingContext(
                TaskWaitingTypes.ExternalInformation, null, task.UpdatedAt ?? task.CreatedAt, task.DueAt),
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

        // Work cannot begin while an approval is outstanding — MOD-0023 owns that release (pack §12 K2).
        if (target is TaskLifecycle.InProgress && IsAwaitingApproval(task))
        {
            reasonCode = TaskReasonCodes.InvalidState;
            return false;
        }

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

    /// <summary>
    /// True while an approval was requested and MOD-0023 has not released the task. Phase 1 stores the flags and
    /// the instance reference; Phase 3 wires the actual handoff. MOD-0024 never stores an approval STATE.
    /// </summary>
    private static bool IsAwaitingApproval(TaskItem task)
        => task.ApprovalRequired
           && task.Lifecycle is TaskLifecycle.Open or TaskLifecycle.Planned
           && task.CompletedAt is null
           && task.CancelledAt is null;
}
