using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// WP-WF-ENGINE-CANDIDATES-01 D2-FIX2 (item 1) — a task's MOD-0023 gates are asked while it HOLDS a link to an instance,
/// whatever its requirement flag says.
///
/// <para><b>Why the flag is not enough.</b> A withdraw that failed turns the requirement off and KEEPS the link
/// (D2-FIX1 A1): the instance may still be open and decidable. Asked only on the flag, the gates let that task start and
/// finish around a decision nobody would ever read — and once it is Done every edit is refused, so the retry that was
/// meant to withdraw it never runs. So the link alone is enough to ask, and the task waits until the engine reports the
/// instance closed.</para>
///
/// <para><b>MOD-0024 still decides nothing.</b> The verdict is the engine's gate answer. The one thing read from it here
/// is whether it says the instance is CLOSED (rejected or cancelled) — which, with the requirement already withdrawn, no
/// longer holds the task. With the requirement ON every blocked answer holds it, exactly as before. Anything the gate
/// cannot place (a timed-out instance reports "not terminal approved", an evaluation that failed reports nothing) keeps
/// holding: fail-closed. Such a link is dropped by the next edit, whose withdraw finds nothing open.</para>
///
/// <para><b>D2-FIX3.</b> The same rule over the READ model (<see cref="Holds(TaskApprovalState?, bool)"/>), so the button
/// and the refusal cannot disagree (item 3); and the one place a task withdraws what it started before it is closed,
/// deleted or handed over (<see cref="WithdrawAsync"/>, items 1, 2, 6, 8).</para>
/// </summary>
internal static class TaskWorkflowLinkGate
{
    /// <summary>The approval gate applies: approval is required, or a link to an approval instance is still held.</summary>
    public static bool AsksApproval(TaskItem task) => task.ApprovalRequired || task.WorkflowInstanceId is not null;

    /// <summary>The review gate applies: review is required, or a link to a review instance is still held.</summary>
    public static bool AsksReview(TaskItem task) => task.ReviewRequired || task.ReviewWorkflowInstanceId is not null;

    /// <summary>
    /// Whether a gate answer holds the task back. <paramref name="required"/> is the task's own requirement flag for that
    /// gate: required, every blocked answer holds; withdrawn, only an instance the engine does not report closed does.
    /// </summary>
    public static bool Holds(WorkflowGateResult gate, bool required) =>
        gate.IsBlocked && (required || !ReportsClosed(gate));

    /// <summary>
    /// D2-FIX3 item 3 — <see cref="Holds(WorkflowGateResult, bool)"/> over what the read model knows of the instance: not
    /// approved holds when the requirement is on; with it withdrawn, only an instance MOD-0023 does not report closed
    /// (rejected or cancelled) holds. An instance that could not be read (null) holds — fail-closed, like the gate.
    /// </summary>
    public static bool Holds(TaskApprovalState? state, bool required) =>
        state is null || (!state.IsApproved && (required || !(state.IsRejected || state.IsCancelled)));

