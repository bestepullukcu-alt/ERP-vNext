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

    private static bool ReportsClosed(WorkflowGateResult gate) =>
        string.Equals(gate.GateStatus, nameof(WorkflowTransitionGateStatus.Rejected), StringComparison.OrdinalIgnoreCase)
        || string.Equals(gate.GateStatus, nameof(WorkflowTransitionGateStatus.Cancelled), StringComparison.OrdinalIgnoreCase);
}
