using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Domain.Entities.Tasks;

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
}