    private static bool ReportsClosed(WorkflowGateResult gate) =>
        string.Equals(gate.GateStatus, nameof(WorkflowTransitionGateStatus.Rejected), StringComparison.OrdinalIgnoreCase)
        || string.Equals(gate.GateStatus, nameof(WorkflowTransitionGateStatus.Cancelled), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// D2-FIX3 — whether a review may be running for this task: it holds a link, or the requirement is on and a withdraw
    /// service is there to look for one whose link was never stored (a submit that lost its race after the engine started
    /// the review — item 8). Without a service only a held link asks, and is then refused (fail-closed).
    /// </summary>
    public static bool MayHoldReview(TaskItem task, ITaskReviewService? reviews) =>
        task.ReviewWorkflowInstanceId is not null || (task.ReviewRequired && reviews is not null);

    /// <summary>The approval counterpart of <see cref="MayHoldReview"/> (a link write that fell over after the start).</summary>
    public static bool MayHoldApproval(TaskItem task, ITaskApprovalService? approvals) =>
        task.WorkflowInstanceId is not null || (task.ApprovalRequired && approvals is not null);

    /// <summary>
    /// D2-FIX3 items 1, 6, 8 — withdraws what the task started in MOD-0023 before it is cancelled or deleted, through
    /// MOD-0023's own cancel command (the services an edit uses; MOD-0024 never edits workflow state). Null when nothing is
    /// left open; otherwise the EXISTING refusal code that says how far it got:
    /// <list type="bullet">
    /// <item><c>TASK_REVIEW_CANCEL_FAILED</c> — the review could not be withdrawn, and NOTHING was: the review goes first.</item>
    /// <item><c>TASK_APPROVAL_CANCEL_FAILED</c> — the review (if any) WAS withdrawn, the approval was not.</item>
    /// </list>
    /// <para><b>Why the review first (item 6).</b> The two withdraws are not one transaction, so one may land and the other
    /// fail. A withdrawn review leaves nothing that cannot be redone: its link is KEPT, the work is the holder's to submit
    /// again, and a new round opens under a new key (the review key names the round it replaces). A withdrawn approval
    /// cannot be redone — its key is the task's own, so a restart would replay the cancelled instance — so it goes LAST,
    /// and is never the half that landed when the other did not. Each link stays where it is: the one still open keeps the
    /// task gated, the withdrawn review's link is what the next submission reads.</para>
    /// </summary>
    public static async Task<string?> WithdrawAsync(
        TaskItem task,
        ITaskApprovalService? approvals,
        ITaskReviewService? reviews,
        CancellationToken ct)
    {
        if (!await WithdrawReviewAsync(task, reviews, ct))
        {
            return TaskReasonCodes.ReviewCancelFailed;
        }

        if (MayHoldApproval(task, approvals) && (approvals is null || !await approvals.CancelApprovalAsync(task, ct)))
        {
            return TaskReasonCodes.ApprovalCancelFailed;
        }

        return null;
    }

    /// <summary>
    /// D2-FIX3 item 2 — the review half alone, for the hand-overs (return, reassign, release): the next holder must not
    /// inherit a review somebody else's submission opened. True when nothing review-wise is left open. Fail-closed: a held
    /// link with no service to withdraw it is false.
    /// </summary>
    public static async Task<bool> WithdrawReviewAsync(TaskItem task, ITaskReviewService? reviews, CancellationToken ct) =>
        !MayHoldReview(task, reviews) || (reviews is not null && await reviews.CancelReviewAsync(task, ct));

    /// <summary>
    /// D2-FIX5 item 1 — a start whose task write LOST its race is read again, and when nobody wants what it started any more
    /// the instance is withdrawn.
    ///
    /// <para><b>The path.</b> A submission (review) or an edit that switches approval on starts the instance in MOD-0023 FIRST
    /// and writes the task's link AFTER, and the write is conditional on the version read at the start. A rival writer in
    /// between — an edit that switched the requirement off, a cancel, a delete — moves the version, the write loses, and the
    /// engine is left with a live instance that no longer has anything to do with the task: the rival's withdraw ran BEFORE
    /// the start, found nothing, and nothing looks again (the requirement is off, no link is held). The approver or the
    /// reviewer kept a decision nobody reads.</para>
    ///
    /// <para><b>The rule.</b> The task is read again (this is only the lost-race branch, so the read is paid by the rare
    /// path). Still wanted — the requirement is on and the task is open — the instance is KEPT: the lost write was somebody
    /// else's unrelated save, and the retry adopts the running instance (one live round per task). Not wanted — the
    /// requirement is off, the task is closed, or it is gone — the instance just started is withdrawn through MOD-0023's own
    /// cancel (MOD-0024 decides nothing). A withdraw that does not happen is logged and the link is stored on the task as it
    /// is now (a few bounded tries), so every later look finds it: an edit of a task with a held link retries the withdraw,
    /// and a cancel or a delete asks for it. A deleted task has no document left to carry the link — the log is all there is.
    /// </para>
    ///
    /// <para>Best effort and never throws (the caller answers its own 409 whatever happens here), except for cancellation.</para>
    /// </summary>
    public static async Task WithdrawUnwantedStartAsync(
        TaskItem started,
        bool review,
        ITaskItemRepository tasks,
        ITaskApprovalService? approvals,
        ITaskReviewService? reviews,
        ILogger logger,
        CancellationToken ct)
    {
        var what = review ? "review" : "approval";
        var instanceId = review ? started.ReviewWorkflowInstanceId : started.WorkflowInstanceId;

        try
        {
            var fresh = await tasks.GetByIdAsync(started.Id, ct);
            if (fresh is not null && (review ? fresh.ReviewRequired : fresh.ApprovalRequired) && !IsClosed(fresh))
            {
                return;
            }

            // A task that is gone is withdrawn through the copy the caller wrote from: it holds the instance as its link.
            var subject = fresh ?? started;
            var withdrawn = review
                ? reviews is not null && await reviews.CancelReviewAsync(subject, ct)
                : approvals is not null && await approvals.CancelApprovalAsync(subject, ct);
            if (withdrawn)
            {
                logger.LogInformation(
                    "Task {TaskId} lost its race after its {What} {InstanceId} was started and nobody wants it any more; it was withdrawn.",
                    started.Id, what, instanceId);
                return;
            }

            logger.LogWarning(
                "Task {TaskId} lost its race after its {What} {InstanceId} was started and nobody wants it any more, but it could not "
                + "be withdrawn; the task is made to carry its link so that the next look finds it.",
                started.Id, what, instanceId);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            logger.LogError(failure,
                "Task {TaskId} lost its race after its {What} {InstanceId} was started and nobody wants it any more, and looking at "
                + "or withdrawing it threw; the task is made to carry its link so that the next look finds it.",
                started.Id, what, instanceId);
        }

        if (instanceId is { } liveInstance)
        {
            await StoreLinkAsync(started.Id, liveInstance, review, tasks, logger, ct);
        }
    }

    /// <summary>
    /// D2-FIX5 item 1 — the link of an instance that could NOT be withdrawn is written onto the task as it is NOW (read again for
    /// each try: the version is what keeps moving), so that the retry paths — an edit with the requirement off, a cancel, a
    /// delete — have something to follow. Bounded; the last failure is logged, never thrown.
    /// </summary>
    private static async Task StoreLinkAsync(
        Guid taskId, Guid instanceId, bool review, ITaskItemRepository tasks, ILogger logger, CancellationToken ct)
    {
        var what = review ? "review" : "approval";

        try
        {
            for (var attempt = 1; attempt <= MaxLinkStoreAttempts; attempt++)
            {
                var current = await tasks.GetByIdAsync(taskId, ct);
                if (current is null)
                {
                    logger.LogError(
                        "The {What} {InstanceId} of deleted task {TaskId} is still open and could not be withdrawn; nothing is left to carry its link.",
                        what, instanceId, taskId);
                    return;
                }

                if (review)
                {
                    current.ReviewWorkflowInstanceId = instanceId;
                }
                else
                {
                    current.WorkflowInstanceId = instanceId;
                }

                if (await tasks.UpdateAsync(current, current.Version, ct))
                {
                    return;
                }
            }

            logger.LogError(
                "The {What} {InstanceId} of task {TaskId} is still open, could not be withdrawn, and its link could not be stored in "
                + "{Attempts} tries; only the task's object reference leads to it.",
                what, instanceId, taskId, MaxLinkStoreAttempts);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            logger.LogError(failure,
                "The {What} {InstanceId} of task {TaskId} is still open, could not be withdrawn, and its link could not be stored.",
                what, instanceId, taskId);
        }
    }

    /// <summary>D2-FIX5 item 1 — how many times the link of an instance that could not be withdrawn is written while the task's
    /// version keeps moving.</summary>
    internal const int MaxLinkStoreAttempts = 3;

    private static bool IsClosed(TaskItem task) =>
        task.Lifecycle is TaskLifecycle.Done or TaskLifecycle.Cancelled
        || task.CompletedAt is not null
        || task.CancelledAt is not null;
}
